import { describe, expect, it } from 'vitest'
import type { TraderRfq } from '@/services/api'
import {
  attentionClass,
  isPickUpEligible,
  isConfirmable,
  requiresPickUpConfirmation,
  sameSourceTerms,
  searchDateRange,
  traderState,
} from '@/features/trader/traderModel'

const row = (overrides: Partial<TraderRfq> = {}): TraderRfq => ({
  caseId: 1,
  clientId: 'c',
  clientName: 'Client',
  securityId: 's',
  securityJapaneseName: 'Security',
  securityBbgDisplay: 'SEC',
  categoryId: 'JGB',
  rfqStatus: 'Active',
  quoteStatus: 'Requested',
  quoteRequestReason: 'Initial',
  currentRevisionId: 'r',
  currentQuoteId: null,
  closedQuoteId: null,
  confirmedAt: null,
  expiresAt: null,
  quoteSeedRevisionId: null,
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  owned: false,
  currentVersion: 1,
  settlementDate: '2026-09-23',
  notional: 100_000_000,
  salesAndTradingMessage: 'message',
  workingQuoteMode: 'Calculated',
  calculated: null,
  manual: null,
  workingQuoteVersion: 1,
  traderMemo: '',
  traderMemoVersion: 1,
  createdAt: '2026-09-21T00:00:00Z',
  stateSince: '2026-09-21T00:00:00Z',
  ...overrides,
})

describe('Trader presentation model', () => {
  it('keeps attention independent from calm lifecycle states', () => {
    expect(attentionClass(row(), 'trader-a')).toBe('trader-row-attention-high')
    expect(attentionClass(row({ owned: true }), 'trader-a')).toBe(
      'trader-row-attention-work',
    )
    expect(
      attentionClass(row({ owned: true, quoteStatus: 'Quoted' }), 'trader-a'),
    ).toBe('')
    expect(traderState(row({ quoteRequestReason: 'Revised' }))).toBe('Req/Rev')
  })

  it('requires an owned valid Working Quote for confirmation', () => {
    expect(isConfirmable(row({ owned: true }), 'trader-a')).toBe(false)
    expect(
      isConfirmable(
        row({
          owned: true,
          calculated: {
            driver: 'Price',
            driverValue: 100,
            price: 100,
            bbgYield: 1,
            baseSimpleYield: 1,
            simpleYieldSlide: 0,
            finalSimpleYield: 1,
            internalYield: 1,
            ysc: 1,
            gSpread: 1,
            asw: 1,
            iSpread: 1,
            zSpread: 1,
          },
        }),
        'trader-a',
      ),
    ).toBe(true)
  })

  it('classifies Pick Up eligibility and confirmation by assignment', () => {
    expect(isPickUpEligible(row())).toBe(true)
    expect(requiresPickUpConfirmation(row(), 'trader-a')).toBe(false)
    expect(
      requiresPickUpConfirmation(
        row({ assignedTraderId: 'trader-b' }),
        'trader-a',
      ),
    ).toBe(true)
    expect(isPickUpEligible(row({ owned: true }))).toBe(false)
    expect(isPickUpEligible(row({ rfqStatus: 'Hit' }))).toBe(false)
  })

  it('builds preset-only desk date ranges and validates Pricer provenance', () => {
    expect(searchDateRange('2026-09-22', '1Y')).toEqual({
      createdFrom: '2025-09-22',
      createdTo: '2026-09-22',
    })
    expect(
      sameSourceTerms(row(), {
        sourceCaseId: 1,
        securityId: 's',
        notional: 100_000_000,
        settlementDate: '2026-09-23',
      }),
    ).toBe(true)
    expect(
      sameSourceTerms(row({ notional: 50 }), {
        sourceCaseId: 1,
        securityId: 's',
        notional: 100_000_000,
        settlementDate: '2026-09-23',
      }),
    ).toBe(false)
  })
})
