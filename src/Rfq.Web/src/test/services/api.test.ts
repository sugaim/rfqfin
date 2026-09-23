import { configureStore } from '@reduxjs/toolkit'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  api,
  type PostProcessItem,
  type PostProcessPreset,
  type PostProcessScope,
} from '@/services/api'
import { worklistStreamUrl } from '@/app/App'

const item: PostProcessItem = {
  caseId: 101,
  createdAt: '2026-09-22T01:00:00Z',
  createdBusinessDate: '2026-09-22',
  clientId: 'client-a',
  clientName: 'Client A',
  securityId: 'security-a',
  securityName: 'Security A',
  securityBbgDisplay: 'SEC A',
  notional: 1_000_000,
  settlementDate: '2026-09-24',
  contactOwnerId: 'sales-dev',
  salesId: 'sales-dev',
  assignedTraderId: 'trader-dev',
  rfqStatus: 'Active',
  currentVersion: 3,
  salesAndTradingMessage: '',
  myMemo: '',
  myMemoVersion: 1,
  price: 99.25,
  finalSimpleYield: 1.5,
  yield: 1.4,
  ysc: 1.3,
  gSpread: 20,
  closedBusinessDate: null,
  lastCorrectionReason: null,
  lastChangedBy: 'sales-dev',
  lastChangedAt: '2026-09-22T01:00:00Z',
}

function json(value: unknown) {
  return new Response(JSON.stringify(value), {
    status: 200,
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

describe('Post Process API cache reconciliation', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('invalidates inactive preset and scope variants after a successful commit', async () => {
    let closed = false
    const gets: { preset: PostProcessPreset; scope: PostProcessScope }[] = []
    useAbsoluteRequests()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(input, init)
        const url = new URL(request.url)
        if (request.method === 'POST') {
          closed = true

          return json([
            {
              caseId: 101,
              status: 'Applied',
              failureCode: null,
              message: null,
            },
          ])
        }

        const preset = url.searchParams.get('preset') as PostProcessPreset
        const scope = url.searchParams.get('scope') as PostProcessScope
        gets.push({ preset, scope })

        return json(preset === 'Unclosed' && closed ? [] : [item])
      }),
    )
    const store = configureStore({
      reducer: { [api.reducerPath]: api.reducer },
      middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware().concat(api.middleware),
    })

    const unclosed = store.dispatch(
      api.endpoints.getPostProcess.initiate({
        preset: 'Unclosed',
        scope: 'Mine',
      }),
    )
    expect(await unclosed.unwrap()).toEqual([item])
    unclosed.unsubscribe()

    const today = store.dispatch(
      api.endpoints.getPostProcess.initiate({
        preset: 'Today',
        scope: 'AllPermitted',
      }),
    )
    expect(await today.unwrap()).toEqual([item])

    await store
      .dispatch(
        api.endpoints.commitPostProcess.initiate({
          items: [
            {
              caseId: 101,
              expectedCurrentVersion: 3,
              lifecycleChange: { type: 'Away' },
            },
          ],
        }),
      )
      .unwrap()

    const revisited = store.dispatch(
      api.endpoints.getPostProcess.initiate({
        preset: 'Unclosed',
        scope: 'Mine',
      }),
    )
    expect(await revisited.unwrap()).toEqual([])
    expect(
      gets.filter(
        (request) => request.preset === 'Unclosed' && request.scope === 'Mine',
      ),
    ).toHaveLength(2)

    revisited.unsubscribe()
    today.unsubscribe()
    store.dispatch(api.util.resetApiState())
  })

  it('re-fetches authoritative state when every commit item fails or has no change', async () => {
    const queryArgs = { preset: 'Today', scope: 'Mine' } as const
    const authoritativeItem: PostProcessItem = {
      ...item,
      rfqStatus: 'Presented',
      currentVersion: 4,
      lastChangedBy: 'other-sales',
      lastChangedAt: '2026-09-22T02:00:00Z',
    }
    let currentItem = item
    let getCount = 0
    useAbsoluteRequests()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(input, init)
        if (request.method === 'POST') {
          currentItem = authoritativeItem

          return json([
            {
              caseId: 101,
              status: 'Failed',
              failureCode: 'VersionConflict',
              message: 'The RFQ changed before the commit was applied.',
            },
            {
              caseId: 102,
              status: 'NoChange',
              failureCode: null,
              message: null,
            },
          ])
        }

        getCount += 1

        return json([currentItem])
      }),
    )
    const store = configureStore({
      reducer: { [api.reducerPath]: api.reducer },
      middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware().concat(api.middleware),
    })
    const query = store.dispatch(
      api.endpoints.getPostProcess.initiate(queryArgs),
    )
    expect(await query.unwrap()).toEqual([item])

    await store
      .dispatch(
        api.endpoints.commitPostProcess.initiate({
          items: [
            {
              caseId: 101,
              expectedCurrentVersion: 3,
              lifecycleChange: { type: 'Away' },
            },
            {
              caseId: 102,
              expectedCurrentVersion: 1,
              lifecycleChange: { type: 'Hit' },
            },
          ],
        }),
      )
      .unwrap()

    await vi.waitFor(() => {
      expect(
        api.endpoints.getPostProcess.select(queryArgs)(store.getState()).data,
      ).toEqual([authoritativeItem])
    })
    expect(getCount).toBe(2)

    query.unsubscribe()
    store.dispatch(api.util.resetApiState())
  })
})

describe('03a transport contract', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('sends a single UI action through the final one-item plural route', async () => {
    let captured: { url: string; body: unknown } | undefined
    useAbsoluteRequests()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request ? input : new Request(input, init)
        captured = {
          url: new URL(request.url).pathname,
          body: await request.clone().json(),
        }

        return json([
          {
            caseId: 101,
            status: 'Applied',
            failureCode: null,
            message: null,
          },
        ])
      }),
    )
    const store = configureStore({
      reducer: { [api.reducerPath]: api.reducer },
      middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware().concat(api.middleware),
    })

    await store
      .dispatch(
        api.endpoints.presentRfqs.initiate({
          items: [{ caseId: 101, expectedCurrentVersion: 7 }],
        }),
      )
      .unwrap()

    expect(captured).toEqual({
      url: '/api/rfqs/present',
      body: { items: [{ caseId: 101, expectedCurrentVersion: 7 }] },
    })
    store.dispatch(api.util.resetApiState())
  })

  it('builds the final worklist stream URL with the selected identity', () => {
    expect(worklistStreamUrl('trader-a')).toBe(
      '/api/worklists/stream?developmentUser=trader-a',
    )
    expect(worklistStreamUrl('trader a')).toBe(
      '/api/worklists/stream?developmentUser=trader%20a',
    )
  })
})
