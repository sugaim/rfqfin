import type { ApiProblemDetails } from '@/generated/rfqApi'

function isApiProblemDetails(value: unknown): value is ApiProblemDetails {
  if (typeof value !== 'object' || value === null) return false

  const candidate = value as Partial<ApiProblemDetails>

  return (
    typeof candidate.status === 'number' &&
    typeof candidate.title === 'string' &&
    typeof candidate.detail === 'string' &&
    typeof candidate.code === 'string' &&
    typeof candidate.traceId === 'string'
  )
}

export function normalizeApiProblem(error: unknown): ApiProblemDetails {
  if (isApiProblemDetails(error)) return error

  if (typeof error === 'object' && error !== null && 'data' in error) {
    const data = (error as { data: unknown }).data
    if (isApiProblemDetails(data)) return data
  }

  return {
    status: 0,
    title: 'Transport failure',
    detail: 'The request could not be completed.',
    code: 'TransportFailure',
    traceId: '',
  }
}

export async function unwrapApiResult<T>(result: {
  unwrap: () => Promise<T>
}): Promise<T> {
  try {
    return await result.unwrap()
  } catch (error) {
    throw normalizeApiProblem(error)
  }
}
