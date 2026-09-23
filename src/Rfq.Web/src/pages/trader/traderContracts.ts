import type {
  BulkItemResult,
  CalculatedQuotePayload,
  CloseRfqResult,
  ConfirmQuoteResult,
  ContactOwnerResult,
  LifecycleResult,
  MemoResult,
  OwnershipResult,
  PresentationResult,
  RfqSearchParams,
  RfqSearchResult,
  TraderRfq,
  WorkingQuoteResult,
} from '@/services/api'
import type { TraderBulkCommand } from '@/pages/trader/traderModel'

export interface UserOption {
  userId: string
  name: string
}

export type RfqOutcome = 'Hit' | 'Away'
export type TraderGridConfigKey = 'main' | 'search' | 'confirm'

export interface TraderOwnershipActions {
  pickUp: (
    row: TraderRfq,
    confirmed: boolean,
  ) => Promise<OwnershipResult | void>
  release: (row: TraderRfq) => Promise<OwnershipResult | void>
  assign: (
    row: TraderRfq,
    targetTraderId: string,
  ) => Promise<OwnershipResult | void>
  takeOver: (row: TraderRfq) => Promise<OwnershipResult | void>
}

export interface TraderWorkingQuoteActions {
  calculate: (
    row: TraderRfq,
    driver: CalculatedQuotePayload['driver'],
    value: number,
    simpleYieldSlide: number,
  ) => Promise<WorkingQuoteResult | void>
  changeMode: (
    row: TraderRfq,
    mode: 'Calculated' | 'Manual',
  ) => Promise<WorkingQuoteResult | void>
  updateManual: (
    row: TraderRfq,
    price: number | null,
    finalSimpleYield: number | null,
  ) => Promise<WorkingQuoteResult | void>
  confirm: (
    row: TraderRfq,
    expiryMinutes: number | null,
  ) => Promise<ConfirmQuoteResult | void>
}

export interface TraderLifecycleActions {
  present?: (row: TraderRfq) => Promise<PresentationResult | void>
  unpresent?: (row: TraderRfq) => Promise<PresentationResult | void>
  withdraw?: (row: TraderRfq) => Promise<LifecycleResult | void>
  close: (row: TraderRfq, outcome: RfqOutcome) => Promise<CloseRfqResult | void>
  cancel?: (row: TraderRfq) => Promise<LifecycleResult | void>
  reopen?: (row: TraderRfq) => Promise<LifecycleResult | void>
  correctOutcome: (
    row: TraderRfq,
    outcome: RfqOutcome,
    reason: string,
  ) => Promise<CloseRfqResult | void>
}

export interface TraderContactOwnerActions {
  change: (
    row: TraderRfq,
    targetUserId: string,
  ) => Promise<ContactOwnerResult | void>
}

export interface TraderMemoActions {
  update: (row: TraderRfq, memo: string) => Promise<MemoResult | void>
}

export interface TraderBulkActions {
  execute: (
    command: TraderBulkCommand,
    rows: TraderRfq[],
    expiryMinutes: number | null,
    targetTraderId?: string,
  ) => Promise<BulkItemResult[]>
}

export interface TraderSearchActions {
  execute: (params: RfqSearchParams) => Promise<RfqSearchResult>
}

export interface TraderPricerActions {
  calculate: (input: {
    securityId: string
    settlementDate: string
    driver: CalculatedQuotePayload['driver']
    value: number
    simpleYieldSlide: number
  }) => Promise<CalculatedQuotePayload>
}

export interface TraderGridLayoutActions {
  configs: Partial<Record<TraderGridConfigKey, string>>
  save?: (key: TraderGridConfigKey, configJson: string) => Promise<void>
}
