import { useMemo, useState, type ReactNode } from 'react'
import { Alert, AutoComplete, Button, Card, Form, Input, InputNumber, Layout, Menu, Popconfirm, Select, Space, Spin, Tag, Typography } from 'antd'
import type { ColDef, RowClickedEvent } from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import {
  useConfirmDraftMutation, useConfirmNewRfqMutation, useCreateDraftMutation,
  useDiscardDraftMutation, useGetActiveSalesRfqsQuery, useGetHealthQuery,
  useGetSystemDateQuery, useGetUsersQuery, useLazyResolveRfqDefaultsQuery,
  useLazySearchClientsQuery, useLazySearchSecuritiesQuery, useUpdateDraftMutation,
  type ClientSearchResult, type CreateDraftRequest, type RfqDefaults,
  type SalesRfq, type SecuritySearchResult, type UpdateDraftRequest,
} from './services/api'

const navigationItems = ['Sales', 'Trader', 'EOD'].map((label) => ({ key: label.toLowerCase(), label }))
const million = 1_000_000

const toAbsoluteNotional = (notionalInMillions?: number) =>
  notionalInMillions === undefined
    ? undefined
    : Number((notionalInMillions * million).toFixed(2))

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
        <Typography.Title level={3} className="app-title">RFQ</Typography.Title>
        <Menu theme="dark" mode="horizontal" defaultSelectedKeys={['sales']} items={navigationItems} className="app-navigation" />
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
  isMutating: boolean
  onClientSearch: (query: string) => void | Promise<void>
  onSecuritySearch: (query: string) => void | Promise<void>
  onResolveDefaults: (securityId: string) => Promise<RfqDefaults>
  onCreate: (request: CreateDraftRequest) => Promise<void>
  onUpdate: (caseId: number, request: UpdateDraftRequest) => Promise<void>
  onConfirmNew: (request: CreateDraftRequest) => Promise<void>
  onConfirmDraft: (caseId: number, request: UpdateDraftRequest) => Promise<void>
  onDiscard: (caseId: number, expectedVersion: number) => Promise<void>
  onReload: () => void | Promise<unknown>
}

