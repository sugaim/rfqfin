export function shouldLoadRecentRevisions(
  open: boolean,
  fetching: boolean,
  generation: number,
  loadedGeneration: number,
): boolean {
  return open && !fetching && generation > loadedGeneration
}
