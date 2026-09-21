import { useMemo, useState, type ReactNode } from 'react'
import {
  Alert,
  AutoComplete,
  Button,
  Card,
  Form,
  Input,
  Layout,
  Menu,
  Select,
  Spin,
  Tag,
  Typography,
  type FormProps,
} from 'antd'
import type { ColDef } from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import {
  useCreateDraftMutation,
  useGetActiveSalesRfqsQuery,
  useGetHealthQuery,
  useGetSystemDateQuery,
  useGetUsersQuery,
  useLazyResolveRfqDefaultsQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
  type ClientSearchResult,
  type CreateDraftRequest,
  type RfqDefaults,
  type SalesRfq,
  type SecuritySearchResult,
} from './services/api'

const navigationItems = ['Sales', 'Trader', 'EOD'].map((label) => ({
  key: label.toLowerCase(),
  label,
}))

export interface AppShellProps {
  health: 'checking' | 'ok' | 'error'
  systemDate?: string
  children?: ReactNode
}

export function AppShell({ health, systemDate, children }: AppShellProps) {
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
        {systemDate && <Tag color="blue">System Date: {systemDate}</Tag>}
        <Tag color={healthPresentation.color}>{healthPresentation.text}</Tag>
      </Layout.Header>
      <Layout.Content className="app-content">
        <Typography.Title level={2}>Sales</Typography.Title>
        {children}
      </Layout.Content>
    </Layout>
  )
}

interface RfqFormValues extends CreateDraftRequest {
  categoryName: string
  contactOwnerName: string
  standardSettlementDate: string
}

export interface SalesScreenProps {
  rfqs: SalesRfq[]
  clients: ClientSearchResult[]
  securities: SecuritySearchResult[]
  traders: { userId: string; name: string }[]
  isLoading: boolean
  isError: boolean
  isCreating: boolean
  onClientSearch: (query: string) => void | Promise<void>
  onSecuritySearch: (query: string) => void | Promise<void>
  onResolveDefaults: (securityId: string) => Promise<RfqDefaults>
  onCreate: (request: CreateDraftRequest) => Promise<void>
  onReload: () => void | Promise<unknown>
}

