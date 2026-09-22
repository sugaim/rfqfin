import { useState } from 'react'
import { Form } from 'antd'
import type { GridApi } from 'ag-grid-community'
import type {
  CreateDraftRequest,
  SalesRfq,
  UpdateDraftRequest,
} from '@/services/api'
import { derivePaneMode, type SalesPaneMode } from '@/features/sales/salesModel'
import type { RfqFormValues } from '@/features/sales/SalesRfqEditor'
import type {
  SalesDraftActions,
  SalesLookupActions,
} from '@/features/sales/salesContracts'

const million = 1_000_000

interface UseSalesRfqEditorInput {
  rfqs: SalesRfq[]
  activeCaseId?: number
  gridApi: GridApi<SalesRfq> | null
  draft: SalesDraftActions
  lookup: SalesLookupActions
  run: (action: () => Promise<void>, reload?: boolean) => Promise<boolean>
  onError: (message: string | null) => void
  onStartNew: () => void
  onSelectRow: (row: SalesRfq) => void
}

export interface SalesRfqEditorController {
  form: ReturnType<typeof Form.useForm<RfqFormValues>>[0]
  mode: SalesPaneMode
  newIntent: boolean
  defaultsLoading: boolean
  startNew: () => void
  cancelNew: () => void
  selectRow: (row: SalesRfq) => void
  applyDefaults: (securityId: string) => Promise<void>
  save: (confirm: boolean) => Promise<void>
  discard: (row: SalesRfq) => Promise<void>
}

export function useSalesRfqEditor({
  rfqs,
  activeCaseId,
  gridApi,
  draft,
  lookup,
  run,
  onError,
  onStartNew,
  onSelectRow,
}: UseSalesRfqEditorInput): SalesRfqEditorController {
  const [form] = Form.useForm<RfqFormValues>()
  const [newIntent, setNewIntent] = useState(false)
  const [defaultsLoading, setDefaultsLoading] = useState(false)
  const selected = rfqs.find((row) => row.caseId === activeCaseId)
  const mode = derivePaneMode(rfqs, activeCaseId, newIntent)

  const startNew = () => {
    setNewIntent(true)
    onStartNew()
    gridApi?.deselectAll()
    form.resetFields()
    onError(null)
  }

  const selectRow = (row: SalesRfq) => {
    setNewIntent(false)
    onSelectRow(row)
    onError(null)
    if (row.revisionStatus === 'Draft') {
      form.setFieldsValue({
        clientId: row.clientId,
        securityId: row.securityId,
        categoryName: row.categoryId,
        assignedTraderId: row.assignedTraderId,
        settlementDate: row.settlementDate ?? undefined,
        standardSettlementDate: row.standardSettlementDate,
        notional: row.notional === null ? undefined : row.notional / million,
        salesAndTradingMessage: row.salesAndTradingMessage,
      })
    } else {
      form.resetFields()
    }
  }

  const applyDefaults = async (securityId: string) => {
    if (!(newIntent || mode === 'draft')) return
    setDefaultsLoading(true)
    try {
      const value = await lookup.resolveDefaults(securityId)
      form.setFieldsValue({
        categoryName: value.categoryName,
        assignedTraderId: value.defaultAssignedTraderId,
        standardSettlementDate: value.standardSettlementDate,
        settlementDate: value.standardSettlementDate,
      })
    } catch {
      onError('Security defaults could not be resolved.')
    } finally {
      setDefaultsLoading(false)
    }
  }

  const save = async (confirm: boolean) => {
    try {
      await form.validateFields(
        confirm
          ? [
              'clientId',
              'securityId',
              'assignedTraderId',
              'settlementDate',
              'notional',
            ]
          : ['clientId', 'securityId'],
      )
    } catch {
      return
    }
    const values = form.getFieldsValue(true) as RfqFormValues
    const request: CreateDraftRequest = {
      clientId: values.clientId,
      securityId: values.securityId,
      assignedTraderId: values.assignedTraderId,
      settlementDate: values.settlementDate,
      standardSettlementDate: values.standardSettlementDate,
      notional:
        values.notional === undefined ? undefined : values.notional * million,
      salesAndTradingMessage: values.salesAndTradingMessage ?? '',
    }
    if (
      !request.assignedTraderId ||
      !request.settlementDate ||
      !request.standardSettlementDate
    ) {
      onError(
        'Select a Security so routing and settlement defaults are resolved.',
      )

      return
    }
    const succeeded = await run(async () => {
      if (mode === 'draft' && selected) {
        const update: UpdateDraftRequest = {
          notional: request.notional,
          settlementDate: request.settlementDate,
          standardSettlementDate: request.standardSettlementDate,
          salesAndTradingMessage: request.salesAndTradingMessage,
          assignedTraderId: request.assignedTraderId,
          expectedVersion: selected.version,
        }
        await (confirm
          ? draft.confirm(selected.caseId, update)
          : draft.update(selected.caseId, update))
      } else {
        await (confirm ? draft.confirmNew(request) : draft.create(request))
      }
    })
    if (succeeded) setNewIntent(false)
  }

  return {
    form,
    mode,
    newIntent,
    defaultsLoading,
    startNew,
    cancelNew: () => setNewIntent(false),
    selectRow,
    applyDefaults,
    save,
    discard: async (row) => {
      if (await run(() => draft.discard(row.caseId, row.version)))
        setNewIntent(false)
    },
  }
}
