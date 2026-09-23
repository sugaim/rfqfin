import { useState } from 'react'
import type { TraderRfq } from '@/services/api'
import {
  isPickUpEligible,
  requiresPickUpConfirmation,
} from '@/pages/trader/traderModel'
import type {
  TraderContactOwnerActions,
  TraderLifecycleActions,
  TraderOwnershipActions,
  TraderWorkingQuoteActions,
} from '@/pages/trader/traderContracts'
import type {
  BulkOperationIntents,
  ContactOwnerOperationIntents,
  LifecycleOperationIntents,
  OwnershipOperationIntents,
  QuoteOperationIntents,
  TraderBulkRunner,
  TraderOperationRunner,
} from '@/pages/trader/operations/operationTypes'

interface UseTraderOperationIntentsInput {
  selected?: TraderRfq
  selectedRows: TraderRfq[]
  currentUserId: string
  run: TraderOperationRunner
  runBulk: TraderBulkRunner
  ownership: TraderOwnershipActions
  workingQuote: TraderWorkingQuoteActions
  lifecycle: TraderLifecycleActions
  contactOwner: TraderContactOwnerActions
}

export interface TraderOperationController {
  targetTraderId?: string
  targetContactOwnerId?: string
  setTargetTraderId: (value?: string) => void
  setTargetContactOwnerId: (value?: string) => void
  clearTargets: () => void
  ownership: OwnershipOperationIntents
  quote: QuoteOperationIntents
  lifecycle: LifecycleOperationIntents
  contactOwner: ContactOwnerOperationIntents
  bulk: BulkOperationIntents
}

export function useTraderOperationIntents({
  selected,
  selectedRows,
  currentUserId,
  run,
  runBulk,
  ownership,
  workingQuote,
  lifecycle,
  contactOwner,
}: UseTraderOperationIntentsInput): TraderOperationController {
  const [targetTraderId, setTargetTraderId] = useState<string>()
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const withSelected = (intent: (row: TraderRfq) => void) => {
    if (selected) intent(selected)
  }
  const pickRows = selectedRows.filter(isPickUpEligible)
  const withdraw = lifecycle.withdraw
  const present = lifecycle.present
  const unpresent = lifecycle.unpresent
  const cancel = lifecycle.cancel
  const reopen = lifecycle.reopen

  return {
    targetTraderId,
    targetContactOwnerId,
    setTargetTraderId,
    setTargetContactOwnerId,
    clearTargets: () => {
      setTargetTraderId(undefined)
      setTargetContactOwnerId(undefined)
    },
    ownership: {
      pickUp: () =>
        withSelected(
          (row) =>
            void run(
              () =>
                ownership.pickUp(
                  row,
                  requiresPickUpConfirmation(row, currentUserId),
                ),
              row,
            ),
        ),
      release: () =>
        withSelected((row) => void run(() => ownership.release(row), row)),
      assign: (traderId) =>
        withSelected(
          (row) => void run(() => ownership.assign(row, traderId), row),
        ),
      takeOver: () =>
        withSelected((row) => void run(() => ownership.takeOver(row), row)),
    },
    quote: {
      changeMode: (mode) =>
        withSelected(
          (row) => void run(() => workingQuote.changeMode(row, mode), row),
        ),
      withdraw: withdraw
        ? () => withSelected((row) => void run(() => withdraw(row), row))
        : undefined,
    },
    lifecycle: {
      present: present
        ? () => withSelected((row) => void run(() => present(row), row))
        : undefined,
      unpresent: unpresent
        ? () => withSelected((row) => void run(() => unpresent(row), row))
        : undefined,
      close: (outcome) =>
        withSelected(
          (row) => void run(() => lifecycle.close(row, outcome), row),
        ),
      cancel: cancel
        ? () => withSelected((row) => void run(() => cancel(row), row))
        : undefined,
      reopen: reopen
        ? () => withSelected((row) => void run(() => reopen(row), row))
        : undefined,
      correctOutcome: (outcome, reason) =>
        withSelected(
          (row) =>
            void run(() => lifecycle.correctOutcome(row, outcome, reason), row),
        ),
    },
    contactOwner: {
      change: (userId) =>
        withSelected(
          (row) => void run(() => contactOwner.change(row, userId), row),
        ),
    },
    bulk: {
      pick: () => void runBulk('Bulk Pick', 'pick', pickRows),
      release: () => void runBulk('Bulk Release', 'release', selectedRows),
      assign: (traderId) =>
        void runBulk('Bulk Assign', 'assign', selectedRows, traderId),
      withdraw: () => void runBulk('Bulk Withdraw', 'withdraw', selectedRows),
      closeAway: () => void runBulk('Bulk Away', 'away', selectedRows),
      cancel: () => void runBulk('Bulk Cancel', 'cancel', selectedRows),
    },
  }
}
