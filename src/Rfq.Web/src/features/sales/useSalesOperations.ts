import { useState } from 'react'
import type { SalesRfq } from '@/services/api'
import {
  commandEligible,
  type SalesCommand,
  type SalesRefreshMode,
  type SalesRowCommand,
} from '@/features/sales/salesModel'
import type {
  SalesAmendmentActions,
  SalesDraftActions,
  SalesLifecycleActions,
} from '@/features/sales/salesContracts'

export interface SalesSingleDialog {
  command: SalesCommand
  row: SalesRfq
}

interface UseSalesOperationsInput {
  currentUserId: string
  refreshMode: SalesRefreshMode
  draft: SalesDraftActions
  lifecycle: SalesLifecycleActions
  amendment: SalesAmendmentActions
  onReload: () => void | Promise<unknown>
  onRowActionApplied: (command: SalesRowCommand, row: SalesRfq) => void
  onSuccess: () => void
}

export interface SalesOperationsController {
  actionError: string | null
  conflict: boolean
  singleDialog: SalesSingleDialog | null
  setActionError: (message: string | null) => void
  clearDialog: () => void
  run: (action: () => Promise<void>, reload?: boolean) => Promise<boolean>
  execute: (command: SalesCommand, row: SalesRfq) => Promise<void>
  executeRow: (command: SalesRowCommand, row: SalesRfq) => Promise<void>
  request: (command: SalesCommand, row: SalesRfq, confirm: boolean) => void
}

export function useSalesOperations({
  currentUserId,
  refreshMode,
  draft,
  lifecycle,
  amendment,
  onReload,
  onRowActionApplied,
  onSuccess,
}: UseSalesOperationsInput): SalesOperationsController {
  const [actionError, setActionError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)
  const [singleDialog, setSingleDialog] = useState<SalesSingleDialog | null>(
    null,
  )
  const updateError = (message: string | null) => {
    setActionError(message)
    if (message === null) setConflict(false)
  }

  const run = async (action: () => Promise<void>, reload = true) => {
    updateError(null)
    setConflict(false)
    try {
      await action()
      onSuccess()
      if (reload) await onReload()

      return true
    } catch (error) {
      const status = (error as { status?: number })?.status
      setConflict(status === 409)
      setActionError(
        status === 409
          ? 'This RFQ was updated elsewhere. Your local input is preserved; review and reload explicitly.'
          : 'The RFQ action could not be completed.',
      )

      return false
    }
  }

  const execute = async (command: SalesCommand, row: SalesRfq) => {
    const succeeded = await run(async () => {
      switch (command) {
        case 'present':
          return lifecycle.present(row.caseId, row.currentVersion)
        case 'unpresent':
          return lifecycle.unpresent(row.caseId, row.currentVersion)
        case 'hit':
          return lifecycle.close(row.caseId, 'Hit', row.currentVersion)
        case 'away':
          return lifecycle.close(row.caseId, 'Away', row.currentVersion)
        case 'cancel':
          return lifecycle.cancel(row)
        case 'reopen':
          return lifecycle.reopen(row)
        case 'confirm-amendment':
          return amendment.confirm(row)
        case 'discard-amendment':
          return amendment.discard(row)
        case 'create-from-existing':
          return lifecycle.createFromExisting(row.caseId)
      }
    }, refreshMode === 'live')
    if (succeeded && refreshMode === 'paused') onRowActionApplied(command, row)
  }

  const executeRow = async (command: SalesRowCommand, row: SalesRfq) => {
    if (refreshMode === 'live') return
    if (command === 'confirm-draft') {
      const { assignedTraderId, settlementDate, notional } = row
      if (!assignedTraderId || !settlementDate || notional === null) return
      const succeeded = await run(
        () =>
          draft.confirm(row.caseId, {
            notional,
            settlementDate,
            standardSettlementDate: row.standardSettlementDate,
            salesAndTradingMessage: row.salesAndTradingMessage,
            assignedTraderId,
            expectedVersion: row.version,
          }),
        false,
      )
      if (succeeded) onRowActionApplied(command, row)
    } else if (command === 'discard-draft') {
      const succeeded = await run(
        () => draft.discard(row.caseId, row.version),
        false,
      )
      if (succeeded) onRowActionApplied(command, row)
    } else {
      await execute(command, row)
    }
  }

  const request = (command: SalesCommand, row: SalesRfq, confirm: boolean) => {
    if (!commandEligible(command, row, currentUserId)) return
    if (confirm) setSingleDialog({ command, row })
    else void execute(command, row)
  }

  return {
    actionError,
    conflict,
    singleDialog,
    setActionError: updateError,
    clearDialog: () => setSingleDialog(null),
    run,
    execute,
    executeRow,
    request,
  }
}
