import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert, AutoComplete, Badge, Button, Descriptions, Drawer, Empty, Form, Input,
  InputNumber, List, Modal, Popconfirm, Segmented, Select, Space, Spin, Tabs,
  Tag, Tooltip, Typography,
} from 'antd'
import type {
  CellEditRequestEvent, ColDef, ColGroupDef, GetContextMenuItemsParams, GridApi,
  DefaultMenuItem, ICellRendererParams, IRowNode, MenuItemDef, RowClickedEvent,
  RowClassParams, SelectionChangedEvent, StatusPanelDef,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  BulkItemResult, ClientSearchResult, CreateDraftRequest, RfqCreationContext,
  SalesRecentRevision, SalesRfq, SecuritySearchResult, UpdateDraftRequest,
} from '../../services/api'
import {
  bulkEligibility, commandEligible, derivePaneMode, displayState, elapsedLabel,
  isTextEditingTarget, matchesSalesPreset, requiresConfirmation,
  reconcileSelection, rowActionCommands,
  type SalesBulkCommand, type SalesCommand, type SalesRowCommand,
  type SalesFilterPreset,
} from './salesModel'

type UserOption = { userId: string; name: string }
type BulkSnapshotItem = { row: SalesRfq; eligible: boolean; reason?: string }
type BulkDialog = { command: SalesBulkCommand; items: BulkSnapshotItem[] }
type SingleDialog = { command: SalesCommand; row: SalesRfq }
type BulkResult = { command: SalesBulkCommand; items: BulkItemResult[] }
export type SalesRefreshMode = 'live' | 'paused'

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
  refreshMode?: SalesRefreshMode
  pendingUpdateCount?: number
  onRefreshModeChange?: (mode: SalesRefreshMode) => void | Promise<void>
  onManualRefresh?: () => void | Promise<void>
  onRowActionApplied?: (command: SalesRowCommand, row: SalesRfq) => void
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
    rfqs, clients, securities, traders, users = [], currentUserId, isLoading, isError, isMutating,
    recentRevisions = [], recentRevisionsLoading = false, remoteUpdatePending = false,
    refreshMode = 'live', pendingUpdateCount = 0,
    onRefreshModeChange = async () => undefined,
    onManualRefresh,
    onRowActionApplied = () => undefined,
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
    onChangeContactOwner = async () => undefined,
    onReload,
    gridConfigJson, onSaveGridConfig,
  } = props
  const [form] = Form.useForm<RfqFormValues>()
  const [gridApi, setGridApi] = useState<GridApi<SalesRfq> | null>(null)
  const [selectedCaseIds, setSelectedCaseIds] = useState<number[]>([])
  const [activeCaseId, setActiveCaseId] = useState<number>()
  const [workPaneTab, setWorkPaneTab] = useState<'rfq' | 'bulk'>('rfq')
  const [newIntent, setNewIntent] = useState(false)
  const [filterPreset, setFilterPreset] = useState<SalesFilterPreset>('all')
  const [memoEditing, setMemoEditing] = useState(false)
  const [memoDraft, setMemoDraft] = useState('')
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [actionError, setActionError] = useState<string | null>(null)
  const [conflict, setConflict] = useState(false)
  const [defaultsLoading, setDefaultsLoading] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [singleDialog, setSingleDialog] = useState<SingleDialog | null>(null)
  const [bulkDialog, setBulkDialog] = useState<BulkDialog | null>(null)
  const [bulkResult, setBulkResult] = useState<BulkResult | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const protectedRef = useRef(false)

  const selectedRows = useMemo(() => selectedCaseIds
    .map((caseId) => rfqs.find((row) => row.caseId === caseId))
    .filter((row): row is SalesRfq => Boolean(row)), [rfqs, selectedCaseIds])
  const selected = rfqs.find((row) => row.caseId === activeCaseId)
  const mode = derivePaneMode(rfqs, activeCaseId, newIntent)
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
    setActiveCaseId((current) => current !== undefined
      && rfqs.some((row) => row.caseId === current) ? current : undefined)
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
    setWorkPaneTab('rfq')
    setActiveCaseId(undefined)
    setSelectedCaseIds([])
    setTargetContactOwnerId(undefined)
    gridApi?.deselectAll()
    form.resetFields()
    setActionError(null)
    setConflict(false)
  }

  const selectRow = (row: SalesRfq) => {
    setNewIntent(false)
    setActiveCaseId(row.caseId)
    setMemoDraft(row.salesMemo)
    setMemoEditing(false)
    setTargetContactOwnerId(undefined)
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
      setTargetContactOwnerId(undefined)
      if (reload) await onReload()
      return true
    } catch (error) {
      const status = (error as { status?: number })?.status
      setConflict(status === 409)
      setActionError(status === 409
        ? 'This RFQ was updated elsewhere. Your local input is preserved; review and reload explicitly.'
        : 'The RFQ action could not be completed.')
      return false
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
    const succeeded = await run(async () => {
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
    }, refreshMode === 'live')
    if (succeeded && refreshMode === 'paused') onRowActionApplied(command, row)
  }

  const executeRowCommand = async (command: SalesRowCommand, row: SalesRfq) => {
    if (refreshMode === 'live') return
    if (command === 'confirm-draft') {
      const { assignedTraderId, settlementDate, notional } = row
      if (!assignedTraderId || !settlementDate || notional === null) return
      const succeeded = await run(() => onConfirmDraft(row.caseId, {
        notional,
        settlementDate,
        standardSettlementDate: row.standardSettlementDate,
        salesAndTradingMessage: row.salesAndTradingMessage,
        assignedTraderId,
        expectedVersion: row.version,
      }), false)
      if (succeeded) onRowActionApplied(command, row)
    } else if (command === 'discard-draft') {
      const succeeded = await run(() => onDiscard(row.caseId, row.version), false)
      if (succeeded) onRowActionApplied(command, row)
    } else {
      await executeCommand(command, row)
    }
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
    setBulkDialog({ command, items })
  }

  const applyBulk = async () => {
    if (!bulkDialog) return
    const snapshot = bulkDialog
    setBulkDialog(null)
    try {
      const eligible = snapshot.items.filter((item) => item.eligible).map((item) => item.row)
      const results = await onBulk(snapshot.command, eligible)
      const skipped: BulkItemResult[] = snapshot.items.filter((item) => !item.eligible).map((item) => ({
        caseId: item.row.caseId, status: 'Skipped', code: 'InvalidState', message: item.reason ?? null,
      }))
      setBulkResult({ command: snapshot.command, items: [...results, ...skipped] })
      setResultExpanded(false)
      await onReload()
    } catch {
      setActionError('The bulk operation could not be completed.')
    }
  }

  const editAmendment = async (event: CellEditRequestEvent<SalesRfq>) => {
    const row = event.data
    if (!row || row.revisionStatus === 'Draft') return
    setActiveCaseId(row.caseId)
    const field = event.colDef.field
    await run(() => onSaveAmendment(
      row,
      field === 'notional' ? Number(event.newValue) * million : row.draftNotional ?? row.notional,
      field === 'settlementDate' ? String(event.newValue) : row.draftSettlementDate ?? row.settlementDate,
      field === 'salesAndTradingMessage'
        ? String(event.newValue ?? '')
        : row.draftSalesAndTradingMessage ?? row.salesAndTradingMessage,
    ), refreshMode === 'live')
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
    items.push('separator')
    items.push({ name: 'Select All Filtered', action: () => params.api
      .forEachNodeAfterFilter((node) => node.setSelected(true)) })
    items.push({ name: 'Clear Selection', action: () => params.api.deselectAll() })
    return items
  }

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setSingleDialog(null); setBulkDialog(null); setDrawerOpen(false); setMemoEditing(false)
        if (newIntent) setNewIntent(false)
        return
      }
      if (!event.altKey) return
      const key = event.key.toLowerCase()
      const editing = isTextEditingTarget(event.target)
      if (editing && key !== 'enter') return
      if (key === 'l') {
        event.preventDefault()
        void onRefreshModeChange(refreshMode === 'live' ? 'paused' : 'live')
        return
      }
      if (key === 'n') { event.preventDefault(); startNew(); return }
      if (key === 'enter') {
        event.preventDefault()
        if (newIntent || mode === 'draft') void saveDraft(true)
        else if (selected?.draftRevisionId) void executeCommand('confirm-amendment', selected)
        return
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  const columns = useMemo<(ColDef<SalesRfq> | ColGroupDef<SalesRfq>)[]>(() => [
    { field: 'caseId', headerName: 'Case', pinned: 'left', width: 78 },
    {
      colId: 'action', headerName: 'Action', pinned: 'left', width: 188,
      sortable: false, filter: false, suppressMovable: true,
      cellRenderer: ({ data }: ICellRendererParams<SalesRfq>) => data ? <RowActions row={data} userId={currentUserId}
        disabled={refreshMode === 'live' || isMutating}
        onCommand={(command) => void executeRowCommand(command, data)} /> : null,
    },
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
  ], [currentUserId, isMutating, now, refreshMode])

  const rowClassRules = {
    'sales-row-selected': ({ node }: RowClassParams<SalesRfq>) => Boolean(node.isSelected()),
    'sales-row-quoted': ({ data }: RowClassParams<SalesRfq>) => data?.rfqStatus === 'Active' && data.quoteStatus === 'Quoted',
    'sales-row-draft': ({ data }: RowClassParams<SalesRfq>) => data?.revisionStatus === 'Draft',
    'sales-row-terminal': ({ data }: RowClassParams<SalesRfq>) => Boolean(data && ['Cancelled', 'Hit', 'Away'].includes(data.rfqStatus)),
  }
  const shortcutText = (() => {
    const parts = selectedRows.length > 0 ? [`${selectedRows.length} selected`] : []
    if (newIntent || mode === 'draft' || selected?.draftRevisionId) parts.push('Alt+Enter Confirm')
    parts.push('Alt+L Live/Pause', 'Alt+N New', 'Right-click for actions')
    return parts.join(' · ')
  })()
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
          <Segmented size="small" aria-label="Refresh mode"
            value={refreshMode === 'live' ? 'Live' : 'Paused'} options={['Live', 'Paused']}
            onChange={(value) => void onRefreshModeChange(value === 'Live' ? 'live' : 'paused')} />
          {refreshMode === 'paused' && <Badge status={pendingUpdateCount ? 'warning' : 'default'}
            text={`Paused · ${pendingUpdateCount} updates pending`} />}
          {refreshMode === 'live' && remoteUpdatePending && <Badge status="processing" text="Update deferred" />}
        </Space>
        <Space size={8}>
          <Button size="small" onClick={() => setDrawerOpen(true)}>Recent Revisions</Button>
          <Button size="small" disabled={!gridApi || !onSaveGridConfig} onClick={() => gridApi && onSaveGridConfig && void onSaveGridConfig(JSON.stringify(gridApi.getColumnState()))}>Save Layout</Button>
          <Button size="small" disabled={!gridApi} onClick={() => gridApi?.resetColumnState()}>Reset Layout</Button>
          <Tooltip title="Refresh latest snapshot"><Button size="small" aria-label="Refresh"
            onClick={() => void (onManualRefresh ?? onReload)()}>↻</Button></Tooltip>
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
                rowSelection={{
                  mode: 'multiRow', selectAll: 'filtered', enableClickSelection: true,
                  enableSelectionWithoutKeys: false, checkboxes: false, headerCheckbox: false,
                }}
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
          <Tabs size="small" activeKey={workPaneTab}
            onChange={(key) => setWorkPaneTab(key as 'rfq' | 'bulk')}
            items={[
              { key: 'rfq', label: 'RFQ', children: <>
          <WorkPaneHeader mode={mode} row={selected} />
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
          {!['neutral', 'new', 'draft'].includes(mode) && selected && (
            <LifecyclePane row={selected} mode={mode} now={now} isMutating={isMutating}
              users={users} currentUserId={currentUserId}
              targetContactOwnerId={targetContactOwnerId}
              onTargetContactOwnerChange={setTargetContactOwnerId}
              onChangeContactOwner={() => targetContactOwnerId && void run(async () => {
                await onChangeContactOwner(selected.caseId, targetContactOwnerId, selected.currentVersion)
                setTargetContactOwnerId(undefined)
              }, refreshMode === 'live')}
              memoEditing={memoEditing} memoDraft={memoDraft}
              onMemoEdit={() => { setMemoDraft(selected.salesMemo); setMemoEditing(true) }}
              onMemoChange={setMemoDraft} onMemoCancel={() => setMemoEditing(false)}
              onMemoSave={() => void run(
                () => onUpdateMemo(selected.caseId, memoDraft, selected.salesMemoVersion),
                refreshMode === 'live',
              )}
              onCorrectOutcome={() => void run(() => onCorrectOutcome(
                selected.caseId,
                selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                selected.currentVersion,
              ), refreshMode === 'live')}
              onCommand={(command) => void executeCommand(command, selected)} />
          )}
              </> },
              { key: 'bulk', label: `Bulk (${selectedRows.length})`, children:
                <BulkPane rows={selectedRows} userId={currentUserId} onOpen={openBulk} /> },
            ]} />
        </aside>
      </div>

      {bulkResult && <BulkResultBar result={bulkResult} expanded={resultExpanded}
        onToggle={() => setResultExpanded((current) => !current)} />}

      <Modal open={singleDialog !== null} title={singleDialog ? `${singleDialog.command.toUpperCase()} Case ${singleDialog.row.caseId}` : ''} okText="Apply" onCancel={() => setSingleDialog(null)} onOk={() => { if (singleDialog) void executeCommand(singleDialog.command, singleDialog.row); setSingleDialog(null) }}>
        {singleDialog && <p>{singleDialog.row.securityJapaneseName} · {singleDialog.row.clientName}</p>}
      </Modal>
      <Modal open={bulkDialog !== null} title={bulkDialog ? `Bulk ${bulkDialog.command}` : ''}
        okText="Apply" onCancel={() => setBulkDialog(null)} onOk={() => void applyBulk()}>
        <List<BulkSnapshotItem> size="small" dataSource={bulkDialog?.items ?? []} renderItem={(item) => <List.Item><Tag color={item.eligible ? 'green' : 'default'}>{item.eligible ? '✓' : '–'}</Tag> Case {item.row.caseId} · {item.row.securityJapaneseName} {item.reason}</List.Item>} />
      </Modal>
      <Drawer title="Recent Revisions" placement="right" size={480} open={drawerOpen} onClose={() => setDrawerOpen(false)}>
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

function WorkPaneHeader({ mode, row }: { mode: string; row?: SalesRfq }) {
  const title = mode === 'new' ? 'New RFQ' : row ? `Case ${row.caseId}` : 'Work Pane'
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

function LifecyclePane({ row, mode, now, isMutating, users, currentUserId,
  targetContactOwnerId, onTargetContactOwnerChange, onChangeContactOwner,
  memoEditing, memoDraft, onMemoEdit, onMemoChange, onMemoCancel, onMemoSave,
  onCorrectOutcome, onCommand }:
  { row: SalesRfq; mode: string; now: number; isMutating: boolean; users: UserOption[]; currentUserId: string; targetContactOwnerId?: string; onTargetContactOwnerChange: (value: string) => void; onChangeContactOwner: () => void; memoEditing: boolean; memoDraft: string; onMemoEdit: () => void; onMemoChange: (value: string) => void; onMemoCancel: () => void; onMemoSave: () => void; onCorrectOutcome: () => void; onCommand: (command: SalesCommand) => void }) {
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
      { key: 'owner', label: 'Owner', children: users.find((user) => user.userId === row.contactOwnerId)?.name ?? row.contactOwnerId },
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
    {row.contactOwnerId === currentUserId && <div className="owner-handoff">
      <Typography.Text type="secondary">Contact Owner handoff</Typography.Text>
      <Space.Compact block>
        <Select size="small" aria-label="Contact Owner" value={targetContactOwnerId}
          onChange={onTargetContactOwnerChange} options={users
            .filter((user) => user.userId !== row.contactOwnerId)
            .map((user) => ({ value: user.userId, label: user.name }))} />
        <Popconfirm title={targetContactOwnerId
          ? `Hand off Case ${row.caseId} to ${targetContactOwnerId}?`
          : 'Select a Contact Owner.'}
          disabled={!targetContactOwnerId} onConfirm={onChangeContactOwner}>
          <Button size="small" disabled={!targetContactOwnerId || isMutating}>Change</Button>
        </Popconfirm>
      </Space.Compact>
    </div>}
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
  if (rows.length < 2) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE}
    description="Select multiple RFQs for bulk operations" />
  return <div className="bulk-pane"><Typography.Paragraph type="secondary">The confirmation snapshot includes all {rows.length} selected Cases. Bulk Hit is intentionally unavailable.</Typography.Paragraph><Space wrap>{actions.map(({ command, label }) => <Button key={command} size="small" disabled={!rows.some((row) => bulkEligibility(command, row, userId))} onClick={() => onOpen(command)}>{label}</Button>)}</Space><List size="small" dataSource={rows.slice(0, 8)} renderItem={(row) => <List.Item>Case {row.caseId} · {row.securityJapaneseName} <Tag>{displayState(row)}</Tag></List.Item>} />{rows.length > 8 && <Typography.Text type="secondary">+{rows.length - 8} more</Typography.Text>}</div>
}

const rowActionLabels: Record<SalesRowCommand, { short: string; full: string }> = {
  'confirm-draft': { short: '✓', full: 'Confirm Draft' },
  'discard-draft': { short: '×', full: 'Discard Draft' },
  present: { short: 'P', full: 'Present' },
  unpresent: { short: 'U', full: 'Unpresent' },
  hit: { short: 'H', full: 'Hit' },
  away: { short: 'A', full: 'Away' },
  cancel: { short: 'X', full: 'Cancel' },
  reopen: { short: 'R', full: 'Reopen' },
  'confirm-amendment': { short: 'A✓', full: 'Confirm Amendment' },
  'discard-amendment': { short: 'A×', full: 'Discard Amendment' },
  'create-from-existing': { short: '+', full: 'Create New from Existing' },
}

function RowActions({ row, userId, disabled, onCommand }: {
  row: SalesRfq
  userId: string
  disabled: boolean
  onCommand: (command: SalesRowCommand) => void
}) {
  return <Space.Compact className="row-action-buttons">
    {rowActionCommands(row, userId).map((command) => <Tooltip key={command}
      title={disabled ? `${rowActionLabels[command].full} (Pause to enable)` : rowActionLabels[command].full}>
      <Button size="small" aria-label={`Row ${row.caseId} ${rowActionLabels[command].full}`}
        disabled={disabled} onClick={(event) => { event.stopPropagation(); onCommand(command) }}>
        {rowActionLabels[command].short}
      </Button>
    </Tooltip>)}
  </Space.Compact>
}

function BulkResultBar({ result, expanded, onToggle }: {
  result: BulkResult
  expanded: boolean
  onToggle: () => void
}) {
  const succeeded = result.items.filter((item) => item.status === 'Succeeded').length
  const skipped = result.items.filter((item) => item.status === 'Skipped').length
  const failed = result.items.filter((item) => item.status === 'Failed').length
  const tone = failed ? 'error' : skipped ? 'warning' : 'success'
  const details = result.items.filter((item) => item.status !== 'Succeeded')
  return <section className={`bulk-result-bar bulk-result-${tone}`} aria-label="Bulk result">
    {expanded && <div className="bulk-result-details"><table>
      <thead><tr><th>Case</th><th>Result</th><th>Code</th><th>Message</th></tr></thead>
      <tbody>{(details.length ? details : result.items).map((item) => <tr key={item.caseId}>
        <td>{item.caseId}</td><td>{item.status}</td><td>{item.code}</td><td>{item.message}</td>
      </tr>)}</tbody>
    </table></div>}
    <div className="bulk-result-summary" role="status">
      <span>Bulk {bulkCommandLabel(result.command)}: {succeeded} ok / {skipped} skipped / {failed} failed</span>
      <Button size="small" type="link" onClick={onToggle}>{expanded ? 'Collapse' : 'Details'}</Button>
    </div>
  </section>
}

function bulkCommandLabel(command: SalesBulkCommand) {
  return command.split('-').map((part) => part[0].toUpperCase() + part.slice(1)).join(' ')
}
