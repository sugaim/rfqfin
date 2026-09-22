import type { TraderRfq } from '@/services/api'
import type { TraderBulkCommand } from '@/features/trader/traderModel'

export type TraderOperationRunner = <T>(
  action: () => Promise<T | void>,
  patch?: (row: TraderRfq, value: T) => TraderRfq,
  row?: TraderRfq,
) => Promise<T | void>

export type TraderBulkRunner = (
  label: string,
  command: TraderBulkCommand,
  rows: TraderRfq[],
  trader?: string,
) => Promise<void>

export interface OwnershipOperationIntents {
  pickUp: () => void
  release: () => void
  assign: (targetTraderId: string) => void
  takeOver: () => void
}

export interface QuoteOperationIntents {
  changeMode: (mode: 'Calculated' | 'Manual') => void
  withdraw?: () => void
}

export interface LifecycleOperationIntents {
  present?: () => void
  unpresent?: () => void
  close: (outcome: 'Hit' | 'Away') => void
  cancel?: () => void
  reopen?: () => void
  correctOutcome: (outcome: 'Hit' | 'Away', reason: string) => void
}

export interface ContactOwnerOperationIntents {
  change: (targetUserId: string) => void
}

export interface BulkOperationIntents {
  pick: () => void
  release: () => void
  assign: (targetTraderId: string) => void
  withdraw: () => void
  closeAway: () => void
  cancel: () => void
}
