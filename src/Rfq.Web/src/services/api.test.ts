import { configureStore } from '@reduxjs/toolkit'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  api,
  type PostProcessItem,
  type PostProcessPreset,
  type PostProcessScope,
} from '@/services/api'

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

describe('Post Process API cache reconciliation', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('invalidates inactive preset and scope variants after a successful commit', async () => {
    let closed = false
    const gets: { preset: PostProcessPreset; scope: PostProcessScope }[] = []
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
              status: 'Succeeded',
              code: null,
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
})
