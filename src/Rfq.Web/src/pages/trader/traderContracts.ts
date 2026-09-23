import type {
  CaseOperationResponse,
  CalculatedQuoteResponse2,
  CloseResponse,
  SearchRfqsApiArg,
  RfqSearchResponse,
  TraderRfqResponse,
  TraderMemoResponse,
  WorkingQuoteResponse,
} from '@/generated/rfqApi'
import type { TraderBulkCommand } from '@/pages/trader/traderModel'

export interface UserOption {
  userId: string
  name: string
}

export type RfqOutcome = 'Hit' | 'Away'
export type TraderGridConfigKey = 'main' | 'search' | 'confirm'

export interface TraderOwnershipActions {
  pickUp: (row: TraderRfqResponse, confirmed: boolean) => Promise<void>
  release: (row: TraderRfqResponse) => Promise<void>
  assign: (row: TraderRfqResponse, targetTraderId: string) => Promise<void>
  takeOver: (row: TraderRfqResponse) => Promise<void>
}

export interface TraderWorkingQuoteActions {
  calculate: (
    row: TraderRfqResponse,
    driver: CalculatedQuoteResponse2['driver'],
    value: number,
    simpleYieldSlide: number,
  ) => Promise<WorkingQuoteResponse | void>
  changeMode: (
    row: TraderRfqResponse,
    mode: 'Calculated' | 'Manual',
  ) => Promise<WorkingQuoteResponse | void>
  updateManual: (
    row: TraderRfqResponse,
    price: number | null,
    finalSimpleYield: number | null,
  ) => Promise<WorkingQuoteResponse | void>
  confirm: (
    row: TraderRfqResponse,
    expiryMinutes: number | null,
  ) => Promise<CaseOperationResponse[] | void>
}

export interface TraderLifecycleActions {
  present?: (row: TraderRfqResponse) => Promise<void>
  unpresent?: (row: TraderRfqResponse) => Promise<void>
  withdraw?: (row: TraderRfqResponse) => Promise<void>
  close: (
    row: TraderRfqResponse,
    outcome: RfqOutcome,
  ) => Promise<CloseResponse | void>
  cancel?: (row: TraderRfqResponse) => Promise<void>
  reopen?: (row: TraderRfqResponse) => Promise<void>
  correctOutcome: (
    row: TraderRfqResponse,
    outcome: RfqOutcome,
    reason: string,
  ) => Promise<CloseResponse | void>
}

export interface TraderContactOwnerActions {
  change: (row: TraderRfqResponse, targetUserId: string) => Promise<void>
}

export interface TraderMemoActions {
  update: (
    row: TraderRfqResponse,
    memo: string,
  ) => Promise<TraderMemoResponse | void>
}

export interface TraderBulkActions {
  execute: (
    command: TraderBulkCommand,
    rows: TraderRfqResponse[],
    expiryMinutes: number | null,
    targetTraderId?: string,
  ) => Promise<CaseOperationResponse[]>
}

export interface TraderSearchActions {
  execute: (params: SearchRfqsApiArg) => Promise<RfqSearchResponse>
}

export interface TraderPricerActions {
  calculate: (input: {
    securityId: string
    settlementDate: string
    driver: CalculatedQuoteResponse2['driver']
    value: number
    simpleYieldSlide: number
  }) => Promise<CalculatedQuoteResponse2>
}

export interface TraderGridLayoutActions {
  configs: Partial<Record<TraderGridConfigKey, string>>
  save?: (key: TraderGridConfigKey, configJson: string) => Promise<void>
}
