import { describe, expect, it } from 'vitest'
import { shouldLoadRecentRevisions } from '@/pages/sales/recentRevisions'

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
})
