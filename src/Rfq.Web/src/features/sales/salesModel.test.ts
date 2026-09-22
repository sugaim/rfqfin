import { describe, expect, it } from 'vitest'
import type { SalesRfq } from '../../services/api'
import {
  bulkEligibility, commandEligible, derivePaneMode, isTextEditingTarget,
  matchesSalesPreset, reconcileSelection, requiresConfirmation,
} from './salesModel'

const row = (overrides: Partial<SalesRfq> = {}): SalesRfq => ({
  caseId: 101,
  clientId: 'client-1',
  clientName: 'Client One',
  securityId: 'security-1',
  securityJapaneseName: 'Security One',
  securityBbgDisplay: 'SECURITY 1',
  categoryId: 'JGB',
  rfqStatus: 'Active',
  quoteStatus: 'Requested',
  quoteRequestReason: 'Initial',
  currentRevisionId: '00000000-0000-0000-0000-000000000101',
  currentQuoteId: null,
  closedQuoteId: null,
  currentVersion: 2,
  revisionStatus: 'Confirmed',
  salesId: 'sales-dev',
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  settlementDate: '2026-09-23',
  standardSettlementDate: '2026-09-23',
  notional: 100_000_000,
  salesAndTradingMessage: '',
  salesMemo: '',
  salesMemoVersion: 1,
  version: 2,
  createdAt: '2026-09-21T00:00:00Z',
  stateSince: '2026-09-21T00:05:00Z',
  confirmedQuote: null,
  ...overrides,
})

describe('Sales pane mode', () => {
  it('does not treat no selection as New and derives lifecycle modes from server state', () => {
    const rows = [row()]
    expect(derivePaneMode(rows, [], false)).toBe('neutral')
    expect(derivePaneMode(rows, [], true)).toBe('new')
    expect(derivePaneMode(rows, [101], false)).toBe('waiting')
    expect(derivePaneMode([row({ quoteStatus: 'Quoted' })], [101], false)).toBe('quoted')
    expect(derivePaneMode([row({ rfqStatus: 'Presented', quoteStatus: 'Quoted' })], [101], false)).toBe('presented')
    expect(derivePaneMode([row(), row({ caseId: 102 })], [101, 102], false)).toBe('bulk')
  })

  it('keeps stable Case selections across refresh and removes disappeared Cases', () => {
    expect(reconcileSelection([101, 102], [row(), row({ caseId: 103 })])).toEqual([101])
  })
})

describe('Sales preset filters', () => {
  it('keeps owner and originating Sales predicates independent', () => {
    const owned = row({ salesId: 'sales-a', contactOwnerId: 'sales-dev' })
    expect(matchesSalesPreset(owned, 'owner', 'sales-dev')).toBe(true)
    expect(matchesSalesPreset(owned, 'sales', 'sales-dev')).toBe(false)
    expect(matchesSalesPreset(owned, 'owner-and-sales', 'sales-dev')).toBe(false)
    expect(matchesSalesPreset(owned, 'owner-or-sales', 'sales-dev')).toBe(true)
  })
})

describe('Sales command eligibility', () => {
  it('supports quoted single actions and deliberately exposes no bulk Hit command', () => {
    const quoted = row({ quoteStatus: 'Quoted' })
    expect(commandEligible('present', quoted, 'sales-dev')).toBe(true)
    expect(commandEligible('hit', quoted, 'sales-dev')).toBe(true)
    expect(bulkEligibility('away', quoted, 'sales-dev')).toBe(true)
    const bulkCommands = ['away', 'cancel', 'present', 'unpresent', 'confirm-drafts',
      'discard-drafts', 'confirm-amendments', 'discard-amendments']
    expect(bulkCommands).not.toContain('hit')
  })

  it('keeps Work Pane actions immediate and confirms dangerous context/shortcut actions', () => {
    expect(requiresConfirmation('work-pane', 'hit')).toBe(false)
    expect(requiresConfirmation('context-menu', 'present')).toBe(false)
    expect(requiresConfirmation('context-menu', 'hit')).toBe(true)
    expect(requiresConfirmation('shortcut', 'away')).toBe(true)
    expect(requiresConfirmation('shortcut', 'cancel')).toBe(true)
  })

  it('recognizes typing surfaces so character shortcuts remain safe', () => {
    const input = document.createElement('input')
    const editable = document.createElement('div')
    editable.contentEditable = 'true'
    expect(isTextEditingTarget(input)).toBe(true)
    expect(isTextEditingTarget(editable)).toBe(true)
    expect(isTextEditingTarget(document.body)).toBe(false)
  })
})
