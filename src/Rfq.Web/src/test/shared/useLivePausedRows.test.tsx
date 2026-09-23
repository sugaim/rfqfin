import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import {
  reconcilePausedRows,
  useLivePausedRows,
} from '@/shared/state/useLivePausedRows'

interface Row {
  caseId: number
  value: string
}

const keyOf = (row: Row) => row.caseId

describe('Live/Paused authoritative reconciliation', () => {
  it('replaces or removes only successful targets in a Paused snapshot', () => {
    const snapshot = [
      { caseId: 1, value: 'old-1' },
      { caseId: 2, value: 'old-2' },
      { caseId: 3, value: 'old-3' },
    ]
    const fresh = [
      { caseId: 1, value: 'fresh-1' },
      { caseId: 3, value: 'fresh-3' },
      { caseId: 4, value: 'fresh-4' },
    ]

    expect(reconcilePausedRows(snapshot, fresh, [1, 2], keyOf)).toEqual([
      { caseId: 1, value: 'fresh-1' },
      { caseId: 3, value: 'old-3' },
    ])
  })

  it('keeps Live query-authoritative and catches up after an unprotected wake-up', async () => {
    const refetch = vi.fn().mockResolvedValue({
      data: [{ caseId: 1, value: 'fresh' }],
    })
    const { result, rerender } = renderHook(
      ({ version, rows }) =>
        useLivePausedRows({
          authoritativeRows: rows,
          remoteChangeVersion: version,
          protectedState: false,
          refetch,
          keyOf,
        }),
      {
        initialProps: {
          version: 0,
          rows: [{ caseId: 1, value: 'query' }],
        },
      },
    )

    expect(result.current.rows[0].value).toBe('query')
    rerender({ version: 1, rows: [{ caseId: 1, value: 'new-query' }] })
    await waitFor(() => expect(refetch).toHaveBeenCalledOnce())
    expect(result.current.rows[0].value).toBe('new-query')
  })

  it('defers one Live catch-up while protected and runs it when protection ends', async () => {
    const refetch = vi.fn().mockResolvedValue({ data: [] })
    const { result, rerender } = renderHook(
      ({ version, protectedState }) =>
        useLivePausedRows<Row>({
          authoritativeRows: [],
          remoteChangeVersion: version,
          protectedState,
          refetch,
          keyOf,
        }),
      { initialProps: { version: 0, protectedState: true } },
    )

    rerender({ version: 1, protectedState: true })
    expect(refetch).not.toHaveBeenCalled()
    expect(result.current.liveUpdateDeferred).toBe(true)
    rerender({ version: 1, protectedState: false })
    await waitFor(() => expect(refetch).toHaveBeenCalledOnce())
  })

  it('keeps the Paused snapshot on wake-up and replaces it on manual refresh', async () => {
    const refetch = vi.fn().mockResolvedValue({
      data: [{ caseId: 1, value: 'fresh' }],
    })
    const { result, rerender } = renderHook(
      ({ version, rows }) =>
        useLivePausedRows({
          authoritativeRows: rows,
          remoteChangeVersion: version,
          protectedState: false,
          refetch,
          keyOf,
        }),
      {
        initialProps: {
          version: 0,
          rows: [{ caseId: 1, value: 'snapshot' }],
        },
      },
    )

    await act(() => result.current.changeMode('paused'))
    rerender({ version: 1, rows: [{ caseId: 1, value: 'query-changed' }] })
    expect(result.current.rows[0].value).toBe('snapshot')
    expect(result.current.updatesPending).toBe(true)
    await act(() => result.current.refresh())
    expect(result.current.mode).toBe('paused')
    expect(result.current.rows[0].value).toBe('fresh')
    expect(result.current.updatesPending).toBe(false)
  })

  it('blocks refresh and Paused-to-Live while protected, then catches up once', async () => {
    const refetch = vi.fn().mockResolvedValue({
      data: [{ caseId: 1, value: 'fresh' }],
    })
    const { result, rerender } = renderHook(
      ({ protectedState }) =>
        useLivePausedRows<Row>({
          authoritativeRows: [{ caseId: 1, value: 'query' }],
          remoteChangeVersion: 0,
          protectedState,
          refetch,
          keyOf,
        }),
      { initialProps: { protectedState: false } },
    )
    await act(() => result.current.changeMode('paused'))
    rerender({ protectedState: true })
    await act(() => result.current.refresh())
    await act(() => result.current.changeMode('live'))
    expect(result.current.mode).toBe('paused')
    expect(refetch).not.toHaveBeenCalled()

    rerender({ protectedState: false })
    await act(() => result.current.changeMode('live'))
    expect(refetch).toHaveBeenCalledOnce()
    expect(result.current.mode).toBe('live')
  })

  it('runs another catch-up when invalidation advances during an in-flight read', async () => {
    let completeFirst!: (value: { data: Row[] }) => void
    const first = new Promise<{ data: Row[] }>((resolve) => {
      completeFirst = resolve
    })
    const refetch = vi
      .fn()
      .mockImplementationOnce(() => first)
      .mockResolvedValue({ data: [{ caseId: 1, value: 'generation-2' }] })
    const { rerender } = renderHook(
      ({ version }) =>
        useLivePausedRows<Row>({
          authoritativeRows: [],
          remoteChangeVersion: version,
          protectedState: false,
          refetch,
          keyOf,
        }),
      { initialProps: { version: 0 } },
    )

    rerender({ version: 1 })
    await waitFor(() => expect(refetch).toHaveBeenCalledOnce())
    rerender({ version: 2 })
    await act(async () => {
      completeFirst({ data: [{ caseId: 1, value: 'generation-1' }] })
      await first
    })

    await waitFor(() => expect(refetch).toHaveBeenCalledTimes(2))
  })
})