export function SalesScreen(props: SalesScreenProps) {
  const {
    rfqs, clients, securities, traders, isLoading, isError, isMutating,
    onClientSearch, onSecuritySearch, onResolveDefaults, onCreate, onUpdate,
    onConfirmNew, onConfirmDraft, onDiscard, onReload,
  } = props
  const [form] = Form.useForm<RfqFormValues>()
  const [editingDraft, setEditingDraft] = useState<SalesRfq | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [defaultsError, setDefaultsError] = useState(false)
  const [isResolvingDefaults, setIsResolvingDefaults] = useState(false)
  const columns = useMemo<ColDef<SalesRfq>[]>(() => [
    { field: 'caseId', headerName: 'Case ID', minWidth: 110 },
    { field: 'clientId', headerName: 'Client ID', minWidth: 130 },
    { field: 'clientName', headerName: 'Client Name', minWidth: 180 },
    { field: 'securityId', headerName: 'Security ID', minWidth: 150 },
    { field: 'securityJapaneseName', headerName: 'Security Name', minWidth: 210 },
    { field: 'securityBbgDisplay', headerName: 'BBG Display', minWidth: 220 },
    { field: 'categoryId', headerName: 'Category', minWidth: 120 },
    {
      field: 'notional',
      headerName: 'Notional (MM)',
      minWidth: 140,
      valueFormatter: ({ value }) =>
        value === null || value === undefined
          ? ''
          : (Number(value) / million).toLocaleString(),
    },
    { field: 'assignedTraderId', headerName: 'Trader', minWidth: 130 },
    { field: 'settlementDate', headerName: 'Settlement', minWidth: 130 },
    { field: 'rfqStatus', headerName: 'RFQ Status', minWidth: 130 },
    { field: 'revisionStatus', headerName: 'Revision', minWidth: 120 },
    { field: 'quoteStatus', headerName: 'Quote Status', minWidth: 130 },
    { field: 'quoteRequestReason', headerName: 'Reason', minWidth: 120 },
    { field: 'createdAt', headerName: 'Created', minWidth: 190, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : '' },
  ], [])

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

  const toCreateRequest = (values: RfqFormValues): CreateDraftRequest => ({
    clientId: values.clientId,
    securityId: values.securityId,
    notional: toAbsoluteNotional(values.notional),
    settlementDate: values.settlementDate || undefined,
    salesAndTradingMessage: values.salesAndTradingMessage,
    assignedTraderId: values.assignedTraderId,
  })

  const toUpdateRequest = (values: RfqFormValues, draft: SalesRfq): UpdateDraftRequest => ({
    notional: toAbsoluteNotional(values.notional),
    settlementDate: values.settlementDate || undefined,
    salesAndTradingMessage: values.salesAndTradingMessage,
    assignedTraderId: values.assignedTraderId,
    expectedVersion: draft.version,
  })

  const finishAction = async () => {
    setEditingDraft(null)
    form.resetFields()
    setDefaultsError(false)
    await onReload()
  }

  const runAction = async (action: () => Promise<void>) => {
    setActionError(null)
    try {
      await action()
      await finishAction()
    } catch {
      setActionError('The RFQ action could not be completed. Reload and try again.')
    }
  }

  const saveDraft = async () => {
    let required: Partial<RfqFormValues>
    try {
      required = await form.validateFields(['clientId', 'securityId'])
    } catch {
      return
    }
    const values = { ...form.getFieldsValue(), ...required } as RfqFormValues
    await runAction(() => editingDraft
      ? onUpdate(editingDraft.caseId, toUpdateRequest(values, editingDraft))
      : onCreate(toCreateRequest(values)))
  }

  const confirm = async () => {
    let required: Partial<RfqFormValues>
    try {
      required = await form.validateFields(['clientId', 'securityId', 'assignedTraderId', 'notional', 'settlementDate'])
    } catch {
      return
    }
    const values = { ...form.getFieldsValue(), ...required } as RfqFormValues
    await runAction(() => editingDraft
      ? onConfirmDraft(editingDraft.caseId, toUpdateRequest(values, editingDraft))
      : onConfirmNew(toCreateRequest(values)))
  }

  const discard = async () => {
    if (editingDraft) await runAction(() => onDiscard(editingDraft.caseId, editingDraft.version))
  }

  const startNew = () => {
    setEditingDraft(null)
    form.resetFields()
    setActionError(null)
    setDefaultsError(false)
  }

  const editSelectedDraft = ({ data }: RowClickedEvent<SalesRfq>) => {
    if (!data || data.revisionStatus !== 'Draft') return
    setEditingDraft(data)
    setActionError(null)
    setDefaultsError(false)
    form.setFieldsValue({
      clientId: data.clientId,
      securityId: data.securityId,
      categoryName: data.categoryId,
      contactOwnerName: data.contactOwnerId,
      assignedTraderId: data.assignedTraderId,
      standardSettlementDate: data.standardSettlementDate,
      settlementDate: data.settlementDate ?? undefined,
      notional: data.notional === null ? undefined : data.notional / million,
      salesAndTradingMessage: data.salesAndTradingMessage,
    })
  }

  return (
    <div className="sales-workspace">
      <Card className="work-pane" title={editingDraft ? `Draft Case ${editingDraft.caseId}` : 'New RFQ'} extra={<Button onClick={startNew}>New</Button>}>
        <Typography.Paragraph type="secondary">Save may be incomplete. Confirm requires notional, settlement, and trader.</Typography.Paragraph>
        {actionError && <Alert type="error" showIcon message={actionError} className="form-alert" />}
        {defaultsError && <Alert type="error" showIcon message="Security defaults could not be resolved." className="form-alert" />}
        <Spin spinning={isResolvingDefaults}>
          <Form<RfqFormValues> form={form} layout="vertical">
            <Form.Item name="clientId" label="Client" rules={[{ required: true, whitespace: true, message: 'Client is required.' }]}>
              <AutoComplete disabled={editingDraft !== null} filterOption={false} options={clients.map((client) => ({ value: client.clientId, label: `${client.code} — ${client.name}` }))} onSearch={(value) => void onClientSearch(value)} />
            </Form.Item>
            <Form.Item name="securityId" label="Security" rules={[{ required: true, whitespace: true, message: 'Security is required.' }]}>
              <AutoComplete disabled={editingDraft !== null} filterOption={false} options={securities.map((security) => ({ value: security.securityId, label: `${security.japaneseName} | ${security.bbgDisplay} | ${security.internalCode} | ${security.isin}` }))} onSearch={(value) => void onSecuritySearch(value)} onSelect={(securityId) => void applyDefaults(securityId)} />
            </Form.Item>
            <Form.Item name="categoryName" label="Category"><Input disabled /></Form.Item>
            <Form.Item name="contactOwnerName" label="Contact Owner"><Input disabled /></Form.Item>
            <Form.Item name="assignedTraderId" label="Assigned Trader" rules={[{ required: true, message: 'Assigned Trader is required.' }]}>
              <Select allowClear options={traders.map((trader) => ({ value: trader.userId, label: trader.name }))} />
            </Form.Item>
            <Form.Item name="notional" label="Notional (MM)" rules={[
              { validator: (_, value) => value === undefined || value === null || value > 0 ? Promise.resolve() : Promise.reject(new Error('Notional must be greater than zero.')) },
              { required: true, message: 'Notional is required for Confirm.' },
            ]}>
              <InputNumber min={0} precision={2} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="standardSettlementDate" label="Standard Settlement"><Input type="date" disabled /></Form.Item>
            <Form.Item name="settlementDate" label="Settlement Date" rules={[{ required: true, message: 'Settlement date is required for Confirm.' }]}><Input type="date" /></Form.Item>
            <Form.Item name="salesAndTradingMessage" label="Sales / Trading Message"><Input.TextArea rows={3} /></Form.Item>
            <Space wrap>
              <Button loading={isMutating} onClick={() => void saveDraft()}>Save Draft</Button>
              <Button type="primary" loading={isMutating} onClick={() => void confirm()}>Confirm</Button>
              {editingDraft && <Popconfirm title="Discard this draft?" onConfirm={() => void discard()}><Button danger loading={isMutating}>Discard</Button></Popconfirm>}
            </Space>
          </Form>
        </Spin>
      </Card>

      <Card className="rfq-grid-card" title="Active RFQs" extra={<Button onClick={() => void onReload()}>Reload</Button>}>
        {isError && <Alert type="error" showIcon message="RFQs could not be loaded." className="grid-alert" />}
        <Spin spinning={isLoading}>
          <div className="rfq-grid" data-testid="rfq-grid">
            <AgGridReact<SalesRfq> rowData={rfqs} columnDefs={columns} getRowId={({ data }) => String(data.caseId)} onRowClicked={editSelectedDraft} rowClassRules={{ 'editable-draft-row': ({ data }) => data?.revisionStatus === 'Draft' }} defaultColDef={{ sortable: true, filter: true, resizable: true }} />
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
  const [updateDraft, updateState] = useUpdateDraftMutation()
  const [confirmNewRfq, confirmNewState] = useConfirmNewRfqMutation()
  const [confirmDraft, confirmState] = useConfirmDraftMutation()
  const [discardDraft, discardState] = useDiscardDraftMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqDefaultsQuery()
  const [clients, setClients] = useState<ClientSearchResult[]>([])
  const [securities, setSecurities] = useState<SecuritySearchResult[]>([])
  const health = healthQuery.isLoading ? 'checking' : healthQuery.isError || healthQuery.data?.status !== 'ok' ? 'error' : 'ok'
  const systemDate = systemDateQuery.isLoading ? 'checking' : systemDateQuery.isError ? 'unavailable' : systemDateQuery.data?.date
  const handleClientSearch = async (query: string) => setClients(query.trim() ? await searchClients(query).unwrap() : [])
  const handleSecuritySearch = async (query: string) => setSecurities(query.trim() ? await searchSecurities(query).unwrap() : [])
  const isMutating = [createState, updateState, confirmNewState, confirmState, discardState].some((state) => state.isLoading)

  return (
    <AppShell health={health} systemDate={systemDate}>
      <SalesScreen
        rfqs={rfqsQuery.data ?? []}
        clients={clients}
        securities={securities}
        traders={(tradersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))}
        isLoading={rfqsQuery.isLoading || rfqsQuery.isFetching}
        isError={rfqsQuery.isError}
        isMutating={isMutating}
        onClientSearch={handleClientSearch}
        onSecuritySearch={handleSecuritySearch}
        onResolveDefaults={(securityId) => resolveDefaults({ securityId }).unwrap()}
        onCreate={(request) => createDraft(request).unwrap().then(() => undefined)}
        onUpdate={(caseId, body) => updateDraft({ caseId, body }).unwrap().then(() => undefined)}
        onConfirmNew={(request) => confirmNewRfq(request).unwrap().then(() => undefined)}
        onConfirmDraft={(caseId, body) => confirmDraft({ caseId, body }).unwrap().then(() => undefined)}
        onDiscard={(caseId, expectedVersion) => discardDraft({ caseId, expectedVersion }).unwrap()}
        onReload={rfqsQuery.refetch}
      />
    </AppShell>
  )
}
