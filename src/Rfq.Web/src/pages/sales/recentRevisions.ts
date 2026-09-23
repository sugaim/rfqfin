import type { PersistedEvent } from '@/services/api'

const relevantEventTypes = new Set(['rfqRevisionConfirmed', 'quoteConfirmed'])

export function hasRelevantRecentRevisionChange(
  events: PersistedEvent[],
  afterEventId: number,
): boolean {
  return events.some(
    (event) =>
      event.eventId > afterEventId && relevantEventTypes.has(event.type),
  )
}

export function shouldLoadRecentRevisions(
  open: boolean,
  fetching: boolean,
  generation: number,
  loadedGeneration: number,
): boolean {
  return open && !fetching && generation > loadedGeneration
}
