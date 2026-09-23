import { describe, expect, it } from 'vitest'
import type { PersistedEvent } from '@/services/api'
import {
  hasRelevantRecentRevisionChange,
  shouldLoadRecentRevisions,
} from '@/pages/sales/recentRevisions'

const event = (eventId: number, type: string): PersistedEvent => ({
  eventId,
  type,
  occurredAt: '2026-09-23T00:00:00Z',
  actorUserId: 'sales-dev',
  caseId: 101,
})

describe('Recent Revisions loading policy', () => {
  it('does not load while closed or during an existing request', () => {
    expect(shouldLoadRecentRevisions(false, false, 2, 0)).toBe(false)
    expect(shouldLoadRecentRevisions(true, true, 2, 0)).toBe(false)
  })

  it('loads when opened and stale or not yet loaded', () => {
    expect(shouldLoadRecentRevisions(true, false, 1, 0)).toBe(true)
    expect(shouldLoadRecentRevisions(true, false, 2, 1)).toBe(true)
    expect(shouldLoadRecentRevisions(true, false, 2, 2)).toBe(false)
  })

  it('marks only unseen revision and quote confirmation events stale', () => {
    const events = [
      event(1, 'rfqCreated'),
      event(2, 'rfqRevisionConfirmed'),
      event(3, 'quoteConfirmed'),
    ]
    expect(hasRelevantRecentRevisionChange(events, 0)).toBe(true)
    expect(hasRelevantRecentRevisionChange(events, 2)).toBe(true)
    expect(hasRelevantRecentRevisionChange(events, 3)).toBe(false)
    expect(hasRelevantRecentRevisionChange([event(4, 'rfqCancelled')], 3)).toBe(
      false,
    )
  })
})
