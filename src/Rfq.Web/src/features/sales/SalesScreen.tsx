import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert, AutoComplete, Badge, Button, Descriptions, Drawer, Empty, Form, Input,
  InputNumber, List, Modal, Select, Space, Spin, Tag, Tooltip, Typography, message,
} from 'antd'
import type {
  CellEditRequestEvent, ColDef, ColGroupDef, GetContextMenuItemsParams, GridApi,
  DefaultMenuItem, IRowNode, MenuItemDef, RowClickedEvent, SelectionChangedEvent, StatusPanelDef,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  BulkItemResult, ClientSearchResult, CreateDraftRequest, RfqCreationContext,
  SalesRecentRevision, SalesRfq, SecuritySearchResult, UpdateDraftRequest,
} from '../../services/api'
import {
  bulkEligibility, commandEligible, derivePaneMode, displayState, elapsedLabel,
  isTextEditingTarget, matchesSalesPreset, requiresConfirmation,
  reconcileSelection,
  type SalesBulkCommand, type SalesCommand,
  type SalesFilterPreset,
} from './salesModel'

type UserOption = { userId: string; name: string }
type BulkSnapshotItem = { row: SalesRfq; eligible: boolean; reason?: string }
type BulkDialog = { command: SalesBulkCommand; items: BulkSnapshotItem[] }
type SingleDialog = { command: SalesCommand; row: SalesRfq }

const million = 1_000_000
const filterOptions: { value: SalesFilterPreset; label: string }[] = [
  { value: 'all', label: 'All RFQs' },
  { value: 'owner', label: 'Owner = Me' },
  { value: 'sales', label: 'Sales = Me' },
  { value: 'owner-and-sales', label: 'Owner & Sales = Me' },
  { value: 'owner-or-sales', label: 'Owner | Sales = Me' },
]

interface RfqFormValues {
  clientId: string
  securityId: string
  categoryName: string
  assignedTraderId: string
  settlementDate: string
  standardSettlementDate: string
  notional?: number
  salesAndTradingMessage: string
}

export interface SalesScreenProps {
  rfqs: SalesRfq[]
  clients: ClientSearchResult[]
  securities: SecuritySearchResult[]
  traders: UserOption[]
  users?: UserOption[]
  currentUserId: string
  isLoading: boolean
  isError: boolean
  isMutating: boolean
  recentRevisions?: SalesRecentRevision[]
  recentRevisionsLoading?: boolean
  remoteUpdatePending?: boolean
  onTransientStateChange?: (protectedState: boolean) => void
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
  onClose: (caseId: number, outcome: 'Hit' | 'Away', expectedCurrentVersion: number) => Promise<void>
  onCancel?: (row: SalesRfq) => Promise<void>
  onReopen?: (row: SalesRfq) => Promise<void>
  onCreateFromExisting?: (caseId: number) => Promise<void>
  onUpdateMemo: (caseId: number, memo: string, expectedVersion: number) => Promise<void>
  onSaveAmendment?: (row: SalesRfq, notional: number | null, settlementDate: string | null, text: string) => Promise<void>
  onConfirmAmendment?: (row: SalesRfq) => Promise<void>
  onDiscardAmendment?: (row: SalesRfq) => Promise<void>
  onBulk?: (command: SalesBulkCommand, rows: SalesRfq[]) => Promise<BulkItemResult[]>
  onBulkClose?: (items: { caseId: number; expectedCurrentVersion: number }[]) => Promise<BulkItemResult[]>
  onCorrectOutcome?: (caseId: number, outcome: 'Hit' | 'Away', expectedCurrentVersion: number) => Promise<void>
  onChangeContactOwner?: (caseId: number, targetUserId: string, expectedCurrentVersion: number) => Promise<void>
  onReload: () => void | Promise<unknown>
  gridConfigJson?: string
  onSaveGridConfig?: (configJson: string) => Promise<void>
}

function quoteValue(value: number | null | undefined, suffix = '') {
  return value === null || value === undefined ? '—' : `${value.toLocaleString()}${suffix}`
}

function compactText(value: string, empty = '—') {
  return value.trim() || empty
}

