export type WorklistInvalidationCategory =
  'sales-list' | 'trader-list' | 'recent-revisions' | 'business-date'

const categories = new Set<WorklistInvalidationCategory>([
  'sales-list',
  'trader-list',
  'recent-revisions',
  'business-date',
])

function isWorklistInvalidationCategory(
  value: string,
): value is WorklistInvalidationCategory {
  return categories.has(value as WorklistInvalidationCategory)
}

export function worklistStreamUrl(developmentUser: string): string {
  return `/api/worklists/stream?developmentUser=${encodeURIComponent(developmentUser)}`
}

export function parseWorklistInvalidation(
  payload: string,
): WorklistInvalidationCategory[] {
  return [...new Set(payload.split(',').filter(isWorklistInvalidationCategory))]
}

export function connectWorklistStream(
  developmentUser: string,
  onInvalidation: (categories: WorklistInvalidationCategory[]) => void,
): () => void {
  if (typeof EventSource === 'undefined') return () => undefined

  const source = new EventSource(worklistStreamUrl(developmentUser))
  source.addEventListener('invalidation', (event) => {
    onInvalidation(
      parseWorklistInvalidation((event as MessageEvent<string>).data),
    )
  })

  return () => source.close()
}
