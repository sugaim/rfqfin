import { Layout, Menu, Space, Tag, Typography } from 'antd'
import { useGetHealthQuery } from './services/api'

const navigationItems = ['Sales', 'Trader', 'EOD'].map((label) => ({
  key: label.toLowerCase(),
  label,
}))

export interface AppShellProps {
  health: 'checking' | 'ok' | 'error'
}

export function AppShell({ health }: AppShellProps) {
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
          theme="dark"
          mode="horizontal"
          defaultSelectedKeys={['sales']}
          items={navigationItems}
          className="app-navigation"
        />
        <Tag color={healthPresentation.color}>{healthPresentation.text}</Tag>
      </Layout.Header>
      <Layout.Content className="app-content">
        <Space orientation="vertical" size="middle">
          <Typography.Title level={2}>Sales</Typography.Title>
          <Typography.Text type="secondary">
            RFQ workflows will be added incrementally.
          </Typography.Text>
        </Space>
      </Layout.Content>
    </Layout>
  )
}

export function App() {
  const { data, isLoading, isError } = useGetHealthQuery()

  const health = isLoading
    ? 'checking'
    : isError || data?.status !== 'ok'
      ? 'error'
      : 'ok'

  return <AppShell health={health} />
}
