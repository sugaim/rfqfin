import { useEffect, useState } from 'react'
import { ConfigProvider } from 'antd'
import { Outlet } from 'react-router'
import {
  useGetBusinessDateQuery,
  useGetEventsQuery,
  useGetHealthQuery,
  useGetMeQuery,
  useGetDefaultQuoteModeQuery,
  useGetQuoteExpiryQuery,
  useGetThemeQuery,
  useSaveDefaultQuoteModeMutation,
  useSaveQuoteExpiryMutation,
  useSaveThemeMutation,
  type PersistedEvent,
} from '@/services/api'
import { AppShell } from '@/app/AppShell'
import {
  applicationTheme,
  applyGlobalTheme,
  type AppThemeMode,
} from '@/app/theme'
import type { PersonalSettingsDraft } from '@/app/SettingsDrawer'

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
  const [themeMode, setThemeMode] = useState<AppThemeMode>('Dark')
  const healthQuery = useGetHealthQuery()
  const businessDateQuery = useGetBusinessDateQuery()
  const currentUserQuery = useGetMeQuery()
  const themeQuery = useGetThemeQuery()
  const quoteModeQuery = useGetDefaultQuoteModeQuery()
  const quoteExpiryQuery = useGetQuoteExpiryQuery()
  const [saveTheme, saveThemeState] = useSaveThemeMutation()
  const [saveQuoteMode, saveQuoteModeState] = useSaveDefaultQuoteModeMutation()
  const [saveQuoteExpiry, saveQuoteExpiryState] = useSaveQuoteExpiryMutation()
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

  useEffect(() => {
    const mode = themeQuery.data?.mode ?? 'Dark'
    setThemeMode(mode)
    applyGlobalTheme(mode)
  }, [themeQuery.data?.mode])

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

  const saveSettings = async (settings: PersonalSettingsDraft) => {
    await Promise.all([
      saveQuoteMode({ mode: settings.quoteMode }).unwrap(),
      saveQuoteExpiry(settings.quoteExpiry).unwrap(),
    ])
    await saveTheme({ mode: settings.theme }).unwrap()
    setThemeMode(settings.theme)
    applyGlobalTheme(settings.theme)
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
    <ConfigProvider theme={applicationTheme(themeMode)}>
      <AppShell
        health={health}
        businessDate={businessDate}
        currentUserId={currentUserId}
        onIdentityChange={changeIdentity}
        pendingUpdates={eventsQuery.data?.length ?? 0}
        onRefreshUpdates={refreshUpdates}
        isTrader={currentUserQuery.data?.roles.includes('Trader') ?? false}
        themeMode={themeMode}
        quoteMode={quoteModeQuery.data?.mode ?? 'Calculated'}
        quoteExpiry={quoteExpiryQuery.data ?? { type: 'None', minutes: null }}
        settingsLoading={
          themeQuery.isLoading ||
          quoteModeQuery.isLoading ||
          quoteExpiryQuery.isLoading ||
          saveThemeState.isLoading ||
          saveQuoteModeState.isLoading ||
          saveQuoteExpiryState.isLoading
        }
        onSaveSettings={saveSettings}
      >
        <Outlet context={outletContext} />
      </AppShell>
    </ConfigProvider>
  )
}
