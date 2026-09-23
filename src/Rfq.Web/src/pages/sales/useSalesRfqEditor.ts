import { useRef, useState } from 'react'
import { Form } from 'antd'
import type { GridApi } from 'ag-grid-community'
import type {
  ApiProblemDetails,
  CreateDraftRequest,
  InitialRfqResponse,
  SalesRfqResponse,
} from '@/generated/rfqApi'
import { derivePaneMode, type SalesPaneMode } from '@/pages/sales/salesModel'
import type { RfqFormValues } from '@/pages/sales/SalesRfqEditor'
import type {
  SalesDraftActions,
  SalesLookupActions,
} from '@/pages/sales/salesContracts'
import {
  createCaseAutosaveCoordinator,
  type CaseAutosaveCoordinator,
} from '@/pages/sales/caseAutosaveCoordinator'

const million = 1_000_000

interface UseSalesRfqEditorInput {
  rfqs: SalesRfqResponse[]
  activeCaseId?: number
  gridApi: GridApi<SalesRfqResponse> | null
  draft: SalesDraftActions
  lookup: SalesLookupActions
  run: (action: () => Promise<unknown>, caseIds?: number[]) => Promise<boolean>
  onError: (message: string | null) => void
  onStartNew: () => void
  onSelectRow: (row: SalesRfqResponse) => void
  onPersisted: (caseId: number) => void
  onReconcileCases: (caseIds: number[]) => Promise<void>
}

interface DraftAutosaveState {
  version: number
  notional: number | null
  settlementDate: string | null
  standardSettlementDate: string | null
  salesAndTradingMessage: string
  assignedTraderId: string
}

type DraftDelta = Omit<DraftAutosaveState, 'version'>

export interface SalesRfqEditorController {
  form: ReturnType<typeof Form.useForm<RfqFormValues>>[0]
  mode: SalesPaneMode
  newIntent: boolean
  defaultsLoading: boolean
  protectedState: boolean
  startNew: () => void
  cancelNew: () => void
  selectRow: (row: SalesRfqResponse) => void
  applyDefaults: (securityId: string) => Promise<void>
  beginEdit: () => void
  completeEdit: (field: keyof DraftDelta) => Promise<void>
  save: (confirm: boolean) => Promise<void>
  discard: (row: SalesRfqResponse) => Promise<void>
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
  onPersisted,
  onReconcileCases,
}: UseSalesRfqEditorInput): SalesRfqEditorController {
  const [form] = Form.useForm<RfqFormValues>()
  const [newIntent, setNewIntent] = useState(false)
  const [defaultsLoading, setDefaultsLoading] = useState(false)
  const [editing, setEditing] = useState(false)
  const [saving, setSaving] = useState(false)
  const autosave = useRef<{
    caseId: number
    value: CaseAutosaveCoordinator<DraftAutosaveState, DraftDelta>
  } | null>(null)
  const selected = rfqs.find((row) => row.caseId === activeCaseId)
  const mode = derivePaneMode(rfqs, activeCaseId, newIntent)

  const startNew = () => {
    setNewIntent(true)
    onStartNew()
    gridApi?.deselectAll()
    form.resetFields()
    onError(null)
  }

  const selectRow = (row: SalesRfqResponse) => {
    setNewIntent(false)
    onSelectRow(row)
    onError(null)
    if (row.revisionStatus === 'Draft') {
      autosave.current = {
        caseId: row.caseId,
        value: createCaseAutosaveCoordinator(
          stateFromRow(row),
          async (state, delta) => {
            setSaving(true)
            try {
              const { version, ...values } = state
              const result = await draft.update(row.caseId, {
                ...values,
                ...delta,
                expectedCurrentVersion: version,
              })

              return stateFromResponse(result)
            } finally {
              setSaving(false)
            }
          },
        ),
      }
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
      autosave.current = null
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
        values.notional === undefined ? null : values.notional * million,
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
    let created: InitialRfqResponse | undefined
    const succeeded = await run(
      async () => {
        if (mode === 'draft' && selected) {
          const coordinator = ensureCoordinator(selected)
          await coordinator.enqueue(confirm ? {} : deltaFromRequest(request))
          const state = coordinator.acknowledged()
          if (confirm) {
            const { version, ...values } = state
            await draft.confirm(selected.caseId, {
              ...values,
              expectedCurrentVersion: version,
            })
          }
        } else {
          created = await (confirm
            ? draft.confirmNew(request)
            : draft.create(request))
        }
      },
      selected ? [selected.caseId] : [],
    )
    if (succeeded) {
      setNewIntent(false)
      if (created) {
        await onReconcileCases([created.caseId]).catch(() => undefined)
        onPersisted(created.caseId)
      }
    }
  }

  const ensureCoordinator = (row: SalesRfqResponse) => {
    if (autosave.current?.caseId !== row.caseId) selectRow(row)

    return autosave.current!.value
  }

  const completeEdit = async (field: keyof DraftDelta) => {
    setEditing(false)
    if (mode !== 'draft' || !selected) return
    const coordinator = ensureCoordinator(selected)
    const values = form.getFieldsValue(true) as RfqFormValues
    const delta = deltaFromFormField(field, values)
    const [attempted] = Object.values(delta)
    const effective = {
      ...coordinator.acknowledged(),
      ...coordinator.pending(),
    }[field]
    if (attempted === effective) return
    try {
      await coordinator.enqueue(delta)
    } catch (error) {
      const status = (error as ApiProblemDetails).status
      onError(
        status === 409
          ? 'This Draft was updated elsewhere. Your local input is preserved; review the latest state explicitly.'
          : 'The Draft could not be autosaved. Your local input is preserved.',
      )
      await onReconcileCases([selected.caseId]).catch(() => undefined)

      return
    }
    await onReconcileCases([selected.caseId]).catch(() => undefined)
  }

  return {
    form,
    mode,
    newIntent,
    defaultsLoading,
    protectedState: editing || saving,
    startNew,
    cancelNew: () => setNewIntent(false),
    selectRow,
    applyDefaults,
    beginEdit: () => {
      if (mode === 'draft') setEditing(true)
    },
    completeEdit,
    save,
    discard: async (row) => {
      if (await run(() => draft.discard(row.caseId, row.version)))
        setNewIntent(false)
    },
  }
}

function stateFromRow(row: SalesRfqResponse): DraftAutosaveState {
  return {
    version: row.version,
    notional: row.notional,
    settlementDate: row.settlementDate,
    standardSettlementDate: row.standardSettlementDate,
    salesAndTradingMessage: row.salesAndTradingMessage,
    assignedTraderId: row.assignedTraderId,
  }
}

function stateFromResponse(result: InitialRfqResponse): DraftAutosaveState {
  return {
    version: result.version,
    notional: result.notional,
    settlementDate: result.settlementDate,
    standardSettlementDate: result.standardSettlementDate,
    salesAndTradingMessage: result.salesAndTradingMessage,
    assignedTraderId: result.assignedTraderId,
  }
}

function deltaFromRequest(request: CreateDraftRequest): DraftDelta {
  return {
    notional: request.notional,
    settlementDate: request.settlementDate,
    standardSettlementDate: request.standardSettlementDate,
    salesAndTradingMessage: request.salesAndTradingMessage,
    assignedTraderId: request.assignedTraderId,
  }
}

function deltaFromFormField(
  field: keyof DraftDelta,
  values: RfqFormValues,
): Partial<DraftDelta> {
  const value =
    field === 'notional'
      ? values.notional === undefined
        ? undefined
        : values.notional * million
      : values[field]

  return { [field]: value }
}
