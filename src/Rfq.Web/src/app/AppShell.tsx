import type { ReactNode } from 'react'
import { useState } from 'react'
import { Button, Layout, Menu, Select, Tag, Typography } from 'antd'
import { useLocation, useNavigate } from 'react-router'
import type { AppThemeMode } from '@/app/theme'
import {
  SettingsDrawer,
  type PersonalSettingsDraft,
} from '@/app/SettingsDrawer'
import type { QuoteExpiryResponse, QuoteModeResponse } from '@/generated/rfqApi'

const navigationItems = [
  { key: '/sales', label: 'Sales' },
  { key: '/trader', label: 'Trader' },
  { key: '/post-process', label: 'Post Process' },
]

export interface AppShellProps {
  health: 'checking' | 'ok' | 'error'
  businessDate?: string
  children?: ReactNode
  currentUserId?: string
  onIdentityChange?: (userId: string) => void
  pendingUpdates?: number
  onRefreshUpdates?: () => void
  isTrader?: boolean
  themeMode?: AppThemeMode
  quoteMode?: QuoteModeResponse['mode']
  quoteExpiry?: QuoteExpiryResponse
  settingsLoading?: boolean
  onSaveSettings?: (settings: PersonalSettingsDraft) => Promise<void>
}

export function AppShell({
  health,
  businessDate,
  children,
  currentUserId = 'sales-dev',
  onIdentityChange,
  pendingUpdates = 0,
  onRefreshUpdates,
  isTrader = false,
  themeMode = 'Dark',
  quoteMode = 'Calculated',
  quoteExpiry = { type: 'None', minutes: null },
  settingsLoading = false,
  onSaveSettings = async () => undefined,
}: AppShellProps) {
  const [settingsOpen, setSettingsOpen] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()
  const activeRoute =
    navigationItems.find((item) => location.pathname.startsWith(item.key)) ??
    navigationItems[0]
  const healthPresentation = {
    checking: { color: 'processing', text: 'API checking' },
    ok: { color: 'success', text: 'API healthy' },
    error: { color: 'error', text: 'API unavailable' },
  }[health]

  return (
    <Layout className="app-shell">
      <Layout.Header className="app-header">
        <Typography.Title level={3} className="app-title">
          RFQ
        </Typography.Title>
        <Menu
          theme={themeMode === 'Dark' ? 'dark' : 'light'}
          mode="horizontal"
          selectedKeys={[activeRoute.key]}
          items={navigationItems}
          onClick={({ key }) => navigate(key)}
          className="app-navigation"
        />
        {isTrader && (
          <Button size="small" onClick={() => setSettingsOpen(true)}>
            Settings
          </Button>
        )}
        <Select
          aria-label="Development identity"
          value={currentUserId}
          onChange={onIdentityChange}
          options={[
            { value: 'sales-dev', label: 'Sales Dev' },
            { value: 'sales-a', label: 'Sales A' },
            { value: 'trader-a', label: 'Trader A' },
            { value: 'trader-b', label: 'Trader B' },
          ]}
          style={{ width: 130 }}
        />
        {businessDate && <Tag color="blue">Business Date: {businessDate}</Tag>}
        {pendingUpdates > 0 && (
          <Button size="small" type="primary" onClick={onRefreshUpdates}>
            Updates Available ({pendingUpdates})
          </Button>
        )}
        <Tag color={healthPresentation.color}>{healthPresentation.text}</Tag>
      </Layout.Header>
      <Layout.Content className="app-content">
        <Typography.Title level={2}>{activeRoute.label}</Typography.Title>
        {children}
      </Layout.Content>
      {isTrader && (
        <SettingsDrawer
          open={settingsOpen}
          theme={themeMode}
          quoteMode={quoteMode}
          quoteExpiry={quoteExpiry}
          isLoading={settingsLoading}
          onClose={() => setSettingsOpen(false)}
          onSave={onSaveSettings}
        />
      )}
    </Layout>
  )
}
