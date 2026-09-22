import { useEffect, useMemo, useState } from 'react'
import { Alert, AutoComplete, Button, Card, Form, Input, InputNumber, Popconfirm, Select, Space, Spin, Tag, Typography, message } from 'antd'
import type { CellEditRequestEvent, ColDef, GridApi, RowClickedEvent } from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  BulkItemResult,
  ClientSearchResult,
  CreateDraftRequest,
  RfqCreationContext,
  SalesRfq,
  SecuritySearchResult,
  UpdateDraftRequest,
} from '../../services/api'

type RfqOutcome = 'Hit' | 'Away'
type UserOption = { userId: string; name: string }
type CloseItem = { caseId: number; expectedCurrentVersion: number }

const million = 1_000_000
const toAbsoluteNotional = (notionalInMillions?: number) =>
  notionalInMillions === undefined
    ? undefined
    : Number((notionalInMillions * million).toFixed(2))

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
  onBulkClose: (items: CloseItem[]) => Promise<BulkItemResult[]>
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
  const bulkCloseAway = async () => {
    setActionError(null)
    try {
      const results = await onBulkClose(
        selectedRows.map((row) => ({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        })),
      )
      const details = results.map((result) =>
        `Case ${result.caseId}: ${result.status}${result.message ? ` (${result.message})` : ''}`)
      void message.info(`Bulk Away — ${details.join('; ')}`)
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
              title={`Bulk close ${selectedRows.length} selected RFQs as Away?`}
              onConfirm={() => void bulkCloseAway()}
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
