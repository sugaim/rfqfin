import { useState } from 'react'
import type { BulkItemResult, SalesRfq } from '@/services/api'
import {
  bulkEligibility,
  type SalesBulkCommand,
} from '@/pages/sales/salesModel'
import type { SalesBulkActions } from '@/pages/sales/salesContracts'
import type { SalesBulkResult } from '@/pages/sales/SalesBulkUi'

export interface SalesBulkSnapshotItem {
  row: SalesRfq
  eligible: boolean
  reason?: string
}

export interface SalesBulkDialog {
  command: SalesBulkCommand
  items: SalesBulkSnapshotItem[]
}

interface UseSalesBulkOperationsInput {
  selectedRows: SalesRfq[]
  currentUserId: string
  actions: SalesBulkActions
  onReconcileCases: (caseIds: number[]) => Promise<void>
  onError: (message: string | null) => void
}

export interface SalesBulkController {
  dialog: SalesBulkDialog | null
  result: SalesBulkResult | null
  resultExpanded: boolean
  open: (command: SalesBulkCommand) => void
  cancel: () => void
  apply: () => Promise<void>
  toggleResult: () => void
}

export function useSalesBulkOperations({
  selectedRows,
  currentUserId,
  actions,
  onReconcileCases,
  onError,
}: UseSalesBulkOperationsInput): SalesBulkController {
  const [dialog, setDialog] = useState<SalesBulkDialog | null>(null)
  const [result, setResult] = useState<SalesBulkResult | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)

  const open = (command: SalesBulkCommand) => {
    const items = selectedRows.map((row) => {
      const eligible = bulkEligibility(command, row, currentUserId)

      return {
        row,
        eligible,
        reason: eligible ? undefined : 'Not eligible in current state',
      }
    })
    if (items.some((item) => item.eligible)) setDialog({ command, items })
  }

  const apply = async () => {
    if (!dialog) return
    const snapshot = dialog
    setDialog(null)
    try {
      const eligible = snapshot.items
        .filter((item) => item.eligible)
        .map((item) => item.row)
      const results = await actions.execute(snapshot.command, eligible)
      const skipped: BulkItemResult[] = snapshot.items
        .filter((item) => !item.eligible)
        .map((item) => ({
          caseId: item.row.caseId,
          status: 'Skipped',
          code: 'InvalidState',
          message: item.reason ?? null,
        }))
      setResult({ command: snapshot.command, items: [...results, ...skipped] })
      setResultExpanded(false)
      await onReconcileCases(
        results
          .filter((item) => item.status === 'Succeeded')
          .map((item) => item.caseId),
      )
    } catch {
      onError('The bulk operation could not be completed.')
    }
  }

  return {
    dialog,
    result,
    resultExpanded,
    open,
    cancel: () => setDialog(null),
    apply,
    toggleResult: () => setResultExpanded((current) => !current),
  }
}
