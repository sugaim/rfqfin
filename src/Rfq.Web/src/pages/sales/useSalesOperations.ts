import { useState } from 'react'
import type { ApiProblemDetails, SalesRfqResponse } from '@/generated/rfqApi'
import {
  commandEligible,
  type SalesCommand,
  type SalesRefreshMode,
  type SalesRowCommand,
} from '@/pages/sales/salesModel'
import type {
  SalesAmendmentActions,
  SalesDraftActions,
  SalesLifecycleActions,
} from '@/pages/sales/salesContracts'

export interface SalesSingleDialog {
  command: SalesCommand
  row: SalesRfqResponse
}

interface UseSalesOperationsInput {
  currentUserId: string
  refreshMode: SalesRefreshMode
  draft: SalesDraftActions
  lifecycle: SalesLifecycleActions
  amendment: SalesAmendmentActions
  onReconcileCases: (caseIds: number[]) => Promise<void>
  onSuccess: () => void
}

export interface SalesOperationsController {
  actionError: string | null
  conflict: boolean
  singleDialog: SalesSingleDialog | null
  setActionError: (message: string | null) => void
  clearDialog: () => void
  run: (action: () => Promise<unknown>, caseIds?: number[]) => Promise<boolean>
  execute: (command: SalesCommand, row: SalesRfqResponse) => Promise<void>
  executeRow: (command: SalesRowCommand, row: SalesRfqResponse) => Promise<void>
  request: (
    command: SalesCommand,
    row: SalesRfqResponse,
    confirm: boolean,
  ) => void
}

export function useSalesOperations({
  currentUserId,
  refreshMode,
  draft,
  lifecycle,
  amendment,
  onReconcileCases,
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

  const run = async (
    action: () => Promise<unknown>,
    caseIds: number[] = [],
  ) => {
    updateError(null)
    setConflict(false)
    try {
      await action()
    } catch (error) {
      const status = (error as ApiProblemDetails).status
      setConflict(status === 409)
      setActionError(
        status === 409
          ? 'This RFQ was updated elsewhere. Your local input is preserved; review and reload explicitly.'
          : 'The RFQ action could not be completed.',
      )

      return false
    }
    onSuccess()
    if (caseIds.length) await onReconcileCases(caseIds).catch(() => undefined)

    return true
  }

  const execute = async (command: SalesCommand, row: SalesRfqResponse) => {
    if (command === 'create-from-existing') {
      let createdCaseId: number | undefined
      const succeeded = await run(async () => {
        const created = await lifecycle.createFromExisting(row.caseId)
        createdCaseId = created.caseId
      })
      if (succeeded && createdCaseId !== undefined)
        await onReconcileCases([createdCaseId]).catch(() => undefined)

      return
    }
    await run(async () => {
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
      }
    }, [row.caseId])
  }

  const executeRow = async (
    command: SalesRowCommand,
    row: SalesRfqResponse,
  ) => {
    if (refreshMode === 'live') return
    if (command === 'confirm-draft') {
      const { assignedTraderId, settlementDate, notional } = row
      if (!assignedTraderId || !settlementDate || notional === null) return
      await run(
        () =>
          draft.confirm(row.caseId, {
            notional,
            settlementDate,
            standardSettlementDate: row.standardSettlementDate,
            salesAndTradingMessage: row.salesAndTradingMessage,
            assignedTraderId,
            expectedCurrentVersion: row.version,
          }),
        [row.caseId],
      )
    } else if (command === 'discard-draft') {
      await run(() => draft.discard(row.caseId, row.version), [row.caseId])
    } else {
      await execute(command, row)
    }
  }

  const request = (
    command: SalesCommand,
    row: SalesRfqResponse,
    confirm: boolean,
  ) => {
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
