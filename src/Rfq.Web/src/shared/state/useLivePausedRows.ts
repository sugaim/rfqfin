import { useCallback, useEffect, useRef, useState } from 'react'

export type RefreshMode = 'live' | 'paused'

interface AuthoritativeQueryResult<Row> {
  data?: Row[]
  error?: unknown
}

interface UseLivePausedRowsInput<Row> {
  authoritativeRows?: Row[]
  remoteChangeVersion: number
  protectedState: boolean
  refetch: () => Promise<AuthoritativeQueryResult<Row>>
  acknowledgeRemoteChanges: () => Promise<void>
  keyOf: (row: Row) => number
  onAuthoritativeRefresh?: () => void
}

export interface LivePausedRowsController<Row> {
  rows: Row[]
  mode: RefreshMode
  updatesPending: boolean
  liveUpdateDeferred: boolean
  refreshError: string | null
  refreshBlocked: boolean
  changeMode: (mode: RefreshMode) => Promise<void>
  refresh: () => Promise<void>
  reconcileCases: (caseIds: number[]) => Promise<void>
  clearRefreshError: () => void
}

export function reconcilePausedRows<Row>(
  snapshot: Row[],
  freshRows: Row[],
  caseIds: number[],
  keyOf: (row: Row) => number,
): Row[] {
  const targets = new Set(caseIds)
  const freshById = new Map(freshRows.map((row) => [keyOf(row), row]))

  const reconciled = snapshot.flatMap((row) => {
    const caseId = keyOf(row)
    if (!targets.has(caseId)) return [row]
    const fresh = freshById.get(caseId)

    return fresh ? [fresh] : []
  })
  const snapshotIds = new Set(snapshot.map(keyOf))
  for (const caseId of targets) {
    const fresh = freshById.get(caseId)
    if (fresh && !snapshotIds.has(caseId)) reconciled.push(fresh)
  }

  return reconciled
}

export function useLivePausedRows<Row>({
  authoritativeRows,
  remoteChangeVersion,
  protectedState,
  refetch,
  acknowledgeRemoteChanges,
  keyOf,
  onAuthoritativeRefresh,
}: UseLivePausedRowsInput<Row>): LivePausedRowsController<Row> {
  const [mode, setMode] = useState<RefreshMode>('live')
  const [pausedSnapshot, setPausedSnapshot] = useState<Row[] | null>(null)
  const [updatesPending, setUpdatesPending] = useState(false)
  const [liveUpdateDeferred, setLiveUpdateDeferred] = useState(false)
  const [refreshError, setRefreshError] = useState<string | null>(null)
  const observedRemoteVersion = useRef(remoteChangeVersion)
  const inFlight = useRef<Promise<AuthoritativeQueryResult<Row>> | null>(null)

  const fetchAuthoritative = useCallback(async () => {
    if (!inFlight.current) {
      inFlight.current = refetch().finally(() => {
        inFlight.current = null
      })
    }

    return inFlight.current
  }, [refetch])

  const catchUp = useCallback(async (): Promise<Row[] | null> => {
    setRefreshError(null)
    const result = await fetchAuthoritative()
    if (result.error || !result.data) {
      setRefreshError('The latest RFQ state could not be loaded.')

      return null
    }
    await acknowledgeRemoteChanges()
    onAuthoritativeRefresh?.()

    return result.data
  }, [acknowledgeRemoteChanges, fetchAuthoritative, onAuthoritativeRefresh])

  useEffect(() => {
    if (remoteChangeVersion === observedRemoteVersion.current) return
    observedRemoteVersion.current = remoteChangeVersion
    if (mode === 'paused') {
      setUpdatesPending(true)
      void acknowledgeRemoteChanges()
    } else if (protectedState) {
      setLiveUpdateDeferred(true)
    } else {
      void catchUp()
    }
  }, [
    acknowledgeRemoteChanges,
    catchUp,
    mode,
    protectedState,
    remoteChangeVersion,
  ])

  useEffect(() => {
    if (mode !== 'live' || protectedState || !liveUpdateDeferred) return
    void catchUp().then((rows) => {
      if (rows) setLiveUpdateDeferred(false)
    })
  }, [catchUp, liveUpdateDeferred, mode, protectedState])

  const refresh = async () => {
    if (protectedState) return
    const rows = await catchUp()
    if (!rows) return
    if (mode === 'paused') setPausedSnapshot(rows)
    setUpdatesPending(false)
    setLiveUpdateDeferred(false)
  }

  const changeMode = async (nextMode: RefreshMode) => {
    if (nextMode === mode) return
    if (nextMode === 'paused') {
      setPausedSnapshot(authoritativeRows ?? [])
      setMode('paused')
      setUpdatesPending(false)

      return
    }
    if (protectedState) return
    const rows = await catchUp()
    if (!rows) return
    setPausedSnapshot(null)
    setUpdatesPending(false)
    setLiveUpdateDeferred(false)
    setMode('live')
  }

  const reconcileCases = async (caseIds: number[]) => {
    if (!caseIds.length) return
    const rows = await catchUp()
    if (!rows) throw new Error('Authoritative RFQ reconciliation failed.')
    if (mode === 'paused')
      setPausedSnapshot((snapshot) =>
        reconcilePausedRows(snapshot ?? [], rows, caseIds, keyOf),
      )
  }

  return {
    rows: mode === 'live' ? (authoritativeRows ?? []) : (pausedSnapshot ?? []),
    mode,
    updatesPending,
    liveUpdateDeferred,
    refreshError,
    refreshBlocked: protectedState,
    changeMode,
    refresh,
    reconcileCases,
    clearRefreshError: () => setRefreshError(null),
  }
}
