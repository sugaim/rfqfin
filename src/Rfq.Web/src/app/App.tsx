import { useEffect, useState } from 'react'
import { ConfigProvider } from 'antd'
import { Outlet } from 'react-router'
import {
  useGetBusinessDateQuery,
  useGetEventsQuery,
  useGetHealthQuery,
  useGetMeQuery,
  type PersistedEvent,
} from '@/services/api'
import { AppShell } from '@/app/AppShell'
import { applicationTheme } from '@/app/theme'

export interface AppOutletContext {
  currentUserId: string
  businessDate?: string
  events: PersistedEvent[]
  refreshToken: number
  remoteChangeVersion: number
  acknowledgeRemoteChanges: () => Promise<void>
}

export function App() {
  const configuredIdentity =
    window.localStorage.getItem('rfq-development-user') ?? 'sales-dev'
  const [refreshEventId, setRefreshEventId] = useState(0)
  const [refreshToken, setRefreshToken] = useState(0)
  const [remoteChangeVersion, setRemoteChangeVersion] = useState(0)
  const healthQuery = useGetHealthQuery()
  const businessDateQuery = useGetBusinessDateQuery()
  const currentUserQuery = useGetMeQuery()
  const eventsQuery = useGetEventsQuery(refreshEventId)
  const health = healthQuery.isLoading
    ? 'checking'
    : healthQuery.isError || healthQuery.data?.status !== 'ok'
      ? 'error'
      : 'ok'
  const businessDate = businessDateQuery.isLoading
    ? 'checking'
    : businessDateQuery.isError
      ? 'unavailable'
      : businessDateQuery.data?.date
  const currentUserId = currentUserQuery.data?.userId ?? configuredIdentity

  const changeIdentity = (userId: string) => {
    window.localStorage.setItem('rfq-development-user', userId)
    window.location.assign(userId.startsWith('trader-') ? '/trader' : '/sales')
  }

  const acknowledgeRemoteChanges = async () => {
    const result = await eventsQuery.refetch()
    const latest = Math.max(
      0,
      ...(result.data ?? []).map((event) => event.eventId),
    )
    setRefreshEventId(latest)
    setRefreshToken((current) => current + 1)
  }

  const refreshUpdates = () => {
    void acknowledgeRemoteChanges()
  }

  useEffect(() => {
    if (typeof EventSource === 'undefined') return undefined
    const source = new EventSource(`/api/events/stream?after=${refreshEventId}`)
    source.addEventListener('changed', () => {
      setRemoteChangeVersion((current) => current + 1)
      void eventsQuery.refetch()
    })
    return () => source.close()
  }, [refreshEventId, eventsQuery.refetch])

  const outletContext: AppOutletContext = {
    currentUserId,
    businessDate: businessDateQuery.data?.date,
    events: eventsQuery.data ?? [],
    refreshToken,
    remoteChangeVersion,
    acknowledgeRemoteChanges,
  }

  return (
    <ConfigProvider theme={applicationTheme}>
      <AppShell
        health={health}
        businessDate={businessDate}
        currentUserId={currentUserId}
        onIdentityChange={changeIdentity}
        pendingUpdates={eventsQuery.data?.length ?? 0}
        onRefreshUpdates={refreshUpdates}
      >
        <Outlet context={outletContext} />
      </AppShell>
    </ConfigProvider>
  )
}