export function SalesScreen(props: SalesScreenProps) {
  const {
    rfqs, clients, securities, traders, currentUserId, isLoading, isError, isMutating,
    recentRevisions = [], recentRevisionsLoading = false, remoteUpdatePending = false,
    onTransientStateChange, onClientSearch, onSecuritySearch, onResolveDefaults,
    onCreate, onUpdate, onConfirmNew, onConfirmDraft, onDiscard, onPresent,
    onUnpresent, onClose,
    onCancel = async () => undefined,
    onReopen = async () => undefined,
    onCreateFromExisting = async () => undefined,
    onUpdateMemo,
    onSaveAmendment = async () => undefined,
    onConfirmAmendment = async () => undefined,
    onDiscardAmendment = async () => undefined,
    onBulk = async () => [],
    onCorrectOutcome = async () => undefined,
    onReload,
    gridConfigJson, onSaveGridConfig,
  } = props
  const [form] = Form.useForm<RfqFormValues>()
  const [gridApi, setGridApi] = useState<GridApi<SalesRfq> | null>(null)
  const [selectedCaseIds, setSelectedCaseIds] = useState<number[]>([])
  const [newIntent, setNewIntent] = useState(false)
  const [filterPreset, setFilterPreset] = useState<SalesFilterPreset>('all')
  const [memoEditing, setMemoEditing] = useState(false)
  const [memoDraft, setMemoDraft] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)
  const [defaultsLoading, setDefaultsLoading] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [singleDialog, setSingleDialog] = useState<SingleDialog | null>(null)
  const [bulkDialog, setBulkDialog] = useState<BulkDialog | null>(null)
  const [bulkResults, setBulkResults] = useState<BulkItemResult[] | null>(null)
  const [now, setNow] = useState(() => Date.now())
  const protectedRef = useRef(false)

  const selectedRows = useMemo(() => selectedCaseIds
    .map((caseId) => rfqs.find((row) => row.caseId === caseId))
    .filter((row): row is SalesRfq => Boolean(row)), [rfqs, selectedCaseIds])
  const selected = selectedRows.length === 1 ? selectedRows[0] : undefined
  const mode = derivePaneMode(rfqs, selectedCaseIds, newIntent)
  const isProtected = newIntent || mode === 'draft' || memoEditing
    || singleDialog !== null || bulkDialog !== null

  useEffect(() => {
    if (protectedRef.current !== isProtected) {
      protectedRef.current = isProtected
      onTransientStateChange?.(isProtected)
    }
  }, [isProtected, onTransientStateChange])

  useEffect(() => {
    setSelectedCaseIds((current) => reconcileSelection(current, rfqs))
  }, [rfqs])

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    gridApi?.onFilterChanged()
  }, [filterPreset, gridApi])

  useEffect(() => {
    if (!gridApi || !gridConfigJson) return
    try {
      gridApi.applyColumnState({ state: JSON.parse(gridConfigJson), applyOrder: true })
    } catch { /* obsolete layouts reset to the application default */ }
  }, [gridApi, gridConfigJson])

  const startNew = () => {
    setNewIntent(true)
    setSelectedCaseIds([])
    gridApi?.deselectAll()
    form.resetFields()
    setActionError(null)
    setConflict(false)
  }

  const selectRow = (row: SalesRfq) => {
    setNewIntent(false)
    setSelectedCaseIds([row.caseId])
    setMemoDraft(row.salesMemo)
    setMemoEditing(false)
    setActionError(null)
    setConflict(false)
    if (row.revisionStatus === 'Draft') {
      form.setFieldsValue({
        clientId: row.clientId,
        securityId: row.securityId,
        categoryName: row.categoryId,
        assignedTraderId: row.assignedTraderId,
        settlementDate: row.settlementDate ?? undefined,
        standardSettlementDate: row.standardSettlementDate,
        notional: row.notional === null ? undefined : row.notional / million,
        salesAndTradingMessage: row.salesAndTradingMessage,
      })
    } else {
      form.resetFields()
    }
  }

  const applyDefaults = async (securityId: string) => {
    if (!(newIntent || mode === 'draft')) return
    setDefaultsLoading(true)
    try {
      const value = await onResolveDefaults(securityId)
      form.setFieldsValue({
        categoryName: value.categoryName,
        assignedTraderId: value.defaultAssignedTraderId,
        standardSettlementDate: value.standardSettlementDate,
        settlementDate: value.standardSettlementDate,
      })
    } catch {
      setActionError('Security defaults could not be resolved.')
    } finally {
      setDefaultsLoading(false)
    }
  }

  const toRequest = (values: RfqFormValues): CreateDraftRequest => ({
    clientId: values.clientId,
    securityId: values.securityId,
    assignedTraderId: values.assignedTraderId,
    settlementDate: values.settlementDate,
    standardSettlementDate: values.standardSettlementDate,
    notional: values.notional === undefined ? undefined : values.notional * million,
    salesAndTradingMessage: values.salesAndTradingMessage ?? '',
  })

  const run = async (action: () => Promise<void>, reload = true) => {
    setActionError(null)
    setConflict(false)
    try {
      await action()
      setNewIntent(false)
      setMemoEditing(false)
      if (reload) await onReload()
    } catch (error) {
      const status = (error as { status?: number })?.status
      setConflict(status === 409)
      setActionError(status === 409
        ? 'This RFQ was updated elsewhere. Your local input is preserved; review and reload explicitly.'
        : 'The RFQ action could not be completed.')
    }
  }

  const saveDraft = async (confirm: boolean) => {
    try {
      const required = confirm
        ? ['clientId', 'securityId', 'assignedTraderId', 'settlementDate', 'notional']
        : ['clientId', 'securityId']
      await form.validateFields(required)
    } catch { return }
    const values = form.getFieldsValue(true) as RfqFormValues
    const request = toRequest(values)
    if (!request.assignedTraderId || !request.settlementDate || !request.standardSettlementDate) {
      setActionError('Select a Security so routing and settlement defaults are resolved.')
      return
    }
    await run(async () => {
      if (mode === 'draft' && selected) {
        const update: UpdateDraftRequest = {
          notional: request.notional,
          settlementDate: request.settlementDate,
          standardSettlementDate: request.standardSettlementDate,
          salesAndTradingMessage: request.salesAndTradingMessage,
          assignedTraderId: request.assignedTraderId,
          expectedVersion: selected.version,
        }
        await (confirm ? onConfirmDraft(selected.caseId, update) : onUpdate(selected.caseId, update))
      } else {
        await (confirm ? onConfirmNew(request) : onCreate(request))
      }
    })
  }

  const executeCommand = async (command: SalesCommand, row: SalesRfq) => {
    await run(async () => {
      switch (command) {
        case 'present': return onPresent(row.caseId, row.currentVersion)
        case 'unpresent': return onUnpresent(row.caseId, row.currentVersion)
        case 'hit': return onClose(row.caseId, 'Hit', row.currentVersion)
        case 'away': return onClose(row.caseId, 'Away', row.currentVersion)
        case 'cancel': return onCancel(row)
        case 'reopen': return onReopen(row)
        case 'confirm-amendment': return onConfirmAmendment(row)
        case 'discard-amendment': return onDiscardAmendment(row)
        case 'create-from-existing': return onCreateFromExisting(row.caseId)
      }
    })
  }

  const requestCommand = (command: SalesCommand, row: SalesRfq, confirm: boolean) => {
    if (!commandEligible(command, row, currentUserId)) return
    if (confirm) setSingleDialog({ command, row })
    else void executeCommand(command, row)
  }

  const openBulk = (command: SalesBulkCommand) => {
    const items = selectedRows.map((row) => ({
      row,
      eligible: bulkEligibility(command, row, currentUserId),
      reason: bulkEligibility(command, row, currentUserId) ? undefined : 'Not eligible in current state',
    }))
    if (!items.some((item) => item.eligible)) return
    setBulkResults(null)
    setBulkDialog({ command, items })
  }

  const applyBulk = async () => {
    if (!bulkDialog) return
    try {
      const eligible = bulkDialog.items.filter((item) => item.eligible).map((item) => item.row)
      const results = await onBulk(bulkDialog.command, eligible)
      const skipped: BulkItemResult[] = bulkDialog.items.filter((item) => !item.eligible).map((item) => ({
        caseId: item.row.caseId, status: 'Skipped', code: 'InvalidState', message: item.reason ?? null,
      }))
      setBulkResults([...results, ...skipped])
      await onReload()
    } catch {
      setActionError('The bulk operation could not be completed.')
      setBulkDialog(null)
    }
  }

  const editAmendment = async (event: CellEditRequestEvent<SalesRfq>) => {
    const row = event.data
    if (!row || row.revisionStatus === 'Draft') return
    setSelectedCaseIds([row.caseId])
    const field = event.colDef.field
    await run(() => onSaveAmendment(
      row,
      field === 'notional' ? Number(event.newValue) * million : row.draftNotional ?? row.notional,
      field === 'settlementDate' ? String(event.newValue) : row.draftSettlementDate ?? row.settlementDate,
      field === 'salesAndTradingMessage'
        ? String(event.newValue ?? '')
        : row.draftSalesAndTradingMessage ?? row.salesAndTradingMessage,
    ))
  }

  const contextMenu = (params: GetContextMenuItemsParams<SalesRfq>): (DefaultMenuItem | MenuItemDef<SalesRfq>)[] => {
    const row = params.node?.data
    if (!row) return []
    const items: (DefaultMenuItem | MenuItemDef<SalesRfq>)[] = []
    const add = (command: SalesCommand, label: string) => {
      if (commandEligible(command, row, currentUserId))
        items.push({ name: label, action: () => requestCommand(
          command, row, requiresConfirmation('context-menu', command)) })
    }
    add('present', 'Present')
    add('unpresent', 'Unpresent')
    add('hit', 'Hit')
    add('away', 'Away')
    add('cancel', 'Cancel')
    add('reopen', 'Reopen')
    add('confirm-amendment', 'Confirm Amendment')
    add('discard-amendment', 'Discard Amendment')
    if (items.length) items.push('separator')
    items.push({ name: 'Create New from Existing', action: () => void executeCommand('create-from-existing', row) })
    items.push({
      name: 'Copy', subMenu: [
        { name: 'Case ID', action: () => void navigator.clipboard.writeText(String(row.caseId)) },
        { name: 'Security ID', action: () => void navigator.clipboard.writeText(row.securityId) },
        { name: 'Client ID', action: () => void navigator.clipboard.writeText(row.clientId) },
      ],
    })
    return items
  }

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setSingleDialog(null); setBulkDialog(null); setDrawerOpen(false); setMemoEditing(false)
        if (newIntent) setNewIntent(false)
        return
      }
      if (!event.altKey || isTextEditingTarget(event.target)) return
      const key = event.key.toLowerCase()
      if (key === 'n') { event.preventDefault(); startNew(); return }
      if (key === 'enter') {
        event.preventDefault()
        if (selectedRows.length > 1 && selectedRows.some((row) => row.draftRevisionId))
          openBulk('confirm-amendments')
        else if (selected?.draftRevisionId) void executeCommand('confirm-amendment', selected)
        return
      }
      if (!selected) return
      const mapping: Record<string, SalesCommand> = {
        p: 'present', u: 'unpresent', r: 'reopen',
        h: 'hit', a: 'away', c: 'cancel',
      }
      const mapped = mapping[key]
      if (mapped) { event.preventDefault(); requestCommand(
        mapped, selected, requiresConfirmation('shortcut', mapped)) }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  const columns = useMemo<(ColDef<SalesRfq> | ColGroupDef<SalesRfq>)[]>(() => [
    {
      groupId: 'client', headerName: 'Client', marryChildren: true, children: [
        { colId: 'client', headerName: 'Client', field: 'clientName', pinned: 'left', width: 140, tooltipField: 'clientName' },
        { colId: 'client-detail', headerName: 'Client Name', field: 'clientName', columnGroupShow: 'open', width: 180 },
        { colId: 'client-id', headerName: 'Client ID', field: 'clientId', columnGroupShow: 'open', width: 120 },
      ],
    },
    {
      groupId: 'security', headerName: 'Security', marryChildren: true, children: [
        { colId: 'security', headerName: 'Security', field: 'securityJapaneseName', pinned: 'left', width: 160, tooltipField: 'securityBbgDisplay' },
        { colId: 'security-ja', headerName: 'Japanese Name', field: 'securityJapaneseName', columnGroupShow: 'open', width: 190 },
        { colId: 'security-bbg', headerName: 'BBG Display', field: 'securityBbgDisplay', columnGroupShow: 'open', width: 200 },
        { colId: 'security-id', headerName: 'Security ID', field: 'securityId', columnGroupShow: 'open', width: 130 },
      ],
    },
    {
      groupId: 'terms', headerName: 'Terms', children: [
        {
          field: 'notional', headerName: 'Notl', width: 95, editable: ({ data }) => Boolean(data && data.revisionStatus !== 'Draft' && ['Active', 'Presented'].includes(data.rfqStatus)),
          valueGetter: ({ data }) => data?.draftNotional ?? data?.notional,
          valueFormatter: ({ value }) => value == null ? '' : `${(Number(value) / million).toLocaleString()} MM`,
          cellClass: ({ data }) => data?.draftNotional != null ? 'amendment-changed-cell' : undefined,
        },
        {
          field: 'settlementDate', headerName: 'Settle', width: 105,
          editable: ({ data }) => Boolean(data && data.revisionStatus !== 'Draft' && ['Active', 'Presented'].includes(data.rfqStatus)),
          valueGetter: ({ data }) => data?.draftSettlementDate ?? data?.settlementDate,
          cellClass: ({ data }) => data?.draftSettlementDate != null ? 'amendment-changed-cell' : undefined,
        },
        { field: 'assignedTraderId', headerName: 'Trader', width: 105 },
      ],
    },
    {
      groupId: 'state', headerName: 'State', children: [
        { colId: 'state', headerName: 'State', width: 100, valueGetter: ({ data }) => data ? displayState(data) : '' },
        { field: 'rfqStatus', headerName: 'RFQ Status', columnGroupShow: 'open', width: 110 },
        { field: 'quoteStatus', headerName: 'Quote Status', columnGroupShow: 'open', width: 110 },
        { field: 'quoteRequestReason', headerName: 'Reason', columnGroupShow: 'open', width: 105 },
      ],
    },
    { colId: 'amend', headerName: 'Work', width: 78, valueGetter: ({ data }) => data?.draftRevisionId ? 'AMEND' : data?.revisionStatus === 'Draft' ? 'DRAFT' : '' },
    {
      groupId: 'time', headerName: 'Time', children: [
        { colId: 'elapsed', headerName: 'Elapsed', width: 90, valueGetter: ({ data }) => data ? elapsedLabel(data.stateSince, now) : '' },
        { field: 'createdAt', headerName: 'Created', columnGroupShow: 'open', width: 155, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : '' },
        { field: 'stateSince', headerName: 'State Since', columnGroupShow: 'open', width: 155, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : '' },
      ],
    },
    {
      groupId: 'quote', headerName: 'Quote', children: [
        { colId: 'price', headerName: 'Price', width: 90, valueGetter: ({ data }) => data?.confirmedQuote?.price ?? null },
        { colId: 'yield', headerName: 'Yield', columnGroupShow: 'open', width: 85, valueGetter: ({ data }) => data?.confirmedQuote?.bbgYield ?? null, valueFormatter: ({ value }) => value == null ? '' : `${value}%` },
        { colId: 'simple', headerName: 'Simple', columnGroupShow: 'open', width: 85, valueGetter: ({ data }) => data?.confirmedQuote?.finalSimpleYield ?? null, valueFormatter: ({ value }) => value == null ? '' : `${value}%` },
        { colId: 'g-spread', headerName: 'G-Spread', columnGroupShow: 'open', width: 90, valueGetter: ({ data }) => data?.confirmedQuote?.gSpread ?? null, valueFormatter: ({ value }) => value == null ? '' : `${value} bp` },
      ],
    },
    { field: 'salesAndTradingMessage', headerName: 'Message', width: 180, tooltipField: 'salesAndTradingMessage', editable: ({ data }) => Boolean(data && data.revisionStatus !== 'Draft' && ['Active', 'Presented'].includes(data.rfqStatus)), valueGetter: ({ data }) => data?.draftSalesAndTradingMessage ?? data?.salesAndTradingMessage, cellClass: ({ data }) => data?.draftSalesAndTradingMessage != null ? 'amendment-changed-cell' : undefined },
    { field: 'currentQuoteId', headerName: 'Quote ID', hide: true, valueFormatter: ({ value }) => value ? String(value).slice(0, 8) : '' },
    { field: 'caseId', headerName: 'Case', width: 85 },
  ], [now])

  const rowClassRules = {
    'sales-row-quoted': ({ data }: { data?: SalesRfq }) => data?.rfqStatus === 'Active' && data.quoteStatus === 'Quoted',
    'sales-row-draft': ({ data }: { data?: SalesRfq }) => data?.revisionStatus === 'Draft',
    'sales-row-terminal': ({ data }: { data?: SalesRfq }) => Boolean(data && ['Cancelled', 'Hit', 'Away'].includes(data.rfqStatus)),
  }
  const shortcutText = selectedRows.length > 1
    ? `${selectedRows.length} selected · Alt+A Away · Right-click for actions`
    : selected?.draftRevisionId
      ? 'AMEND pending · Alt+Enter Confirm · Right-click for more'
      : selected ? 'Alt+P Present · Alt+H Hit · Alt+A Away · Alt+C Cancel' : 'Right-click for actions'
  const statusBar = useMemo<{ statusPanels: StatusPanelDef[] }>(() => ({ statusPanels: [
    { statusPanel: 'agSelectedRowCountComponent', align: 'left' },
    { statusPanel: 'agFilteredRowCountComponent', align: 'left' },
    { statusPanel: 'salesShortcutStatus', align: 'right', statusPanelParams: { text: shortcutText } },
  ] }), [shortcutText])

  return (
    <div className="sales-blotter">
      <div className="sales-toolbar">
        <Space size={8} wrap>
          <Button type="primary" size="small" onClick={startNew}>New RFQ</Button>
          <Typography.Text type="secondary">Filter</Typography.Text>
          <Select size="small" value={filterPreset} options={filterOptions} onChange={setFilterPreset} style={{ width: 170 }} />
          {remoteUpdatePending && <Badge status="processing" text="Update deferred" />}
        </Space>
        <Space size={8}>
          <Button size="small" onClick={() => setDrawerOpen(true)}>Recent Revisions</Button>
          <Button size="small" disabled={!gridApi || !onSaveGridConfig} onClick={() => gridApi && onSaveGridConfig && void onSaveGridConfig(JSON.stringify(gridApi.getColumnState()))}>Save Layout</Button>
          <Button size="small" disabled={!gridApi} onClick={() => gridApi?.resetColumnState()}>Reset Layout</Button>
        </Space>
      </div>

      {actionError && <Alert className="sales-action-alert" type={conflict ? 'warning' : 'error'} showIcon message={actionError} action={conflict ? <Button size="small" onClick={() => void onReload()}>Review latest</Button> : undefined} />}
      <div className="sales-main">
        <div className="sales-grid-shell">
          {isError && <Alert type="error" showIcon message="RFQs could not be loaded." />}
          <Spin spinning={isLoading}>
            <div className="sales-grid" data-testid="rfq-grid">
              <AgGridReact<SalesRfq>
                rowData={rfqs}
                columnDefs={columns}
                defaultColDef={{ sortable: true, filter: true, resizable: true, suppressHeaderMenuButton: true }}
                getRowId={({ data }) => String(data.caseId)}
                rowHeight={28}
                headerHeight={30}
                groupHeaderHeight={26}
                tooltipShowDelay={400}
                rowSelection={{ mode: 'multiRow', selectAll: 'filtered' }}
                onGridReady={({ api }) => setGridApi(api)}
                onRowClicked={({ data }: RowClickedEvent<SalesRfq>) => data && selectRow(data)}
                onSelectionChanged={({ api }: SelectionChangedEvent<SalesRfq>) => setSelectedCaseIds(api.getSelectedRows().map((row) => row.caseId))}
                isExternalFilterPresent={() => filterPreset !== 'all'}
                doesExternalFilterPass={(node: IRowNode<SalesRfq>) => Boolean(node.data && matchesSalesPreset(node.data, filterPreset, currentUserId))}
                readOnlyEdit
                onCellEditRequest={(event) => void editAmendment(event)}
                rowClassRules={rowClassRules}
                getContextMenuItems={contextMenu}
                statusBar={statusBar}
                components={{ salesShortcutStatus: (value: { text: string }) => <span className="sales-shortcut-status">{value.text}</span> }}
              />
            </div>
          </Spin>
        </div>
        <aside className={`sales-work-pane sales-pane-${mode}`}>
          <WorkPaneHeader mode={mode} row={selected} count={selectedRows.length} />
          {(mode === 'new' || mode === 'draft') && (
            <Spin spinning={defaultsLoading}>
              <Form<RfqFormValues> form={form} layout="vertical" size="small" className="sales-form">
                <Form.Item name="clientId" label="Client" rules={[{ required: true }]}>
                  <AutoComplete disabled={mode === 'draft'} filterOption={false} onSearch={(value) => void onClientSearch(value)} options={clients.map((client) => ({ value: client.clientId, label: `${client.name} · ${client.code}` }))} />
                </Form.Item>
                <Form.Item name="securityId" label="Security" rules={[{ required: true }]}>
                  <AutoComplete disabled={mode === 'draft'} filterOption={false} onSearch={(value) => void onSecuritySearch(value)} onSelect={(value) => void applyDefaults(value)} options={securities.map((security) => ({ value: security.securityId, label: `${security.japaneseName} · ${security.bbgDisplay}` }))} />
                </Form.Item>
                <div className="sales-form-subfields">
                  <Form.Item name="categoryName" label="Category"><Input disabled /></Form.Item>
                  <Form.Item name="assignedTraderId" label="Trader"><Select options={traders.map((trader) => ({ value: trader.userId, label: trader.name }))} /></Form.Item>
                </div>
                <Form.Item name="settlementDate" label="Settle"><Input type="date" /></Form.Item>
                <Form.Item name="standardSettlementDate" hidden><Input /></Form.Item>
                <Form.Item name="notional" label="Notl (MM)"><InputNumber min={0} precision={2} /></Form.Item>
                <Form.Item name="salesAndTradingMessage" label="Message"><Input /></Form.Item>
                <Space wrap>
                  <Button size="small" loading={isMutating} onClick={() => void saveDraft(false)}>Save Draft</Button>
                  <Button size="small" type="primary" loading={isMutating} onClick={() => void saveDraft(true)}>Confirm</Button>
                  {mode === 'draft' && selected && <Button size="small" danger onClick={() => void run(() => onDiscard(selected.caseId, selected.version))}>Discard</Button>}
                </Space>
              </Form>
            </Spin>
          )}
          {mode === 'neutral' && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Select an RFQ or choose New RFQ" />}
          {mode === 'bulk' && <BulkPane rows={selectedRows} userId={currentUserId} onOpen={openBulk} />}
          {!['neutral', 'new', 'draft', 'bulk'].includes(mode) && selected && (
            <LifecyclePane row={selected} mode={mode} now={now} isMutating={isMutating}
              memoEditing={memoEditing} memoDraft={memoDraft}
              onMemoEdit={() => { setMemoDraft(selected.salesMemo); setMemoEditing(true) }}
              onMemoChange={setMemoDraft} onMemoCancel={() => setMemoEditing(false)}
              onMemoSave={() => void run(() => onUpdateMemo(selected.caseId, memoDraft, selected.salesMemoVersion))}
              onCorrectOutcome={() => void run(() => onCorrectOutcome(
                selected.caseId,
                selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                selected.currentVersion,
              ))}
              onCommand={(command) => void executeCommand(command, selected)} />
          )}
        </aside>
      </div>

      <Modal open={singleDialog !== null} title={singleDialog ? `${singleDialog.command.toUpperCase()} Case ${singleDialog.row.caseId}` : ''} okText="Apply" onCancel={() => setSingleDialog(null)} onOk={() => { if (singleDialog) void executeCommand(singleDialog.command, singleDialog.row); setSingleDialog(null) }}>
        {singleDialog && <p>{singleDialog.row.securityJapaneseName} · {singleDialog.row.clientName}</p>}
      </Modal>
      <Modal open={bulkDialog !== null} title={bulkDialog ? `Bulk ${bulkDialog.command}` : ''} okText={bulkResults ? 'Close' : 'Apply'} onCancel={() => { setBulkDialog(null); setBulkResults(null) }} onOk={() => bulkResults ? (setBulkDialog(null), setBulkResults(null)) : void applyBulk()}>
        {bulkResults
          ? <List<BulkItemResult> size="small" dataSource={bulkResults} renderItem={(item) => <List.Item><Tag color={item.status === 'Succeeded' ? 'green' : item.status === 'Failed' ? 'red' : 'default'}>{item.status}</Tag> Case {item.caseId} {item.message}</List.Item>} />
          : <List<BulkSnapshotItem> size="small" dataSource={bulkDialog?.items ?? []} renderItem={(item) => <List.Item><Tag color={item.eligible ? 'green' : 'default'}>{item.eligible ? '✓' : '–'}</Tag> Case {item.row.caseId} · {item.row.securityJapaneseName} {item.reason}</List.Item>} />}
      </Modal>
      <Drawer title="Recent Revisions" placement="right" width={480} open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        <Spin spinning={recentRevisionsLoading}>
          <List dataSource={recentRevisions} locale={{ emptyText: 'No confirmed revisions yet' }} renderItem={(item) => (
            <List.Item className="revision-item">
              <List.Item.Meta title={<Space><Typography.Text>{new Date(item.occurredAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</Typography.Text><Tag color={item.kind === 'Quote' ? 'gold' : 'blue'}>{item.kind}</Tag><Typography.Text>Case {item.caseId}</Typography.Text></Space>} description={<><div>{item.securityName} · {item.clientName}</div>{item.changes.map((change) => <div key={change.field}><Typography.Text type="secondary">{change.field}</Typography.Text> {change.before ?? '—'} → {change.after ?? '—'}</div>)}</>} />
            </List.Item>
          )} />
        </Spin>
      </Drawer>
    </div>
  )
}

function WorkPaneHeader({ mode, row, count }: { mode: string; row?: SalesRfq; count: number }) {
  const title = mode === 'new' ? 'New RFQ' : mode === 'bulk' ? `${count} RFQs selected`
    : row ? `Case ${row.caseId}` : 'Work Pane'
  return <div className="work-pane-header"><div><Typography.Text strong>{title}</Typography.Text>{row && <div className="work-pane-security">{row.clientName} · {row.securityJapaneseName}</div>}</div><Tag>{mode.toUpperCase()}</Tag></div>
}

function QuoteSummary({ row }: { row: SalesRfq }) {
  const quote = row.confirmedQuote
  if (!quote) return null
  return <div className="quote-summary">
    <div><span>Price</span><strong>{quoteValue(quote.price)}</strong></div>
    <div><span>Yield</span><strong>{quoteValue(quote.bbgYield, '%')}</strong></div>
    <div><span>Simple</span><strong>{quoteValue(quote.finalSimpleYield, '%')}</strong></div>
    <div><span>G-Spread</span><strong>{quoteValue(quote.gSpread, ' bp')}</strong></div>
  </div>
}

function LifecyclePane({ row, mode, now, isMutating, memoEditing, memoDraft,
  onMemoEdit, onMemoChange, onMemoCancel, onMemoSave, onCorrectOutcome, onCommand }:
  { row: SalesRfq; mode: string; now: number; isMutating: boolean; memoEditing: boolean; memoDraft: string; onMemoEdit: () => void; onMemoChange: (value: string) => void; onMemoCancel: () => void; onMemoSave: () => void; onCorrectOutcome: () => void; onCommand: (command: SalesCommand) => void }) {
  const amendment = row.draftRevisionId ? [
    row.draftNotional !== null && row.draftNotional !== undefined && row.draftNotional !== row.notional ? `Notl ${quoteValue(row.notional && row.notional / million, ' MM')} → ${quoteValue(row.draftNotional / million, ' MM')}` : null,
    row.draftSettlementDate && row.draftSettlementDate !== row.settlementDate ? `Settle ${row.settlementDate} → ${row.draftSettlementDate}` : null,
    row.draftSalesAndTradingMessage !== null && row.draftSalesAndTradingMessage !== undefined && row.draftSalesAndTradingMessage !== row.salesAndTradingMessage ? 'Message changed' : null,
  ].filter(Boolean) : []
  return <>
    <Descriptions size="small" column={1} colon={false} className="work-pane-facts" items={[
      { key: 'state', label: 'State', children: <Space><Tag>{displayState(row)}</Tag>{row.draftRevisionId && <Tag color="purple">AMEND</Tag>}</Space> },
      { key: 'notl', label: 'Notl', children: row.notional == null ? '—' : `${(row.notional / million).toLocaleString()} MM` },
      { key: 'settle', label: 'Settle', children: row.settlementDate ?? '—' },
      { key: 'trader', label: 'Trader', children: row.assignedTraderId },
      { key: 'elapsed', label: 'Elapsed', children: `${elapsedLabel(row.stateSince, now)} · ${row.quoteRequestReason ?? 'current state'}` },
      { key: 'message', label: 'Message', children: compactText(row.salesAndTradingMessage) },
    ]} />
    {(mode === 'quoted' || mode === 'presented' || mode === 'hit' || mode === 'away' || mode === 'cancelled') && <QuoteSummary row={row} />}
    {amendment.length > 0 && <div className="amendment-diff"><Typography.Text strong>Pending Amendment</Typography.Text>{amendment.map((value) => <div key={value}>{value}</div>)}<Space><Button size="small" type="primary" onClick={() => onCommand('confirm-amendment')}>Confirm Amendment</Button><Button size="small" onClick={() => onCommand('discard-amendment')}>Discard</Button></Space></div>}
    <div className="work-pane-actions">
      {mode === 'waiting' && <Button size="small" danger onClick={() => onCommand('cancel')}>Cancel</Button>}
      {mode === 'quoted' && <><Button size="small" type="primary" onClick={() => onCommand('present')}>Present</Button><Button size="small" onClick={() => onCommand('hit')}>Hit</Button><Button size="small" onClick={() => onCommand('away')}>Away</Button><Button size="small" danger onClick={() => onCommand('cancel')}>Cancel</Button></>}
      {mode === 'presented' && <><Button size="small" type="primary" onClick={() => onCommand('hit')}>Hit</Button><Button size="small" type="primary" onClick={() => onCommand('away')}>Away</Button><Button size="small" onClick={() => onCommand('unpresent')}>Unpresent</Button><Button size="small" danger onClick={() => onCommand('cancel')}>Cancel</Button></>}
      {mode === 'cancelled' && <Button size="small" type="primary" onClick={() => onCommand('reopen')}>Reopen</Button>}
      {(mode === 'hit' || mode === 'away') && <Button size="small" type="text" onClick={onCorrectOutcome}>Correct outcome</Button>}
      <Button size="small" type="text" onClick={() => onCommand('create-from-existing')}>Create New from Existing</Button>
    </div>
    <div className="memo-line">
      <Typography.Text type="secondary">Memo</Typography.Text>
      {memoEditing ? <><Input.TextArea aria-label="Sales-only Memo" size="small" autoSize={{ minRows: 2, maxRows: 4 }} value={memoDraft} onChange={(event) => onMemoChange(event.target.value)} /><Space><Button size="small" type="primary" loading={isMutating} onClick={onMemoSave}>Save</Button><Button size="small" onClick={onMemoCancel}>Cancel</Button></Space></> : <><Tooltip title={row.salesMemo}><span className="memo-preview">{compactText(row.salesMemo, 'No memo')}</span></Tooltip><Button size="small" type="link" onClick={onMemoEdit}>Edit</Button></>}
    </div>
  </>
}

function BulkPane({ rows, userId, onOpen }: { rows: SalesRfq[]; userId: string; onOpen: (command: SalesBulkCommand) => void }) {
  const actions: { command: SalesBulkCommand; label: string }[] = [
    { command: 'away', label: 'Away' }, { command: 'cancel', label: 'Cancel' },
    { command: 'present', label: 'Present' }, { command: 'unpresent', label: 'Unpresent' },
    { command: 'confirm-drafts', label: 'Confirm Drafts' }, { command: 'discard-drafts', label: 'Discard Drafts' },
    { command: 'confirm-amendments', label: 'Confirm Amendments' }, { command: 'discard-amendments', label: 'Discard Amendments' },
  ]
  return <div className="bulk-pane"><Typography.Paragraph type="secondary">The confirmation snapshot includes all {rows.length} selected Cases. Bulk Hit is intentionally unavailable.</Typography.Paragraph><Space wrap>{actions.map(({ command, label }) => <Button key={command} size="small" disabled={!rows.some((row) => bulkEligibility(command, row, userId))} onClick={() => onOpen(command)}>{label}</Button>)}</Space><List size="small" dataSource={rows.slice(0, 8)} renderItem={(row) => <List.Item>Case {row.caseId} · {row.securityJapaneseName} <Tag>{displayState(row)}</Tag></List.Item>} />{rows.length > 8 && <Typography.Text type="secondary">+{rows.length - 8} more</Typography.Text>}</div>
}