export function SalesScreen({
  rfqs,
  clients,
  securities,
  traders,
  isLoading,
  isError,
  isCreating,
  onClientSearch,
  onSecuritySearch,
  onResolveDefaults,
  onCreate,
  onReload,
}: SalesScreenProps) {
  const [form] = Form.useForm<RfqFormValues>()
  const [saveError, setSaveError] = useState(false)
  const [defaultsError, setDefaultsError] = useState(false)
  const [isResolvingDefaults, setIsResolvingDefaults] = useState(false)
  const columns = useMemo<ColDef<SalesRfq>[]>(
    () => [
      { field: 'caseId', headerName: 'Case ID', minWidth: 110 },
      { field: 'clientId', headerName: 'Client ID', minWidth: 130 },
      { field: 'clientName', headerName: 'Client Name', minWidth: 180 },
      { field: 'securityId', headerName: 'Security ID', minWidth: 150 },
      {
        field: 'securityJapaneseName',
        headerName: 'Security Name',
        minWidth: 210,
      },
      { field: 'securityBbgDisplay', headerName: 'BBG Display', minWidth: 220 },
      { field: 'categoryId', headerName: 'Category', minWidth: 120 },
      { field: 'assignedTraderId', headerName: 'Trader', minWidth: 130 },
      { field: 'settlementDate', headerName: 'Settlement', minWidth: 130 },
      { field: 'rfqStatus', headerName: 'RFQ Status', minWidth: 130 },
      { field: 'revisionStatus', headerName: 'Revision', minWidth: 120 },
      {
        field: 'createdAt',
        headerName: 'Created',
        minWidth: 190,
        valueFormatter: ({ value }) =>
          value ? new Date(String(value)).toLocaleString() : '',
      },
    ],
    [],
  )

  const applyDefaults = async (securityId: string) => {
    setDefaultsError(false)
    setIsResolvingDefaults(true)
    try {
      const defaults = await onResolveDefaults(securityId)
      form.setFieldsValue({
        categoryName: defaults.categoryName,
        contactOwnerName: defaults.contactOwnerName,
        assignedTraderId: defaults.assignedTraderId,
        standardSettlementDate: defaults.standardSettlementDate,
        settlementDate: defaults.standardSettlementDate,
      })
    } catch {
      setDefaultsError(true)
    } finally {
      setIsResolvingDefaults(false)
    }
  }

  const handleFinish: FormProps<RfqFormValues>['onFinish'] = async (values) => {
    setSaveError(false)
    try {
      await onCreate({
        clientId: values.clientId,
        securityId: values.securityId,
        settlementDate: values.settlementDate,
        assignedTraderId: values.assignedTraderId,
      })
      form.resetFields()
      setDefaultsError(false)
      await onReload()
    } catch {
      setSaveError(true)
    }
  }

  const startNew = () => {
    form.resetFields()
    setSaveError(false)
    setDefaultsError(false)
  }

  return (
    <div className="sales-workspace">
      <Card
        className="work-pane"
        title="New RFQ"
        extra={<Button onClick={startNew}>New</Button>}
      >
        <Typography.Paragraph type="secondary">
          Draft is stored only after Save Draft.
        </Typography.Paragraph>
        {saveError && (
          <Alert
            type="error"
            showIcon
            message="Draft could not be saved."
            className="form-alert"
          />
        )}
        {defaultsError && (
          <Alert
            type="error"
            showIcon
            message="Security defaults could not be resolved."
            className="form-alert"
          />
        )}
        <Spin spinning={isResolvingDefaults}>
          <Form<RfqFormValues>
            form={form}
            layout="vertical"
            onFinish={handleFinish}
          >
            <Form.Item
              name="clientId"
              label="Client"
              rules={[{ required: true, whitespace: true, message: 'Client is required.' }]}
            >
              <AutoComplete
                filterOption={false}
                options={clients.map((client) => ({
                  value: client.clientId,
                  label: `${client.code} — ${client.name}`,
                }))}
                onSearch={(value) => void onClientSearch(value)}
              />
            </Form.Item>
            <Form.Item
              name="securityId"
              label="Security"
              rules={[{ required: true, whitespace: true, message: 'Security is required.' }]}
            >
              <AutoComplete
                filterOption={false}
                options={securities.map((security) => ({
                  value: security.securityId,
                  label: `${security.japaneseName} | ${security.bbgDisplay} | ${security.internalCode} | ${security.isin}`,
                }))}
                onSearch={(value) => void onSecuritySearch(value)}
                onSelect={(securityId) => void applyDefaults(securityId)}
              />
            </Form.Item>
            <Form.Item name="categoryName" label="Category">
              <Input disabled />
            </Form.Item>
            <Form.Item name="contactOwnerName" label="Contact Owner">
              <Input disabled />
            </Form.Item>
            <Form.Item
              name="assignedTraderId"
              label="Assigned Trader"
              rules={[{ required: true, message: 'Assigned Trader is required.' }]}
            >
              <Select
                options={traders.map((trader) => ({
                  value: trader.userId,
                  label: trader.name,
                }))}
              />
            </Form.Item>
            <Form.Item name="standardSettlementDate" label="Standard Settlement">
              <Input type="date" disabled />
            </Form.Item>
            <Form.Item
              name="settlementDate"
              label="Settlement Date"
              rules={[{ required: true, message: 'Settlement date is required.' }]}
            >
              <Input type="date" />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={isCreating} block>
              Save Draft
            </Button>
          </Form>
        </Spin>
      </Card>

      <Card
        className="rfq-grid-card"
        title="Active RFQs"
        extra={<Button onClick={() => void onReload()}>Reload</Button>}
      >
        {isError && (
          <Alert
            type="error"
            showIcon
            message="RFQs could not be loaded."
            className="grid-alert"
          />
        )}
        <Spin spinning={isLoading}>
          <div className="rfq-grid" data-testid="rfq-grid">
            <AgGridReact<SalesRfq>
              rowData={rfqs}
              columnDefs={columns}
              getRowId={({ data }) => String(data.caseId)}
              defaultColDef={{ sortable: true, filter: true, resizable: true }}
            />
          </div>
        </Spin>
      </Card>
    </div>
  )
}

export function App() {
  const healthQuery = useGetHealthQuery()
  const systemDateQuery = useGetSystemDateQuery()
  const rfqsQuery = useGetActiveSalesRfqsQuery()
  const tradersQuery = useGetUsersQuery('Trader')
  const [createDraft, createState] = useCreateDraftMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqDefaultsQuery()
  const [clients, setClients] = useState<ClientSearchResult[]>([])
  const [securities, setSecurities] = useState<SecuritySearchResult[]>([])

  const health = healthQuery.isLoading
    ? 'checking'
    : healthQuery.isError || healthQuery.data?.status !== 'ok'
      ? 'error'
      : 'ok'
  const systemDate = systemDateQuery.isLoading
    ? 'checking'
    : systemDateQuery.isError
      ? 'unavailable'
      : systemDateQuery.data?.date

  const handleClientSearch = async (query: string) => {
    setClients(query.trim() ? await searchClients(query).unwrap() : [])
  }

  const handleSecuritySearch = async (query: string) => {
    setSecurities(query.trim() ? await searchSecurities(query).unwrap() : [])
  }

  return (
    <AppShell health={health} systemDate={systemDate}>
      <SalesScreen
        rfqs={rfqsQuery.data ?? []}
        clients={clients}
        securities={securities}
        traders={(tradersQuery.data ?? []).map((user) => ({
          userId: user.userId,
          name: user.name,
        }))}
        isLoading={rfqsQuery.isLoading || rfqsQuery.isFetching}
        isError={rfqsQuery.isError}
        isCreating={createState.isLoading}
        onClientSearch={handleClientSearch}
        onSecuritySearch={handleSecuritySearch}
        onResolveDefaults={(securityId) =>
          resolveDefaults({ securityId }).unwrap()
        }
        onCreate={(request) => createDraft(request).unwrap().then(() => undefined)}
        onReload={rfqsQuery.refetch}
      />
    </AppShell>
  )
}
