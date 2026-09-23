import type { CalculatedQuotePayload, TraderRfq } from '@/services/api'

export type TraderRefreshMode = 'live' | 'paused'
export type TraderPaneTab = 'operations' | 'pricer'
export type SearchDatePreset = '1M' | '3M' | '6M' | '1Y' | '2Y' | '5Y'
export type TraderBulkCommand =
  'pick' | 'release' | 'assign' | 'confirm' | 'withdraw' | 'away' | 'cancel'
export type CalcState =
  | { status: 'calculating'; requestId: number }
  | {
      status: 'failed'
      requestId: number
      code?: string
      message: string
      traceId?: string
      failureLogId?: string
    }

export interface PricerProvenance {
  sourceCaseId: number
  securityId: string
  notional: number | null
  settlementDate: string | null
}

export function traderState(row: TraderRfq) {
  if (row.rfqStatus === 'Presented') return 'Presented'
  if (row.rfqStatus === 'Cancelled') return 'Cancelled'
  if (row.rfqStatus === 'Hit') return 'Hit'
  if (row.rfqStatus === 'Away') return 'Away'
  if (row.quoteStatus === 'Quoted') return 'Quoted'
  const reasons: Record<string, string> = {
    Initial: 'Ini',
    Revised: 'Rev',
    Withdrawn: 'Wdr',
    Expired: 'Exp',
    Reopened: 'Reopen',
  }

  return row.quoteStatus === 'Requested'
    ? `Req/${reasons[row.quoteRequestReason ?? ''] ?? row.quoteRequestReason ?? '—'}`
    : row.rfqStatus
}

export function traderRouting(row: TraderRfq, currentUserId: string) {
  const trader =
    row.assignedTraderId === currentUserId ? 'Me' : row.assignedTraderId

  return `${trader} · ${row.owned ? 'Owned' : 'New'}`
}

export function attentionClass(row: TraderRfq, currentUserId: string) {
  if (row.assignedTraderId === currentUserId && !row.owned)
    return 'trader-row-attention-high'
  if (
    row.assignedTraderId === currentUserId &&
    row.owned &&
    row.quoteStatus === 'Requested'
  )
    return 'trader-row-attention-work'
  if (['Cancelled', 'Hit', 'Away'].includes(row.rfqStatus))
    return 'trader-row-terminal'

  return ''
}

export function canEditQuote(
  row: TraderRfq | undefined,
  currentUserId: string,
) {
  return Boolean(
    row &&
    row.owned &&
    row.assignedTraderId === currentUserId &&
    row.quoteStatus === 'Requested',
  )
}

export function isPickUpEligible(row: TraderRfq) {
  return !row.owned && ['Active', 'Presented'].includes(row.rfqStatus)
}

export function requiresPickUpConfirmation(
  row: TraderRfq,
  currentUserId: string,
) {
  return row.assignedTraderId !== currentUserId
}

export function isConfirmable(row: TraderRfq, currentUserId: string) {
  if (!canEditQuote(row, currentUserId)) return false

  return row.workingQuoteMode === 'Calculated'
    ? row.calculated !== null
    : row.manual?.price != null && row.manual.finalSimpleYield != null
}

export function elapsedLabel(stateSince: string, now = Date.now()) {
  const minutes = Math.floor(
    Math.max(0, now - new Date(stateSince).getTime()) / 60_000,
  )
  if (minutes < 60) return `${minutes}m`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ${minutes % 60}m`

  return `${Math.floor(hours / 24)}d ${hours % 24}h`
}

export function searchDateRange(date: string, preset: SearchDatePreset) {
  const to = new Date(`${date}T00:00:00Z`)
  const from = new Date(to)
  const count = Number(preset.slice(0, -1))
  if (preset.endsWith('M')) from.setUTCMonth(from.getUTCMonth() - count)
  else from.setUTCFullYear(from.getUTCFullYear() - count)

  return { createdFrom: from.toISOString().slice(0, 10), createdTo: date }
}

export function sameSourceTerms(
  row: TraderRfq,
  provenance: PricerProvenance | null,
) {
  return Boolean(
    provenance &&
    row.caseId === provenance.sourceCaseId &&
    row.securityId === provenance.securityId &&
    row.notional === provenance.notional &&
    row.settlementDate === provenance.settlementDate,
  )
}

export function calculatedValue(
  payload: CalculatedQuotePayload | null,
  driver: CalculatedQuotePayload['driver'],
) {
  if (!payload) return undefined
  const values: Record<CalculatedQuotePayload['driver'], number> = {
    Price: payload.price,
    BbgYield: payload.bbgYield,
    SimpleYield: payload.baseSimpleYield,
    Ysc: payload.ysc,
    GSpread: payload.gSpread,
    Asw: payload.asw,
    ISpread: payload.iSpread,
    ZSpread: payload.zSpread,
  }

  return values[driver]
}
