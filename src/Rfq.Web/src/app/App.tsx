import { useEffect, useState } from 'react'
import { ConfigProvider } from 'antd'
import { Outlet } from 'react-router'
import {
  useGetBusinessDateQuery,
  useGetHealthQuery,
  useGetMeQuery,
  useGetDefaultQuoteModeQuery,
  useGetQuoteExpiryQuery,
  useGetThemeQuery,
  useSaveDefaultQuoteModeMutation,
  useSaveQuoteExpiryMutation,
  useSaveThemeMutation,
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
  salesChangeVersion: number
  traderChangeVersion: number
  recentRevisionsChangeVersion: number
}

export function App() {
  const configuredIdentity =
    window.localStorage.getItem('rfq-development-user') ?? 'sales-dev'
  const [salesChangeVersion, setSalesChangeVersion] = useState(0)
  const [traderChangeVersion, setTraderChangeVersion] = useState(0)
  const [recentRevisionsChangeVersion, setRecentRevisionsChangeVersion] =
    useState(0)
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

  const refreshUpdates = () => {
    setSalesChangeVersion((current) => current + 1)
    setTraderChangeVersion((current) => current + 1)
    setRecentRevisionsChangeVersion((current) => current + 1)
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
    const source = new EventSource('/api/events/stream')
    source.addEventListener('invalidation', (event) => {
      const categories = new Set(
        (event as MessageEvent<string>).data.split(','),
      )
      if (categories.has('sales-list'))
        setSalesChangeVersion((current) => current + 1)
      if (categories.has('trader-list'))
        setTraderChangeVersion((current) => current + 1)
      if (categories.has('recent-revisions'))
        setRecentRevisionsChangeVersion((current) => current + 1)
      if (categories.has('business-date')) {
        void businessDateQuery.refetch()
        setSalesChangeVersion((current) => current + 1)
        setTraderChangeVersion((current) => current + 1)
      }
    })

    return () => source.close()
  }, [businessDateQuery.refetch])

  const outletContext: AppOutletContext = {
    currentUserId,
    businessDate: businessDateQuery.data?.date,
    salesChangeVersion,
    traderChangeVersion,
    recentRevisionsChangeVersion,
  }

  return (
    <ConfigProvider theme={applicationTheme(themeMode)}>
      <AppShell
        health={health}
        businessDate={businessDate}
        currentUserId={currentUserId}
        onIdentityChange={changeIdentity}
        pendingUpdates={0}
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
