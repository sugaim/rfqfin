import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Alert, AutoComplete, Button, Card, Drawer, Form, Input, InputNumber, Layout, Menu, Popconfirm, Select, Space, Spin, Tag, Typography, message } from 'antd'
import type { CellEditRequestEvent, ColDef, GridApi, RowClickedEvent } from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import {
  useAssignTraderMutation,
  useBulkConfirmAmendmentsMutation, useBulkDiscardAmendmentsMutation,
  useCancelRfqMutation, useConfirmAmendmentMutation, useCreateFromExistingMutation,
  useDiscardAmendmentMutation, useGetEodQuery, useGetEventsQuery, useReopenRfqMutation,
  useSaveAmendmentMutation, useSearchRfqsQuery, useWithdrawQuoteMutation,
  useScratchPriceMutation,
  useGetGridConfigQuery, useSaveGridConfigMutation,
  useBulkCloseRfqsMutation, useCalculateWorkingQuoteMutation, useChangeContactOwnerMutation,
  useChangeWorkingQuoteModeMutation, useCloseRfqMutation,
  useConfirmDraftMutation, useConfirmNewRfqMutation, useConfirmQuoteMutation, useCorrectRfqOutcomeMutation, useCreateDraftMutation,
  useDiscardDraftMutation, useGetActiveSalesRfqsQuery, useGetActiveTraderRfqsQuery,
  useGetMeQuery, useGetHealthQuery,
  useGetBusinessDateQuery, useGetQuoteExpiryQuery,
  useGetAssignableTradersQuery, useGetContactOwnerCandidatesQuery,
  useLazyResolveRfqCreationContextQuery,
  useLazySearchClientsQuery, useLazySearchSecuritiesQuery, usePickUpRfqMutation,
  usePresentQuoteMutation, useReleaseRfqMutation, useTakeOverRfqMutation,
  useUnpresentQuoteMutation, useUpdateDraftMutation, useUpdateManualWorkingQuoteMutation,
  useUpdateSalesMemoMutation, useUpdateTraderMemoMutation,
  type BulkCloseItemResult,
  type ClientSearchResult, type CreateDraftRequest, type RfqCreationContext,
  type SalesRfq, type SecuritySearchResult, type TraderRfq, type UpdateDraftRequest,
} from './services/api'

type RfqOutcome = 'Hit' | 'Away'
type UserOption = { userId: string; name: string }
type CloseItem = { caseId: number; expectedCurrentVersion: number }

const navigationItems = ['Sales', 'Trader', 'EOD'].map((label) => ({ key: label.toLowerCase(), label }))
const million = 1_000_000
const formatPercent = (value: unknown) =>
  value == null ? '' : `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 })}%`
const formatBasisPoints = (value: unknown) =>
  value == null ? '' : `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 })} bp`

const toAbsoluteNotional = (notionalInMillions?: number) =>
  notionalInMillions === undefined
    ? undefined
    : Number((notionalInMillions * million).toFixed(2))

export interface AppShellProps {
  health: 'checking' | 'ok' | 'error'
  businessDate?: string
  children?: ReactNode
  activeView?: string
  currentUserId?: string
  onNavigate?: (view: string) => void
  onIdentityChange?: (userId: string) => void
  pendingUpdates?: number
  onRefreshUpdates?: () => void
}

