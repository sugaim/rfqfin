import { configureStore } from '@reduxjs/toolkit'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { rfqApi, type ApiProblemDetails } from '@/generated/rfqApi'
import { baseApi } from '@/services/baseApi'
import { normalizeApiProblem, unwrapApiResult } from '@/services/apiProblem'
import {
  connectWorklistStream,
  worklistStreamUrl,
  type WorklistInvalidationCategory,
} from '@/services/worklistStream'

function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function useAbsoluteRequests() {
  const NativeRequest = Request
  class AbsoluteRequest extends NativeRequest {
    constructor(input: RequestInfo | URL, init?: RequestInit) {
      super(
        typeof input === 'string' && input.startsWith('/')
          ? new URL(input, 'http://localhost')
          : input,
        init,
      )
    }
  }
  vi.stubGlobal('Request', AbsoluteRequest)
}

function createStore() {
  return configureStore({
    reducer: { [baseApi.reducerPath]: baseApi.reducer },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().concat(baseApi.middleware),
  })
}

describe('generated API transport boundary', () => {
  afterEach(() => {
    window.localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('sends the configured development identity from baseApi', async () => {
    useAbsoluteRequests()
    window.localStorage.setItem('rfq-development-user', 'sales-a')
    let identity: string | null = null
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(input, init)
        identity = request.headers.get('X-Development-User')

        return json({ status: 'ok' })
      }),
    )
    const store = createStore()

    await store.dispatch(rfqApi.endpoints.getHealth.initiate()).unwrap()

    expect(identity).toBe('sales-a')
    store.dispatch(baseApi.util.resetApiState())
  })

  it('uses the generated query contract without duplicating the /api prefix', async () => {
    useAbsoluteRequests()
    let capturedUrl = ''
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(input, init)
        capturedUrl = request.url

        return json([])
      }),
    )
    const store = createStore()

    await store
      .dispatch(
        rfqApi.endpoints.getPostProcess.initiate({
          preset: 'Today',
          scope: 'Mine',
        }),
      )
      .unwrap()

    const url = new URL(capturedUrl)
    expect(url.pathname).toBe('/api/post-process')
    expect(Object.fromEntries(url.searchParams)).toEqual({
      preset: 'Today',
      scope: 'Mine',
    })
    store.dispatch(baseApi.util.resetApiState())
  })

  it('uses the generated plural mutation contract and preserves Failed business results', async () => {
    useAbsoluteRequests()
    let captured: { url: string; body: unknown } | undefined
    const failedResult = {
      caseId: 101,
      status: 'Failed' as const,
      failureCode: 'VersionConflict' as const,
      message: 'The RFQ changed.',
    }
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(input, init)
        captured = {
          url: new URL(request.url).pathname,
          body: await request.clone().json(),
        }

        return json([failedResult])
      }),
    )
    const store = createStore()

    const result = await store
      .dispatch(
        rfqApi.endpoints.presentRfqs.initiate({
          presentRfqsRequest: {
            items: [{ caseId: 101, expectedCurrentVersion: 7 }],
          },
        }),
      )
      .unwrap()

    expect(captured).toEqual({
      url: '/api/rfqs/present',
      body: { items: [{ caseId: 101, expectedCurrentVersion: 7 }] },
    })
    expect(result).toEqual([failedResult])
    store.dispatch(baseApi.util.resetApiState())
  })

  it('normalizes server problems and hides unknown RTK transport envelopes', () => {
    const problem: ApiProblemDetails = {
      status: 409,
      title: 'Conflict',
      detail: 'The RFQ changed.',
      code: 'VersionConflict',
      traceId: 'trace-1',
      calculationErrorCode: null,
      failureLogId: null,
    }

    expect(normalizeApiProblem({ status: 409, data: problem })).toBe(problem)
    expect(
      normalizeApiProblem({ status: 'FETCH_ERROR', error: 'offline' }),
    ).toEqual({
      status: 0,
      title: 'Transport failure',
      detail: 'The request could not be completed.',
      code: 'TransportFailure',
      traceId: '',
    })
  })

  it('rejects capabilities with normalized problems rather than RTK envelopes', async () => {
    const problem: ApiProblemDetails = {
      status: 409,
      title: 'Conflict',
      detail: 'The RFQ changed.',
      code: 'VersionConflict',
      traceId: 'trace-2',
      calculationErrorCode: null,
      failureLogId: null,
    }

    await expect(
      unwrapApiResult({
        unwrap: () => Promise.reject({ status: 409, data: problem }),
      }),
    ).rejects.toBe(problem)
    await expect(
      unwrapApiResult({
        unwrap: () =>
          Promise.reject({ status: 'FETCH_ERROR', error: 'offline' }),
      }),
    ).rejects.toEqual(
      expect.objectContaining({ status: 0, code: 'TransportFailure' }),
    )
  })
})

describe('worklist stream transport', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('encodes the selected development identity in the stream URL', () => {
    expect(worklistStreamUrl('trader a')).toBe(
      '/api/worklists/stream?developmentUser=trader%20a',
    )
  })

  it('parses known invalidation categories, delivers them, and closes', () => {
    class FakeEventSource {
      static current: FakeEventSource
      readonly url: string
      closed = false
      private listener?: (event: Event) => void

      constructor(url: string | URL) {
        this.url = String(url)
        FakeEventSource.current = this
      }

      addEventListener(
        _type: string,
        listener: EventListenerOrEventListenerObject,
      ) {
        this.listener = listener as (event: Event) => void
      }

      emit(data: string) {
        this.listener?.(new MessageEvent('invalidation', { data }))
      }

      close() {
        this.closed = true
      }
    }
    vi.stubGlobal('EventSource', FakeEventSource)
    const received: WorklistInvalidationCategory[][] = []

    const dispose = connectWorklistStream('trader-a', (value) =>
      received.push(value),
    )
    FakeEventSource.current.emit('sales-list,unknown,business-date,sales-list')

    expect(FakeEventSource.current.url).toBe(
      '/api/worklists/stream?developmentUser=trader-a',
    )
    expect(received).toEqual([['sales-list', 'business-date']])
    dispose()
    expect(FakeEventSource.current.closed).toBe(true)
  })
})
