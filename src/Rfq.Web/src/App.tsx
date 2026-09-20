import { useMemo, useState, type ReactNode } from 'react'
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  Layout,
  Menu,
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
  type CreateDraftRequest,
  type SalesRfq,
} from './services/api'

const navigationItems = ['Sales', 'Trader', 'EOD'].map((label) => ({
  key: label.toLowerCase(),
  label,
}))

export interface AppShellProps {
  health: 'checking' | 'ok' | 'error'
  children?: ReactNode
}

export function AppShell({ health, children }: AppShellProps) {
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
        <Typography.Title level={2}>Sales</Typography.Title>
        {children}
      </Layout.Content>
    </Layout>
  )
}

export interface SalesScreenProps {
  rfqs: SalesRfq[]
  isLoading: boolean
  isError: boolean
  isCreating: boolean
  onCreate: (request: CreateDraftRequest) => Promise<void>
  onReload: () => void | Promise<unknown>
}

export function SalesScreen({
  rfqs,
  isLoading,
  isError,
  isCreating,
  onCreate,
  onReload,
}: SalesScreenProps) {
  const [form] = Form.useForm<CreateDraftRequest>()
  const [saveError, setSaveError] = useState(false)
  const columns = useMemo<ColDef<SalesRfq>[]>(
    () => [
      { field: 'caseId', headerName: 'Case ID', minWidth: 250 },
      { field: 'clientId', headerName: 'Client', minWidth: 160 },
      { field: 'securityId', headerName: 'Security', minWidth: 180 },
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

  const handleFinish: FormProps<CreateDraftRequest>['onFinish'] = async (
    values,
  ) => {
    setSaveError(false)
    try {
      await onCreate(values)
      form.resetFields()
      await onReload()
    } catch {
      setSaveError(true)
    }
  }

  const startNew = () => {
    form.resetFields()
    setSaveError(false)
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
        <Form form={form} layout="vertical" onFinish={handleFinish}>
          <Form.Item
            name="clientId"
            label="Client ID"
            rules={[
              {
                required: true,
                whitespace: true,
                message: 'Client ID is required.',
              },
            ]}
          >
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item
            name="securityId"
            label="Security ID"
            rules={[
              {
                required: true,
                whitespace: true,
                message: 'Security ID is required.',
              },
            ]}
          >
            <Input autoComplete="off" />
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={isCreating} block>
            Save Draft
          </Button>
        </Form>
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
              getRowId={({ data }) => data.caseId}
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
  const rfqsQuery = useGetActiveSalesRfqsQuery()
  const [createDraft, createState] = useCreateDraftMutation()

  const health = healthQuery.isLoading
    ? 'checking'
    : healthQuery.isError || healthQuery.data?.status !== 'ok'
      ? 'error'
      : 'ok'

  const handleCreate = async (request: CreateDraftRequest) => {
    await createDraft(request).unwrap()
  }

  return (
    <AppShell health={health}>
      <SalesScreen
        rfqs={rfqsQuery.data ?? []}
        isLoading={rfqsQuery.isLoading || rfqsQuery.isFetching}
        isError={rfqsQuery.isError}
        isCreating={createState.isLoading}
        onCreate={handleCreate}
        onReload={rfqsQuery.refetch}
      />
    </AppShell>
  )
}