export function AppShell({
  health,
  businessDate,
  children,
  activeView = 'sales',
  currentUserId = 'sales-dev',
  onNavigate,
  onIdentityChange,
  pendingUpdates = 0,
  onRefreshUpdates,
}: AppShellProps) {
  const healthPresentation = {
    checking: { color: 'processing', text: 'API checking' },
    ok: { color: 'success', text: 'API healthy' },
    error: { color: 'error', text: 'API unavailable' },
  }[health]

  return (
    <Layout className="app-shell">
      <Layout.Header className="app-header">
        <Typography.Title level={3} className="app-title">RFQ</Typography.Title>
        <Menu theme="dark" mode="horizontal" selectedKeys={[activeView]} items={navigationItems} onClick={({ key }) => onNavigate?.(key)} className="app-navigation" />
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
        {pendingUpdates > 0 && <Button size="small" type="primary" onClick={onRefreshUpdates}>Updates Available ({pendingUpdates})</Button>}
        <Tag color={healthPresentation.color}>{healthPresentation.text}</Tag>
      </Layout.Header>
      <Layout.Content className="app-content">
        <Typography.Title level={2}>{activeView === 'trader' ? 'Trader' : activeView === 'eod' ? 'EOD' : 'Sales'}</Typography.Title>
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
  traders: UserOption[]
  users: UserOption[]
  currentUserId: string
  isLoading: boolean
  isError: boolean
  isMutating: boolean
  onClientSearch: (query: string) => void | Promise<void>
  onSecuritySearch: (query: string) => void | Promise<void>
  onResolveDefaults: (securityId: string) => Promise<RfqCreationContext>
  onCreate: (request: CreateDraftRequest) => Promise<void>
  onUpdate: (caseId: number, request: UpdateDraftRequest) => Promise<void>
  onConfirmNew: (request: CreateDraftRequest) => Promise<void>
  onConfirmDraft: (caseId: number, request: UpdateDraftRequest) => Promise<void>
  onDiscard: (caseId: number, expectedVersion: number) => Promise<void>
  onPresent: (caseId: number, expectedCurrentVersion: number) => Promise<void>
  onUnpresent: (caseId: number, expectedCurrentVersion: number) => Promise<void>
  onClose: (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) => Promise<void>
  onBulkClose: (items: CloseItem[], outcome: RfqOutcome) => Promise<BulkCloseItemResult[]>
  onCorrectOutcome: (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) => Promise<void>
  onChangeContactOwner: (caseId: number, targetUserId: string, expectedCurrentVersion: number) => Promise<void>
  onUpdateMemo: (caseId: number, memo: string, expectedVersion: number) => Promise<void>
  onReload: () => void | Promise<unknown>
  onSaveAmendment?: (row: SalesRfq, notional: number | null, settlementDate: string | null, message: string) => Promise<void>
  onConfirmAmendment?: (row: SalesRfq) => Promise<void>
  onDiscardAmendment?: (row: SalesRfq) => Promise<void>
  onCreateFromExisting?: (caseId: number) => Promise<void>
  onCancel?: (row: SalesRfq) => Promise<void>
  onReopen?: (row: SalesRfq) => Promise<void>
  onBulkConfirmAmendments?: (rows: SalesRfq[]) => Promise<void>
  onBulkDiscardAmendments?: (rows: SalesRfq[]) => Promise<void>
  gridConfigJson?: string
  onSaveGridConfig?: (configJson: string) => Promise<void>
}

export function SalesScreen(props: SalesScreenProps) {
  const {
    rfqs, clients, securities, traders, users, currentUserId, isLoading, isError, isMutating,
    onClientSearch, onSecuritySearch, onResolveDefaults, onCreate, onUpdate,
    onConfirmNew, onConfirmDraft, onDiscard, onPresent, onUnpresent, onClose,
    onBulkClose, onCorrectOutcome, onChangeContactOwner, onUpdateMemo, onReload,
    onSaveAmendment, onConfirmAmendment, onDiscardAmendment, onCreateFromExisting,
    onCancel, onReopen,
    onBulkConfirmAmendments, onBulkDiscardAmendments,
    gridConfigJson, onSaveGridConfig,
  } = props
  const [form] = Form.useForm<RfqFormValues>()
  const [editingDraft, setEditingDraft] = useState<SalesRfq | null>(null)
  const [selectedRfq, setSelectedRfq] = useState<SalesRfq | null>(null)
  const [selectedRows, setSelectedRows] = useState<SalesRfq[]>([])
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [memoDraft, setMemoDraft] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)
  const [defaultsError, setDefaultsError] = useState(false)
  const [isResolvingDefaults, setIsResolvingDefaults] = useState(false)
  const [salesGridApi, setSalesGridApi] = useState<GridApi<SalesRfq> | null>(null)
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
      valueGetter: ({ data }) => data?.draftNotional ?? data?.notional,
      valueFormatter: ({ value }) =>
        value === null || value === undefined
          ? ''
          : (Number(value) / million).toLocaleString(),
      editable: ({ data }) => data?.rfqStatus === 'Active' || data?.rfqStatus === 'Presented',
      cellEditor: 'agNumberCellEditor',
      cellClass: ({ data }) => data?.draftNotional != null ? 'amendment-changed-cell' : undefined,
    },
    { field: 'assignedTraderId', headerName: 'Trader', minWidth: 130 },
    {
      field: 'settlementDate', headerName: 'Settlement', minWidth: 130,
      valueGetter: ({ data }) => data?.draftSettlementDate ?? data?.settlementDate,
      editable: ({ data }) => data?.rfqStatus === 'Active' || data?.rfqStatus === 'Presented',
      cellClass: ({ data }) => data?.draftSettlementDate != null ? 'amendment-changed-cell' : undefined,
    },
    {
      field: 'salesAndTradingMessage', headerName: 'Message', minWidth: 180,
      valueGetter: ({ data }) => data?.draftSalesAndTradingMessage ?? data?.salesAndTradingMessage,
      editable: ({ data }) => data?.rfqStatus === 'Active' || data?.rfqStatus === 'Presented',
      cellClass: ({ data }) => data?.draftSalesAndTradingMessage != null ? 'amendment-changed-cell' : undefined,
    },
    { field: 'rfqStatus', headerName: 'RFQ Status', minWidth: 130 },
    { field: 'revisionStatus', headerName: 'Revision', minWidth: 120 },
    { field: 'quoteStatus', headerName: 'Quote Status', minWidth: 130 },
    { field: 'quoteRequestReason', headerName: 'Reason', minWidth: 120 },
    {
      field: 'currentQuoteId',
      headerName: 'Current Quote',
      minWidth: 140,
      valueFormatter: ({ value }) => value ? String(value).slice(0, 8) : '',
    },
    { field: 'createdAt', headerName: 'Created', minWidth: 190, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : '' },
  ], [])

  const applyDefaults = async (securityId: string) => {
    setDefaultsError(false)
    setIsResolvingDefaults(true)
    try {
      const defaults = await onResolveDefaults(securityId)
      form.setFieldsValue({
        categoryName: defaults.categoryName,
        contactOwnerName: currentUserId,
        assignedTraderId: defaults.defaultAssignedTraderId,
        standardSettlementDate: defaults.standardSettlementDate,
        settlementDate: defaults.standardSettlementDate,
      })
    } catch {
      setDefaultsError(true)
    } finally {
      setIsResolvingDefaults(false)
    }
  }

  useEffect(() => {
    if (!salesGridApi || !gridConfigJson) return
    try {
      salesGridApi.applyColumnState({ state: JSON.parse(gridConfigJson), applyOrder: true })
    } catch { /* ignore obsolete/invalid local layout */ }
  }, [salesGridApi, gridConfigJson])

  const toCreateRequest = (values: RfqFormValues): CreateDraftRequest => ({
    clientId: values.clientId,
    securityId: values.securityId,
    notional: toAbsoluteNotional(values.notional),
    settlementDate: values.settlementDate,
    standardSettlementDate: values.standardSettlementDate,
    salesAndTradingMessage: values.salesAndTradingMessage ?? '',
    assignedTraderId: values.assignedTraderId,
  })

  const toUpdateRequest = (values: RfqFormValues, draft: SalesRfq): UpdateDraftRequest => ({
    notional: toAbsoluteNotional(values.notional),
    settlementDate: values.settlementDate,
    standardSettlementDate: values.standardSettlementDate,
    salesAndTradingMessage: values.salesAndTradingMessage ?? '',
    assignedTraderId: values.assignedTraderId,
    expectedVersion: draft.version,
  })

  const finishAction = async () => {
    setEditingDraft(null)
    setSelectedRfq(null)
    setSelectedRows([])
    setTargetContactOwnerId(undefined)
    setMemoDraft('')
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
      void message.error('Conflict or validation error. The latest RFQ has been reloaded.')
      await onReload()
    }
  }

  const saveDraft = async () => {
    let required: Partial<RfqFormValues>
    try {
      required = await form.validateFields([
        'clientId', 'securityId', 'assignedTraderId', 'settlementDate',
      ])
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
    setSelectedRfq(null)
    form.resetFields()
    setActionError(null)
    setDefaultsError(false)
  }

  const editSelectedDraft = ({ data }: RowClickedEvent<SalesRfq>) => {
    if (!data) return
    setSelectedRfq(data)
    setTargetContactOwnerId(undefined)
    setMemoDraft(data.salesMemo)
    if (data.revisionStatus !== 'Draft') {
      setEditingDraft(null)
      form.resetFields()
      return
    }
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

  const isContactOwner = selectedRfq?.contactOwnerId === currentUserId
  const canClose = Boolean(
    selectedRfq
    && isContactOwner
    && (selectedRfq.rfqStatus === 'Active' || selectedRfq.rfqStatus === 'Presented')
    && selectedRfq.quoteStatus === 'Quoted',
  )
  const canCorrectOutcome = Boolean(
    selectedRfq
    && isContactOwner
    && (selectedRfq.rfqStatus === 'Hit' || selectedRfq.rfqStatus === 'Away'),
  )
  const bulkClose = async (outcome: RfqOutcome) => {
    setActionError(null)
    try {
      const results = await onBulkClose(
        selectedRows.map((row) => ({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        })),
        outcome,
      )
      const details = results.map((result) =>
        `Case ${result.caseId}: ${result.result}${result.error ? ` (${result.error})` : ''}`)
      void message.info(`Bulk ${outcome} — ${details.join('; ')}`)
      await finishAction()
    } catch {
      setActionError('The bulk close could not be completed. Reload and try again.')
    }
  }

  const editAmendment = async (event: CellEditRequestEvent<SalesRfq>) => {
    if (!event.data || !onSaveAmendment) return
    const field = event.colDef.field
    const row = event.data
    await runAction(() => onSaveAmendment(
      row,
      field === 'notional' ? Number(event.newValue) * million : row.draftNotional ?? row.notional,
      field === 'settlementDate' ? String(event.newValue) : row.draftSettlementDate ?? row.settlementDate,
      field === 'salesAndTradingMessage' ? String(event.newValue ?? '') : row.draftSalesAndTradingMessage ?? row.salesAndTradingMessage,
    ))
  }

  return (
    <div className="sales-workspace">
      <Card className="work-pane" title={editingDraft ? `Draft Case ${editingDraft.caseId}` : 'New RFQ'} extra={<Button onClick={startNew}>New</Button>}>
        <Typography.Paragraph type="secondary">Notional may be omitted when saving. Confirm requires notional.</Typography.Paragraph>
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

      <Card
        className="rfq-grid-card"
        title="RFQs"
        extra={(
          <Space wrap>
            <Button
              disabled={!selectedRfq
                || selectedRfq.contactOwnerId !== currentUserId
                || selectedRfq.rfqStatus !== 'Active'
                || selectedRfq.quoteStatus !== 'Quoted'
                || isMutating}
              onClick={() => selectedRfq
                && void runAction(() => onPresent(selectedRfq.caseId, selectedRfq.currentVersion))}
            >
              Present
            </Button>
            <Button
              disabled={!selectedRfq
                || selectedRfq.contactOwnerId !== currentUserId
                || selectedRfq.rfqStatus !== 'Presented'
                || isMutating}
              onClick={() => selectedRfq
                && void runAction(() => onUnpresent(selectedRfq.caseId, selectedRfq.currentVersion))}
            >
              Unpresent
            </Button>
            <Popconfirm
              title={selectedRfq ? `Close Case ${selectedRfq.caseId} as Hit?` : 'Close as Hit?'}
              onConfirm={() => selectedRfq
                && void runAction(() => onClose(
                  selectedRfq.caseId,
                  'Hit',
                  selectedRfq.currentVersion,
                ))}
            >
              <Button disabled={!canClose || isMutating}>Hit</Button>
            </Popconfirm>
            <Popconfirm
              title={selectedRfq ? `Close Case ${selectedRfq.caseId} as Away?` : 'Close as Away?'}
              onConfirm={() => selectedRfq
                && void runAction(() => onClose(
                  selectedRfq.caseId,
                  'Away',
                  selectedRfq.currentVersion,
                ))}
            >
              <Button disabled={!canClose || isMutating}>Away</Button>
            </Popconfirm>
            <Popconfirm
              title={selectedRfq
                ? `Correct Case ${selectedRfq.caseId} to ${selectedRfq.rfqStatus === 'Hit' ? 'Away' : 'Hit'}?`
                : 'Correct outcome?'}
              onConfirm={() => selectedRfq
                && void runAction(() => onCorrectOutcome(
                  selectedRfq.caseId,
                  selectedRfq.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                  selectedRfq.currentVersion,
                ))}
            >
              <Button disabled={!canCorrectOutcome || isMutating}>Correct Outcome</Button>
            </Popconfirm>
            <Popconfirm
              title={`Bulk close ${selectedRows.length} selected RFQs as Hit?`}
              onConfirm={() => void bulkClose('Hit')}
            >
              <Button disabled={selectedRows.length === 0 || isMutating}>Bulk Hit</Button>
            </Popconfirm>
            <Popconfirm
              title={`Bulk close ${selectedRows.length} selected RFQs as Away?`}
              onConfirm={() => void bulkClose('Away')}
            >
              <Button disabled={selectedRows.length === 0 || isMutating}>Bulk Away</Button>
            </Popconfirm>
            <Button onClick={() => void onReload()}>Reload</Button>
            <Button disabled={!salesGridApi || !onSaveGridConfig} onClick={() => salesGridApi && onSaveGridConfig && void onSaveGridConfig(JSON.stringify(salesGridApi.getColumnState()))}>Save Layout</Button>
            <Button disabled={!selectedRfq || !onCreateFromExisting} onClick={() => selectedRfq && void runAction(() => onCreateFromExisting!(selectedRfq.caseId))}>Create New from Existing</Button>
            <Button disabled={!selectedRfq?.draftRevisionId || !onConfirmAmendment} onClick={() => selectedRfq && void runAction(() => onConfirmAmendment!(selectedRfq))}>Confirm Amendment</Button>
            <Button disabled={!selectedRfq?.draftRevisionId || !onDiscardAmendment} onClick={() => selectedRfq && void runAction(() => onDiscardAmendment!(selectedRfq))}>Discard Amendment</Button>
            <Button disabled={!onBulkConfirmAmendments || !selectedRows.some((row) => row.draftRevisionId)} onClick={() => void runAction(() => onBulkConfirmAmendments!(selectedRows.filter((row) => row.draftRevisionId)))}>Confirm Selected</Button>
            <Button disabled={!onBulkDiscardAmendments || !selectedRows.some((row) => row.draftRevisionId)} onClick={() => void runAction(() => onBulkDiscardAmendments!(selectedRows.filter((row) => row.draftRevisionId)))}>Discard Selected</Button>
            <Button danger disabled={!selectedRfq || !onCancel || !['Active','Presented'].includes(selectedRfq.rfqStatus)} onClick={() => selectedRfq && void runAction(() => onCancel!(selectedRfq))}>Cancel</Button>
            <Button disabled={!selectedRfq || !onReopen || selectedRfq.rfqStatus !== 'Cancelled'} onClick={() => selectedRfq && void runAction(() => onReopen!(selectedRfq))}>Reopen</Button>
          </Space>
        )}
      >
        {isError && <Alert type="error" showIcon message="RFQs could not be loaded." className="grid-alert" />}
        <Spin spinning={isLoading}>
          <div className="rfq-grid" data-testid="rfq-grid">
            <AgGridReact<SalesRfq>
              rowData={rfqs}
              onGridReady={({ api: gridApi }) => setSalesGridApi(gridApi)}
              columnDefs={columns}
              getRowId={({ data }) => String(data.caseId)}
              onRowClicked={editSelectedDraft}
              onSelectionChanged={({ api: gridApi }) => setSelectedRows(gridApi.getSelectedRows())}
              readOnlyEdit
              onCellEditRequest={(event) => void editAmendment(event)}
              rowSelection={{ mode: 'multiRow' }}
              rowClassRules={{ 'editable-draft-row': ({ data }) => data?.revisionStatus === 'Draft' }}
              defaultColDef={{ sortable: true, filter: true, resizable: true }}
            />
          </div>
        </Spin>
        {selectedRfq && (
          <Card size="small" title={`Case ${selectedRfq.caseId} details`} className="case-details">
            <Space wrap align="end">
              <div>
                <Typography.Text type="secondary">Contact Owner</Typography.Text>
                <br />
                <Select
                  aria-label="Contact Owner"
                  value={targetContactOwnerId}
                  placeholder={selectedRfq.contactOwnerId}
                  onChange={setTargetContactOwnerId}
                  disabled={!isContactOwner || isMutating}
                  options={users.map((user) => ({ value: user.userId, label: user.name }))}
                  style={{ width: 190 }}
                />
              </div>
              <Popconfirm
                title={targetContactOwnerId
                  ? `Hand off Case ${selectedRfq.caseId} to ${targetContactOwnerId}?`
                  : 'Select a Contact Owner.'}
                onConfirm={() => targetContactOwnerId
                  && void runAction(() => onChangeContactOwner(
                    selectedRfq.caseId,
                    targetContactOwnerId,
                    selectedRfq.currentVersion,
                  ))}
              >
                <Button disabled={!isContactOwner || !targetContactOwnerId || isMutating}>
                  Change Contact Owner
                </Button>
              </Popconfirm>
            </Space>
            <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
              Sales-only Memo
            </Typography.Paragraph>
            <Input.TextArea
              aria-label="Sales-only Memo"
              rows={3}
              value={memoDraft}
              onChange={(event) => setMemoDraft(event.target.value)}
            />
            <Button
              style={{ marginTop: 8 }}
              loading={isMutating}
              onClick={() => void runAction(() => onUpdateMemo(
                selectedRfq.caseId,
                memoDraft,
                selectedRfq.salesMemoVersion,
              ))}
            >
              Save Sales Memo
            </Button>
          </Card>
        )}
      </Card>
    </div>
  )
}

export interface TraderScreenProps {
  rfqs: TraderRfq[]
  traders: UserOption[]
  users: UserOption[]
  currentUserId: string
  defaultExpiryMinutes: number | null
  isLoading: boolean
  isError: boolean
  isMutating: boolean
  onPickUp: (caseId: number, expectedVersion: number, confirmed: boolean) => Promise<void>
  onRelease: (caseId: number, expectedVersion: number) => Promise<void>
  onAssign: (caseId: number, targetTraderId: string, expectedVersion: number) => Promise<void>
  onTakeOver: (caseId: number, expectedVersion: number, confirmed: boolean) => Promise<void>
  onCalculate: (
    row: TraderRfq,
    driver: string,
    value: number,
    simpleYieldSlide: number,
  ) => Promise<void>
  onChangeMode: (row: TraderRfq, mode: 'Calculated' | 'Manual') => Promise<void>
  onUpdateManual: (
    row: TraderRfq,
    price: number | null,
    finalSimpleYield: number | null,
  ) => Promise<void>
  onConfirmQuote: (row: TraderRfq, expiryMinutes: number | null) => Promise<void>
  onClose: (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) => Promise<void>
  onBulkClose: (items: CloseItem[], outcome: RfqOutcome) => Promise<BulkCloseItemResult[]>
  onCorrectOutcome: (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) => Promise<void>
  onChangeContactOwner: (caseId: number, targetUserId: string, expectedCurrentVersion: number) => Promise<void>
  onUpdateMemo: (caseId: number, memo: string, expectedVersion: number) => Promise<void>
  onWithdraw?: (row: TraderRfq) => Promise<void>
  onScratchPrice?: (input: { securityId: string; settlementDate: string; driver: string; value: number; simpleYieldSlide: number }) => Promise<unknown>
  onReload: () => void | Promise<unknown>
}

export function TraderScreen({
  rfqs,
  traders,
  users,
  currentUserId,
  defaultExpiryMinutes,
  isLoading,
  isError,
  isMutating,
  onPickUp,
  onRelease,
  onAssign,
  onTakeOver,
  onCalculate,
  onChangeMode,
  onUpdateManual,
  onConfirmQuote,
  onClose,
  onBulkClose,
  onCorrectOutcome,
  onChangeContactOwner,
  onUpdateMemo,
  onWithdraw,
  onScratchPrice,
  onReload,
}: TraderScreenProps) {
  const [selected, setSelected] = useState<TraderRfq | null>(null)
  const [selectedRows, setSelectedRows] = useState<TraderRfq[]>([])
  const [targetTraderId, setTargetTraderId] = useState<string>()
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [memoDraft, setMemoDraft] = useState('')
  const [actionError, setActionError] = useState(false)
  const [calculationStatus, setCalculationStatus] = useState<Record<number, string>>({})
  const [expirySelections, setExpirySelections] = useState<Record<number, string>>({})
  const [pricerOpen, setPricerOpen] = useState(false)
  const [scratchSecurity, setScratchSecurity] = useState('')
  const [scratchSettlement, setScratchSettlement] = useState('')
  const [scratchValue, setScratchValue] = useState<number>(100)
  const [scratchResult, setScratchResult] = useState<unknown>()
  const canEditQuote = (row?: TraderRfq) => Boolean(
    row
    && row.owned
    && row.assignedTraderId === currentUserId
    && row.quoteStatus === 'Requested',
  )
  const columns = useMemo<ColDef<TraderRfq>[]>(() => [
    { field: 'caseId', headerName: 'Case ID', minWidth: 110 },
    { field: 'clientName', headerName: 'Client', minWidth: 180 },
    { field: 'securityJapaneseName', headerName: 'Security Name', minWidth: 210 },
    { field: 'securityBbgDisplay', headerName: 'BBG Display', minWidth: 220 },
    { field: 'categoryId', headerName: 'Category', minWidth: 110 },
    {
      field: 'notional',
      headerName: 'Notional (MM)',
      minWidth: 140,
      valueFormatter: ({ value }) => value == null ? '' : (Number(value) / million).toLocaleString(),
    },
    { field: 'assignedTraderId', headerName: 'Assigned Trader', minWidth: 150 },
    { field: 'owned', headerName: 'Owned', minWidth: 100, valueFormatter: ({ value }) => value ? 'Yes' : 'No' },
    { field: 'rfqStatus', headerName: 'RFQ Status', minWidth: 130 },
    { field: 'quoteStatus', headerName: 'Quote Status', minWidth: 130 },
    { field: 'quoteRequestReason', headerName: 'Reason', minWidth: 120 },
    { field: 'settlementDate', headerName: 'Settlement', minWidth: 130 },
    { field: 'workingQuoteMode', headerName: 'Mode', minWidth: 110 },
    {
      field: 'currentQuoteId',
      headerName: 'Confirmed Quote',
      minWidth: 210,
      valueGetter: ({ data }) => data?.currentQuoteId ?? data?.closedQuoteId,
      valueFormatter: ({ value }) => value ? String(value).slice(0, 8) : '',
    },
    { field: 'confirmedAt', headerName: 'Confirmed At', minWidth: 190, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : '' },
    { field: 'expiresAt', headerName: 'Expires At', minWidth: 190, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : 'None' },
    {
      colId: 'price',
      headerName: 'Price',
      minWidth: 110,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Manual'
        ? data.manual?.price
        : data?.calculated?.price,
      editable: ({ data }) => canEditQuote(data),
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'bbgYield',
      headerName: 'BBG Yield (%)',
      minWidth: 120,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.bbgYield
        : undefined,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated',
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'baseSimpleYield',
      headerName: 'Simple Yield (%)',
      minWidth: 130,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.baseSimpleYield
        : undefined,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated',
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'gSpread',
      headerName: 'G-Spread (bp)',
      minWidth: 120,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.gSpread
        : undefined,
      valueFormatter: ({ value }) => formatBasisPoints(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated',
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'simpleYieldSlide',
      headerName: 'SY Slide (%)',
      minWidth: 110,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.simpleYieldSlide ?? 0
        : undefined,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated' && data?.calculated != null,
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'finalSimpleYield',
      headerName: 'Final Simple Yield (%)',
      minWidth: 150,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Manual'
        ? data.manual?.finalSimpleYield
        : data?.calculated?.finalSimpleYield,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Manual',
      cellEditor: 'agNumberCellEditor',
    },
    {
      headerName: 'Calc Status',
      minWidth: 120,
      valueGetter: ({ data }) => data ? calculationStatus[data.caseId] ?? 'Idle' : '',
    },
  ], [calculationStatus, currentUserId])

  const runAction = async (action: () => Promise<void>) => {
    setActionError(false)
    try {
      await action()
      setSelected(null)
      setSelectedRows([])
      setTargetTraderId(undefined)
      setTargetContactOwnerId(undefined)
      setMemoDraft('')
      await onReload()
    } catch {
      setActionError(true)
      void message.error('Conflict or validation error. The latest RFQ has been reloaded.')
      await onReload()
    }
  }

  const pickUpTargets = selectedRows.length > 1
    ? selectedRows.filter((row) => !row.owned && row.rfqStatus !== 'Hit' && row.rfqStatus !== 'Away')
    : selected && !selected.owned && selected.rfqStatus !== 'Hit' && selected.rfqStatus !== 'Away'
      ? [selected]
      : []
  const pickUp = () => runAction(async () => {
    await Promise.all(pickUpTargets.map((row) =>
      onPickUp(
        row.caseId,
        row.currentVersion,
        pickUpTargets.length > 1 || row.assignedTraderId !== currentUserId,
      )))
  })
  const release = () => selected && runAction(() =>
    onRelease(selected.caseId, selected.currentVersion))
  const assign = () => selected && targetTraderId && runAction(() =>
    onAssign(selected.caseId, targetTraderId, selected.currentVersion))
  const takeOver = () => selected && runAction(() =>
    onTakeOver(selected.caseId, selected.currentVersion, true))

  const editQuote = async (event: CellEditRequestEvent<TraderRfq>) => {
    const row = event.data
    const value = Number(event.newValue)
    if (!Number.isFinite(value)) {
      event.api.refreshCells({ rowNodes: [event.node], force: true })
      return
    }

    setCalculationStatus((current) => ({ ...current, [row.caseId]: 'Calculating' }))
    try {
      if (row.workingQuoteMode === 'Manual') {
        await onUpdateManual(
          row,
          event.column.getColId() === 'price' ? value : row.manual?.price ?? null,
          event.column.getColId() === 'finalSimpleYield'
            ? value
            : row.manual?.finalSimpleYield ?? null,
        )
      } else {
        const drivers: Record<string, string> = {
          price: 'Price',
          bbgYield: 'BbgYield',
          baseSimpleYield: 'SimpleYield',
          gSpread: 'GSpread',
        }
        const column = event.column.getColId()
        const driver = column === 'simpleYieldSlide'
          ? row.calculated?.driver
          : drivers[column]
        const driverValue = column === 'simpleYieldSlide'
          ? row.calculated?.driverValue
          : value
        if (!driver || driverValue === undefined) return
        await onCalculate(
          row,
          driver,
          driverValue,
          column === 'simpleYieldSlide'
            ? value
            : row.calculated?.simpleYieldSlide ?? 0,
        )
      }
      setCalculationStatus((current) => ({ ...current, [row.caseId]: 'Success' }))
      await onReload()
    } catch {
      setCalculationStatus((current) => ({ ...current, [row.caseId]: 'Error' }))
      event.api.refreshCells({ rowNodes: [event.node], force: true })
      void message.error('Calculation failed. The edited value was reverted.')
    }
  }

  const pickUpNeedsConfirmation = pickUpTargets.length > 1
    || pickUpTargets.some((row) => row.assignedTraderId !== currentUserId)
  const selectedExpiry = selected
    ? expirySelections[selected.caseId] ?? (defaultExpiryMinutes?.toString() ?? 'none')
    : undefined
  const canConfirmQuote = Boolean(
    selected
    && canEditQuote(selected)
    && (selected.workingQuoteMode === 'Calculated'
      ? selected.calculated != null
      : selected.manual?.price != null && selected.manual.finalSimpleYield != null),
  )
  const isContactOwner = selected?.contactOwnerId === currentUserId
  const isSelectedOpen = Boolean(
    selected && (selected.rfqStatus === 'Active' || selected.rfqStatus === 'Presented'),
  )
  const canClose = Boolean(isContactOwner && isSelectedOpen && selected?.quoteStatus === 'Quoted')
  const canCorrectOutcome = Boolean(
    isContactOwner && selected && (selected.rfqStatus === 'Hit' || selected.rfqStatus === 'Away'),
  )
  const bulkClose = async (outcome: RfqOutcome) => {
    setActionError(false)
    try {
      const results = await onBulkClose(
        selectedRows.map((row) => ({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        })),
        outcome,
      )
      const details = results.map((result) =>
        `Case ${result.caseId}: ${result.result}${result.error ? ` (${result.error})` : ''}`)
      void message.info(`Bulk ${outcome} — ${details.join('; ')}`)
      setSelected(null)
      setSelectedRows([])
      await onReload()
    } catch {
      setActionError(true)
    }
  }

  return (
    <Card
      className="trader-grid-card"
      title="RFQs"
      extra={<Button onClick={() => void onReload()}>Reload</Button>}
    >
      {isError && <Alert type="error" showIcon message="Trader RFQs could not be loaded." className="grid-alert" />}
      {actionError && <Alert type="error" showIcon message="Ownership changed or the action is not permitted. Reload and try again." className="grid-alert" />}
      <Space wrap className="ownership-actions">
        <Button onClick={() => {
          setScratchSecurity(selected?.securityId ?? '')
          setScratchSettlement(selected?.settlementDate ?? '')
          setScratchValue(selected?.calculated?.price ?? 100)
          setPricerOpen(true)
        }}>Pricer</Button>
        {pickUpNeedsConfirmation ? (
          <Popconfirm
            title={pickUpTargets.length > 1
              ? `Pick up ${pickUpTargets.length} selected RFQs?`
              : `Pick up Case ${pickUpTargets[0]?.caseId} from ${pickUpTargets[0]?.assignedTraderId}?`}
            onConfirm={() => void pickUp()}
          >
            <Button disabled={isMutating}>Pick Up</Button>
          </Popconfirm>
        ) : (
          <Button
            disabled={pickUpTargets.length === 0 || isMutating}
            onClick={() => void pickUp()}
          >
            Pick Up
          </Button>
        )}
        <Button
          disabled={!selected || !isSelectedOpen || !selected.owned || selected.assignedTraderId !== currentUserId || isMutating}
          onClick={() => void release()}
        >
          Release
        </Button>
        <Select
          aria-label="Assign to trader"
          placeholder="Assign to..."
          value={targetTraderId}
          onChange={setTargetTraderId}
          disabled={!selected || !isSelectedOpen || selected.owned || isMutating}
          options={traders.map((trader) => ({ value: trader.userId, label: trader.name }))}
          style={{ width: 180 }}
        />
        <Button
          disabled={!selected || !isSelectedOpen || selected.owned || !targetTraderId || isMutating}
          onClick={() => void assign()}
        >
          Assign
        </Button>
        <Popconfirm
          title={selected ? `Take over Case ${selected.caseId} from ${selected.assignedTraderId} as ${currentUserId}?` : 'Take over this RFQ?'}
          description="This changes the owner immediately."
          onConfirm={() => void takeOver()}
        >
          <Button
            danger
            disabled={!selected || !isSelectedOpen || !selected.owned || selected.assignedTraderId === currentUserId || isMutating}
          >
            Take Over
          </Button>
        </Popconfirm>
        <Select
          aria-label="Quote mode"
          value={selected?.workingQuoteMode}
          placeholder="Quote mode"
          disabled={!selected || !canEditQuote(selected) || isMutating}
          options={[
            { value: 'Calculated', label: 'Calculated' },
            { value: 'Manual', label: 'Manual' },
          ]}
          onChange={(mode: 'Calculated' | 'Manual') => {
            if (selected) void runAction(() => onChangeMode(selected, mode))
          }}
          style={{ width: 140 }}
        />
        <Select
          aria-label="Quote expiry"
          value={selectedExpiry}
          disabled={!selected || !canEditQuote(selected) || isMutating}
          options={[
            { value: 'none', label: 'Expiry: None' },
            { value: '5', label: 'Expiry: 5 min' },
            { value: '15', label: 'Expiry: 15 min' },
            { value: '30', label: 'Expiry: 30 min' },
          ]}
          onChange={(value) => selected && setExpirySelections((current) => ({
            ...current,
            [selected.caseId]: value,
          }))}
          style={{ width: 150 }}
        />
        <Popconfirm
          title={selected ? `Confirm quote for Case ${selected.caseId}?` : 'Confirm quote?'}
          onConfirm={() => selected && void runAction(() => onConfirmQuote(
            selected,
            selectedExpiry === 'none' ? null : Number(selectedExpiry),
          ))}
        >
          <Button type="primary" disabled={!canConfirmQuote || isMutating}>
            Confirm Quote
          </Button>
        </Popconfirm>
        <Button
          disabled={!selected || !onWithdraw || selected.rfqStatus === 'Presented' || selected.quoteStatus !== 'Quoted' || !selected.owned || selected.assignedTraderId !== currentUserId || isMutating}
          onClick={() => selected && void runAction(() => onWithdraw!(selected))}
        >Withdraw</Button>
        <Popconfirm
          title={selected ? `Close Case ${selected.caseId} as Hit?` : 'Close as Hit?'}
          onConfirm={() => selected
            && void runAction(() => onClose(selected.caseId, 'Hit', selected.currentVersion))}
        >
          <Button disabled={!canClose || isMutating}>Hit</Button>
        </Popconfirm>
        <Popconfirm
          title={selected ? `Close Case ${selected.caseId} as Away?` : 'Close as Away?'}
          onConfirm={() => selected
            && void runAction(() => onClose(selected.caseId, 'Away', selected.currentVersion))}
        >
          <Button disabled={!canClose || isMutating}>Away</Button>
        </Popconfirm>
        <Popconfirm
          title={selected
            ? `Correct Case ${selected.caseId} to ${selected.rfqStatus === 'Hit' ? 'Away' : 'Hit'}?`
            : 'Correct outcome?'}
          onConfirm={() => selected
            && void runAction(() => onCorrectOutcome(
              selected.caseId,
              selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
              selected.currentVersion,
            ))}
        >
          <Button disabled={!canCorrectOutcome || isMutating}>Correct Outcome</Button>
        </Popconfirm>
        <Popconfirm
          title={`Bulk close ${selectedRows.length} selected RFQs as Hit?`}
          onConfirm={() => void bulkClose('Hit')}
        >
          <Button disabled={selectedRows.length === 0 || isMutating}>Bulk Hit</Button>
        </Popconfirm>
        <Popconfirm
          title={`Bulk close ${selectedRows.length} selected RFQs as Away?`}
          onConfirm={() => void bulkClose('Away')}
        >
          <Button disabled={selectedRows.length === 0 || isMutating}>Bulk Away</Button>
        </Popconfirm>
      </Space>
      <Spin spinning={isLoading}>
        <div className="rfq-grid" data-testid="trader-rfq-grid">
          <AgGridReact<TraderRfq>
            rowData={rfqs}
            columnDefs={columns}
            getRowId={({ data }) => String(data.caseId)}
            onRowClicked={({ data }) => {
              setSelected(data ?? null)
              setTargetTraderId(undefined)
              setTargetContactOwnerId(undefined)
              setMemoDraft(data?.traderMemo ?? '')
              setActionError(false)
            }}
            onSelectionChanged={({ api: gridApi }) =>
              setSelectedRows(gridApi.getSelectedRows())}
            readOnlyEdit
            onCellEditRequest={(event) => void editQuote(event)}
            rowSelection={{ mode: 'multiRow' }}
            defaultColDef={{ sortable: true, filter: true, resizable: true }}
          />
        </div>
      </Spin>
      {selected && (
        <Card size="small" title={`Case ${selected.caseId} details`} className="case-details">
          <Space wrap align="end">
            <div>
              <Typography.Text type="secondary">Contact Owner</Typography.Text>
              <br />
              <Select
                aria-label="Contact Owner"
                value={targetContactOwnerId}
                placeholder={selected.contactOwnerId}
                onChange={setTargetContactOwnerId}
                disabled={!isContactOwner || isMutating}
                options={users.map((user) => ({ value: user.userId, label: user.name }))}
                style={{ width: 190 }}
              />
            </div>
            <Popconfirm
              title={targetContactOwnerId
                ? `Hand off Case ${selected.caseId} to ${targetContactOwnerId}?`
                : 'Select a Contact Owner.'}
              onConfirm={() => targetContactOwnerId
                && void runAction(() => onChangeContactOwner(
                  selected.caseId,
                  targetContactOwnerId,
                  selected.currentVersion,
                ))}
            >
              <Button disabled={!isContactOwner || !targetContactOwnerId || isMutating}>
                Change Contact Owner
              </Button>
            </Popconfirm>
          </Space>
          <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
            Trader-only Memo
          </Typography.Paragraph>
          <Input.TextArea
            aria-label="Trader-only Memo"
            rows={3}
            value={memoDraft}
            onChange={(event) => setMemoDraft(event.target.value)}
          />
          <Button
            style={{ marginTop: 8 }}
            loading={isMutating}
            onClick={() => void runAction(() => onUpdateMemo(
              selected.caseId,
              memoDraft,
              selected.traderMemoVersion,
            ))}
          >
            Save Trader Memo
          </Button>
        </Card>
      )}
      <Drawer title="Independent Pricer" open={pricerOpen} onClose={() => setPricerOpen(false)}>
        <Typography.Paragraph type="secondary">Scratch values are independent and are never applied back to the RFQ.</Typography.Paragraph>
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input aria-label="Pricer Security" placeholder="Security ID" value={scratchSecurity} onChange={(event) => setScratchSecurity(event.target.value)} />
          <Input aria-label="Pricer Settlement" type="date" value={scratchSettlement} onChange={(event) => setScratchSettlement(event.target.value)} />
          <InputNumber aria-label="Pricer Value" value={scratchValue} onChange={(value) => setScratchValue(Number(value ?? 0))} style={{ width: '100%' }} />
          <Button type="primary" disabled={!onScratchPrice || !scratchSecurity || !scratchSettlement} onClick={() => onScratchPrice && void onScratchPrice({ securityId: scratchSecurity, settlementDate: scratchSettlement, driver: 'Price', value: scratchValue, simpleYieldSlide: 0 }).then(setScratchResult)}>Calculate</Button>
          {scratchResult != null && <pre>{JSON.stringify(scratchResult, null, 2)}</pre>}
        </Space>
      </Drawer>
    </Card>
  )
}

export function App() {
  const configuredIdentity = window.localStorage.getItem('rfq-development-user') ?? 'sales-dev'
  const [activeView, setActiveView] = useState(
    configuredIdentity.startsWith('trader-') ? 'trader' : 'sales',
  )
  const [refreshEventId, setRefreshEventId] = useState(0)
  const healthQuery = useGetHealthQuery()
  const businessDateQuery = useGetBusinessDateQuery()
  const currentUserQuery = useGetMeQuery()
  const rfqsQuery = useGetActiveSalesRfqsQuery(undefined, { skip: activeView !== 'sales' })
  const traderRfqsQuery = useGetActiveTraderRfqsQuery(undefined, { skip: activeView !== 'trader' })
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const quoteExpiryQuery = useGetQuoteExpiryQuery()
  const eventsQuery = useGetEventsQuery(refreshEventId)
  const eodQuery = useGetEodQuery(businessDateQuery.data?.date ?? '2026-09-21', { skip: activeView !== 'eod' })
  const pastQuery = useSearchRfqsQuery(undefined, { skip: activeView !== 'eod' })
  const gridConfigQuery = useGetGridConfigQuery({ screenId: 'sales', configKey: 'main' }, { skip: activeView !== 'sales' })
  const [createDraft, createState] = useCreateDraftMutation()
  const [updateDraft, updateState] = useUpdateDraftMutation()
  const [confirmNewRfq, confirmNewState] = useConfirmNewRfqMutation()
  const [confirmDraft, confirmState] = useConfirmDraftMutation()
  const [discardDraft, discardState] = useDiscardDraftMutation()
  const [pickUpRfq, pickUpState] = usePickUpRfqMutation()
  const [releaseRfq, releaseState] = useReleaseRfqMutation()
  const [assignTrader, assignState] = useAssignTraderMutation()
  const [takeOverRfq, takeOverState] = useTakeOverRfqMutation()
  const [calculateWorkingQuote, calculateState] = useCalculateWorkingQuoteMutation()
  const [changeWorkingQuoteMode, changeModeState] = useChangeWorkingQuoteModeMutation()
  const [updateManualWorkingQuote, updateManualState] = useUpdateManualWorkingQuoteMutation()
  const [confirmQuote, confirmQuoteState] = useConfirmQuoteMutation()
  const [presentQuote, presentState] = usePresentQuoteMutation()
  const [unpresentQuote, unpresentState] = useUnpresentQuoteMutation()
  const [closeRfq, closeState] = useCloseRfqMutation()
  const [bulkCloseRfqs, bulkCloseState] = useBulkCloseRfqsMutation()
  const [correctRfqOutcome, correctOutcomeState] = useCorrectRfqOutcomeMutation()
  const [changeContactOwner, changeContactOwnerState] = useChangeContactOwnerMutation()
  const [updateSalesMemo, updateSalesMemoState] = useUpdateSalesMemoMutation()
  const [updateTraderMemo, updateTraderMemoState] = useUpdateTraderMemoMutation()
  const [saveAmendment, saveAmendmentState] = useSaveAmendmentMutation()
  const [confirmAmendment, confirmAmendmentState] = useConfirmAmendmentMutation()
  const [discardAmendment, discardAmendmentState] = useDiscardAmendmentMutation()
  const [createFromExisting, createFromExistingState] = useCreateFromExistingMutation()
  const [cancelRfq, cancelState] = useCancelRfqMutation()
  const [reopenRfq, reopenState] = useReopenRfqMutation()
  const [withdrawQuote, withdrawState] = useWithdrawQuoteMutation()
  const [bulkConfirmAmendments, bulkConfirmAmendmentsState] = useBulkConfirmAmendmentsMutation()
  const [bulkDiscardAmendments, bulkDiscardAmendmentsState] = useBulkDiscardAmendmentsMutation()
  const [scratchPrice] = useScratchPriceMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqCreationContextQuery()
  const [clients, setClients] = useState<ClientSearchResult[]>([])
  const [securities, setSecurities] = useState<SecuritySearchResult[]>([])
  const health = healthQuery.isLoading ? 'checking' : healthQuery.isError || healthQuery.data?.status !== 'ok' ? 'error' : 'ok'
  const businessDate = businessDateQuery.isLoading ? 'checking' : businessDateQuery.isError ? 'unavailable' : businessDateQuery.data?.date
  const handleClientSearch = async (query: string) => setClients(query.trim() ? await searchClients(query).unwrap() : [])
  const handleSecuritySearch = async (query: string) => setSecurities(query.trim() ? await searchSecurities(query).unwrap() : [])
  const isMutating = [
    createState,
    updateState,
    confirmNewState,
    confirmState,
    discardState,
    presentState,
    unpresentState,
    closeState,
    bulkCloseState,
    correctOutcomeState,
    changeContactOwnerState,
    updateSalesMemoState,
    saveAmendmentState, confirmAmendmentState, discardAmendmentState,
    createFromExistingState, cancelState, reopenState,
    bulkConfirmAmendmentsState, bulkDiscardAmendmentsState,
  ].some((state) => state.isLoading)
  const isOwnershipMutating = [
    pickUpState,
    releaseState,
    assignState,
    takeOverState,
    calculateState,
    changeModeState,
    updateManualState,
    confirmQuoteState,
    closeState,
    bulkCloseState,
    correctOutcomeState,
    changeContactOwnerState,
    updateTraderMemoState,
    withdrawState,
  ].some((state) => state.isLoading)
  const users = (usersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))
  const traders = (tradersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))

  const closeCase = (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) =>
    closeRfq({ caseId, outcome, expectedCurrentVersion }).unwrap().then(() => undefined)
  const bulkCloseCases = (items: CloseItem[], outcome: RfqOutcome) =>
    bulkCloseRfqs({ items, outcome }).unwrap()
  const correctOutcome = (
    caseId: number,
    outcome: RfqOutcome,
    expectedCurrentVersion: number,
  ) => correctRfqOutcome({ caseId, outcome, expectedCurrentVersion }).unwrap().then(() => undefined)
  const handOffContactOwner = (
    caseId: number,
    targetUserId: string,
    expectedCurrentVersion: number,
  ) => changeContactOwner({
    caseId,
    targetUserId,
    expectedCurrentVersion,
    confirmed: true,
  }).unwrap().then(() => undefined)

  const changeIdentity = (userId: string) => {
    window.localStorage.setItem('rfq-development-user', userId)
    window.location.reload()
  }

  const refreshUpdates = async () => {
    if (activeView === 'sales') await rfqsQuery.refetch()
    if (activeView === 'trader') await traderRfqsQuery.refetch()
    const latest = Math.max(0, ...(eventsQuery.data ?? []).map((event) => event.eventId))
    setRefreshEventId(latest)
  }

  useEffect(() => {
    if (typeof EventSource === 'undefined') return undefined
    const source = new EventSource(`/api/events/stream?after=${refreshEventId}`)
    source.addEventListener('changed', () => { void eventsQuery.refetch() })
    return () => source.close()
  }, [refreshEventId, eventsQuery.refetch])

  return (
    <AppShell
      health={health}
      businessDate={businessDate}
      activeView={activeView}
      currentUserId={currentUserQuery.data?.userId ?? configuredIdentity}
      onNavigate={setActiveView}
      onIdentityChange={changeIdentity}
      pendingUpdates={eventsQuery.data?.length ?? 0}
      onRefreshUpdates={() => void refreshUpdates()}
    >
      {activeView === 'sales' && (
        <SalesScreen
          rfqs={rfqsQuery.data ?? []}
          clients={clients}
          securities={securities}
          traders={traders}
          users={users}
          currentUserId={currentUserQuery.data?.userId ?? configuredIdentity}
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
          onPresent={(caseId, expectedCurrentVersion) =>
            presentQuote({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
          onUnpresent={(caseId, expectedCurrentVersion) =>
            unpresentQuote({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
          onClose={closeCase}
          onBulkClose={bulkCloseCases}
          onCorrectOutcome={correctOutcome}
          onChangeContactOwner={handOffContactOwner}
          onUpdateMemo={(caseId, memo, expectedVersion) =>
            updateSalesMemo({ caseId, memo, expectedVersion }).unwrap().then(() => undefined)}
          onSaveAmendment={(row, notional, settlementDate, salesAndTradingMessage) =>
            saveAmendment({
              caseId: row.caseId, notional, settlementDate, salesAndTradingMessage,
              expectedCurrentVersion: row.currentVersion,
              expectedDraftVersion: row.draftVersion ?? null,
            }).unwrap().then(() => undefined)}
          onConfirmAmendment={(row) => confirmAmendment({
            caseId: row.caseId,
            expectedCurrentVersion: row.currentVersion,
            expectedDraftVersion: row.draftVersion!,
          }).unwrap().then(() => undefined)}
          onDiscardAmendment={(row) => discardAmendment({
            caseId: row.caseId,
            expectedCurrentVersion: row.currentVersion,
            expectedDraftVersion: row.draftVersion!,
          }).unwrap().then(() => undefined)}
          onCreateFromExisting={(caseId) => createFromExisting(caseId).unwrap().then(() => undefined)}
          onCancel={(row) => cancelRfq({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion }).unwrap().then(() => undefined)}
          onReopen={(row) => reopenRfq({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion }).unwrap().then(() => undefined)}
          onBulkConfirmAmendments={(rows) => bulkConfirmAmendments({ items: rows.map((row) => ({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion, expectedDraftVersion: row.draftVersion! })) }).unwrap().then((results) => { void message.info(results.map((item) => `Case ${item.caseId}: ${item.result}${item.error ? ` (${item.error})` : ''}`).join('; ')) })}
          onBulkDiscardAmendments={(rows) => bulkDiscardAmendments({ items: rows.map((row) => ({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion, expectedDraftVersion: row.draftVersion! })) }).unwrap().then((results) => { void message.info(results.map((item) => `Case ${item.caseId}: ${item.result}${item.error ? ` (${item.error})` : ''}`).join('; ')) })}
          gridConfigJson={gridConfigQuery.data ? JSON.stringify(gridConfigQuery.data.config) : undefined}
          onSaveGridConfig={(configJson) => saveGridConfig({ screenId: 'sales', configKey: 'main', version: 1, config: JSON.parse(configJson) }).unwrap().then(() => undefined)}
          onReload={rfqsQuery.refetch}
        />
      )}
      {activeView === 'trader' && (
        <TraderScreen
          rfqs={traderRfqsQuery.data ?? []}
          traders={traders}
          users={users}
          currentUserId={currentUserQuery.data?.userId ?? configuredIdentity}
          defaultExpiryMinutes={quoteExpiryQuery.data?.type === 'After' ? quoteExpiryQuery.data.minutes : null}
          isLoading={traderRfqsQuery.isLoading || traderRfqsQuery.isFetching}
          isError={traderRfqsQuery.isError}
          isMutating={isOwnershipMutating}
          onPickUp={(caseId, expectedVersion, confirmed) =>
            pickUpRfq({ caseId, expectedVersion, confirmed }).unwrap().then(() => undefined)}
          onRelease={(caseId, expectedVersion) =>
            releaseRfq({ caseId, expectedVersion }).unwrap().then(() => undefined)}
          onAssign={(caseId, targetTraderId, expectedVersion) =>
            assignTrader({ caseId, targetTraderId, expectedVersion }).unwrap().then(() => undefined)}
          onTakeOver={(caseId, expectedVersion, confirmed) =>
            takeOverRfq({ caseId, expectedVersion, confirmed }).unwrap().then(() => undefined)}
          onCalculate={(row, driver, value, simpleYieldSlide) =>
            calculateWorkingQuote({
              caseId: row.caseId,
              driver,
              value,
              simpleYieldSlide,
              expectedCurrentVersion: row.currentVersion,
              expectedWorkingQuoteVersion: row.workingQuoteVersion,
            }).unwrap().then(() => undefined)}
          onChangeMode={(row, mode) =>
            changeWorkingQuoteMode({
              caseId: row.caseId,
              mode,
              expectedCurrentVersion: row.currentVersion,
              expectedWorkingQuoteVersion: row.workingQuoteVersion,
            }).unwrap().then(() => undefined)}
          onUpdateManual={(row, price, finalSimpleYield) =>
            updateManualWorkingQuote({
              caseId: row.caseId,
              price,
              finalSimpleYield,
              expectedCurrentVersion: row.currentVersion,
              expectedWorkingQuoteVersion: row.workingQuoteVersion,
            }).unwrap().then(() => undefined)}
          onConfirmQuote={(row, expiryMinutes) =>
            confirmQuote({
              caseId: row.caseId,
              expiry: expiryMinutes === null
                ? { type: 'None', minutes: null }
                : { type: 'After', minutes: expiryMinutes },
              expectedCurrentVersion: row.currentVersion,
              expectedWorkingQuoteVersion: row.workingQuoteVersion,
            }).unwrap().then(() => undefined)}
          onClose={closeCase}
          onBulkClose={bulkCloseCases}
          onCorrectOutcome={correctOutcome}
          onChangeContactOwner={handOffContactOwner}
          onUpdateMemo={(caseId, memo, expectedVersion) =>
            updateTraderMemo({ caseId, memo, expectedVersion }).unwrap().then(() => undefined)}
          onWithdraw={(row) => withdrawQuote({ caseId: row.caseId, expectedVersion: row.currentVersion }).unwrap().then(() => undefined)}
          onScratchPrice={(input) => scratchPrice(input).unwrap()}
          onReload={traderRfqsQuery.refetch}
        />
      )}
      {activeView === 'eod' && (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <Card title="EOD Summary">
            {(eodQuery.data ?? []).map((item) => (
              <Space key={item.contactOwnerId} style={{ marginRight: 24 }}>
                <Typography.Text strong>{item.contactOwnerId}</Typography.Text>
                <Tag>Open {item.open}</Tag><Tag color="green">Hit {item.hit}</Tag><Tag color="orange">Away {item.away}</Tag>
              </Space>
            ))}
          </Card>
          <Card title="Past RFQ">
            {pastQuery.data?.requiresNarrowing && <Alert type="warning" message="More than 20,000 results. Narrow the search." />}
            <div className="rfq-grid"><AgGridReact
              rowData={pastQuery.data?.items ?? []}
              columnDefs={[
                { field: 'caseId' }, { field: 'createdAt' }, { field: 'clientName' },
                { field: 'securityName' }, { field: 'status' }, { field: 'quoteStatus' },
                { field: 'contactOwnerId' }, { field: 'assignedTraderId' },
              ]}
              defaultColDef={{ sortable: true, filter: true, resizable: true }}
            /></div>
          </Card>
          <Card title="Changes">
            <Typography.Text>Pending Updates: {eventsQuery.data?.length ?? 0}</Typography.Text>
            {(eventsQuery.data ?? []).slice(-10).map((event) => <div key={event.eventId}>#{event.eventId} Case {event.caseId}: {event.type}</div>)}
          </Card>
        </Space>
      )}
    </AppShell>
  )
}
