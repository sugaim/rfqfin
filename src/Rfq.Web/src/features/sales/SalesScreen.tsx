import { type ReactElement, useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert,
  Badge,
  Button,
  Drawer,
  Empty,
  Form,
  List,
  Modal,
  Segmented,
  Select,
  Space,
  Spin,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type {
  CellEditRequestEvent,
  GetContextMenuItemsParams,
  GridApi,
  DefaultMenuItem,
  IRowNode,
  MenuItemDef,
  RowClickedEvent,
  RowClassParams,
  SelectionChangedEvent,
  StatusPanelDef,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  BulkItemResult,
  ClientSearchResult,
  CreateDraftRequest,
  RfqCreationContext,
  SalesRecentRevision,
  SalesRfq,
  SecuritySearchResult,
  UpdateDraftRequest,
} from '@/services/api'
import {
  bulkEligibility,
  commandEligible,
  derivePaneMode,
  isTextEditingTarget,
  matchesSalesPreset,
  requiresConfirmation,
  reconcileSelection,
  type SalesBulkCommand,
  type SalesCommand,
  type SalesRowCommand,
  type SalesFilterPreset,
  type SalesRefreshMode,
} from '@/features/sales/salesModel'
import {
  applyGridLayout,
  gridLayoutMenu,
  initializeGridLayout,
  type GridColumnGroupState,
} from '@/features/grid/gridLayout'
import { buildSalesColumns } from '@/features/sales/salesColumns'
import {
  SalesRfqEditor,
  type RfqFormValues,
} from '@/features/sales/SalesRfqEditor'
import {
  BulkPane,
  SalesBulkResultBar,
  type SalesBulkResult,
} from '@/features/sales/SalesBulkUi'
import { LifecyclePane, WorkPaneHeader } from '@/features/sales/SalesWorkPane'

type UserOption = { userId: string; name: string }
type BulkSnapshotItem = { row: SalesRfq; eligible: boolean; reason?: string }
type BulkDialog = { command: SalesBulkCommand; items: BulkSnapshotItem[] }
type SingleDialog = { command: SalesCommand; row: SalesRfq }
export type { SalesRefreshMode } from '@/features/sales/salesModel'

const million = 1_000_000
const filterOptions: { value: SalesFilterPreset; label: string }[] = [
  { value: 'all', label: 'All RFQs' },
  { value: 'owner', label: 'Owner = Me' },
  { value: 'sales', label: 'Sales = Me' },
  { value: 'owner-and-sales', label: 'Owner & Sales = Me' },
  { value: 'owner-or-sales', label: 'Owner | Sales = Me' },
]

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
  onClose: (
    caseId: number,
    outcome: 'Hit' | 'Away',
    expectedCurrentVersion: number,
  ) => Promise<void>
  onCancel?: (row: SalesRfq) => Promise<void>
  onReopen?: (row: SalesRfq) => Promise<void>
  onCreateFromExisting?: (caseId: number) => Promise<void>
  onUpdateMemo: (
    caseId: number,
    memo: string,
    expectedVersion: number,
  ) => Promise<void>
  onSaveAmendment?: (
    row: SalesRfq,
    notional: number | null,
    settlementDate: string | null,
    text: string,
  ) => Promise<void>
  onConfirmAmendment?: (row: SalesRfq) => Promise<void>
  onDiscardAmendment?: (row: SalesRfq) => Promise<void>
  onBulk?: (
    command: SalesBulkCommand,
    rows: SalesRfq[],
  ) => Promise<BulkItemResult[]>
  onCorrectOutcome?: (
    caseId: number,
    outcome: 'Hit' | 'Away',
    expectedCurrentVersion: number,
    reason: string,
  ) => Promise<void>
  onChangeContactOwner?: (
    caseId: number,
    targetUserId: string,
    expectedCurrentVersion: number,
  ) => Promise<void>
  onReload: () => void | Promise<unknown>
  gridConfigJson?: string
  onSaveGridConfig?: (configJson: string) => Promise<void>
}

export function SalesScreen(props: SalesScreenProps): ReactElement {
  const {
    rfqs,
    clients,
    securities,
    traders,
    users = [],
    currentUserId,
    isLoading,
    isError,
    isMutating,
    recentRevisions = [],
    recentRevisionsLoading = false,
    remoteUpdatePending = false,
    refreshMode = 'live',
    pendingUpdateCount = 0,
    onRefreshModeChange = async () => undefined,
    onManualRefresh,
    onRowActionApplied = () => undefined,
    onTransientStateChange,
    onClientSearch,
    onSecuritySearch,
    onResolveDefaults,
    onCreate,
    onUpdate,
    onConfirmNew,
    onConfirmDraft,
    onDiscard,
    onPresent,
    onUnpresent,
    onClose,
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
    gridConfigJson,
    onSaveGridConfig,
  } = props
  const [form] = Form.useForm<RfqFormValues>()
  const [gridApi, setGridApi] = useState<GridApi<SalesRfq> | null>(null)
  const defaultColumnGroupState = useRef<GridColumnGroupState>([])
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
  const [bulkResult, setBulkResult] = useState<SalesBulkResult | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const protectedRef = useRef(false)

  const selectedRows = useMemo(
    () =>
      selectedCaseIds
        .map((caseId) => rfqs.find((row) => row.caseId === caseId))
        .filter((row): row is SalesRfq => Boolean(row)),
    [rfqs, selectedCaseIds],
  )
  const selected = rfqs.find((row) => row.caseId === activeCaseId)
  const mode = derivePaneMode(rfqs, activeCaseId, newIntent)
  const isProtected =
    newIntent ||
    mode === 'draft' ||
    memoEditing ||
    singleDialog !== null ||
    bulkDialog !== null

  useEffect(() => {
    if (protectedRef.current !== isProtected) {
      protectedRef.current = isProtected
      onTransientStateChange?.(isProtected)
    }
  }, [isProtected, onTransientStateChange])

  useEffect(() => {
    setSelectedCaseIds((current) => reconcileSelection(current, rfqs))
    setActiveCaseId((current) =>
      current !== undefined && rfqs.some((row) => row.caseId === current)
        ? current
        : undefined,
    )
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
    applyGridLayout(gridApi, gridConfigJson, defaultColumnGroupState.current)
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
    notional:
      values.notional === undefined ? undefined : values.notional * million,
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
      setActionError(
        status === 409
          ? 'This RFQ was updated elsewhere. Your local input is preserved; review and reload explicitly.'
          : 'The RFQ action could not be completed.',
      )

      return false
    }
  }

  const saveDraft = async (confirm: boolean) => {
    try {
      const required = confirm
        ? [
            'clientId',
            'securityId',
            'assignedTraderId',
            'settlementDate',
            'notional',
          ]
        : ['clientId', 'securityId']
      await form.validateFields(required)
    } catch {
      return
    }
    const values = form.getFieldsValue(true) as RfqFormValues
    const request = toRequest(values)
    if (
      !request.assignedTraderId ||
      !request.settlementDate ||
      !request.standardSettlementDate
    ) {
      setActionError(
        'Select a Security so routing and settlement defaults are resolved.',
      )

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
        await (confirm
          ? onConfirmDraft(selected.caseId, update)
          : onUpdate(selected.caseId, update))
      } else {
        await (confirm ? onConfirmNew(request) : onCreate(request))
      }
    })
  }

  const executeCommand = async (command: SalesCommand, row: SalesRfq) => {
    const succeeded = await run(async () => {
      switch (command) {
        case 'present':
          return onPresent(row.caseId, row.currentVersion)
        case 'unpresent':
          return onUnpresent(row.caseId, row.currentVersion)
        case 'hit':
          return onClose(row.caseId, 'Hit', row.currentVersion)
        case 'away':
          return onClose(row.caseId, 'Away', row.currentVersion)
        case 'cancel':
          return onCancel(row)
        case 'reopen':
          return onReopen(row)
        case 'confirm-amendment':
          return onConfirmAmendment(row)
        case 'discard-amendment':
          return onDiscardAmendment(row)
        case 'create-from-existing':
          return onCreateFromExisting(row.caseId)
      }
    }, refreshMode === 'live')
    if (succeeded && refreshMode === 'paused') onRowActionApplied(command, row)
  }

  const executeRowCommand = async (command: SalesRowCommand, row: SalesRfq) => {
    if (refreshMode === 'live') return
    if (command === 'confirm-draft') {
      const { assignedTraderId, settlementDate, notional } = row
      if (!assignedTraderId || !settlementDate || notional === null) return
      const succeeded = await run(
        () =>
          onConfirmDraft(row.caseId, {
            notional,
            settlementDate,
            standardSettlementDate: row.standardSettlementDate,
            salesAndTradingMessage: row.salesAndTradingMessage,
            assignedTraderId,
            expectedVersion: row.version,
          }),
        false,
      )
      if (succeeded) onRowActionApplied(command, row)
    } else if (command === 'discard-draft') {
      const succeeded = await run(
        () => onDiscard(row.caseId, row.version),
        false,
      )
      if (succeeded) onRowActionApplied(command, row)
    } else {
      await executeCommand(command, row)
    }
  }

  const requestCommand = (
    command: SalesCommand,
    row: SalesRfq,
    confirm: boolean,
  ) => {
    if (!commandEligible(command, row, currentUserId)) return
    if (confirm) setSingleDialog({ command, row })
    else void executeCommand(command, row)
  }

  const openBulk = (command: SalesBulkCommand) => {
    const items = selectedRows.map((row) => ({
      row,
      eligible: bulkEligibility(command, row, currentUserId),
      reason: bulkEligibility(command, row, currentUserId)
        ? undefined
        : 'Not eligible in current state',
    }))
    if (!items.some((item) => item.eligible)) return
    setBulkDialog({ command, items })
  }

  const applyBulk = async () => {
    if (!bulkDialog) return
    const snapshot = bulkDialog
    setBulkDialog(null)
    try {
      const eligible = snapshot.items
        .filter((item) => item.eligible)
        .map((item) => item.row)
      const results = await onBulk(snapshot.command, eligible)
      const skipped: BulkItemResult[] = snapshot.items
        .filter((item) => !item.eligible)
        .map((item) => ({
          caseId: item.row.caseId,
          status: 'Skipped',
          code: 'InvalidState',
          message: item.reason ?? null,
        }))
      setBulkResult({
        command: snapshot.command,
        items: [...results, ...skipped],
      })
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
    await run(
      () =>
        onSaveAmendment(
          row,
          field === 'notional'
            ? Number(event.newValue) * million
            : (row.draftNotional ?? row.notional),
          field === 'settlementDate'
            ? String(event.newValue)
            : (row.draftSettlementDate ?? row.settlementDate),
          field === 'salesAndTradingMessage'
            ? String(event.newValue ?? '')
            : (row.draftSalesAndTradingMessage ?? row.salesAndTradingMessage),
        ),
      refreshMode === 'live',
    )
  }

  const contextMenu = (
    params: GetContextMenuItemsParams<SalesRfq>,
  ): (DefaultMenuItem | MenuItemDef<SalesRfq>)[] => {
    const row = params.node?.data
    const items: (DefaultMenuItem | MenuItemDef<SalesRfq>)[] = []
    if (row) {
      const add = (command: SalesCommand, label: string) => {
        if (commandEligible(command, row, currentUserId))
          items.push({
            name: label,
            action: () =>
              requestCommand(
                command,
                row,
                requiresConfirmation('context-menu', command),
              ),
          })
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
      items.push({
        name: 'Create New from Existing',
        action: () => void executeCommand('create-from-existing', row),
      })
      items.push({
        name: 'Copy',
        subMenu: [
          {
            name: 'Case ID',
            action: () =>
              void navigator.clipboard.writeText(String(row.caseId)),
          },
          {
            name: 'Security ID',
            action: () => void navigator.clipboard.writeText(row.securityId),
          },
          {
            name: 'Client ID',
            action: () => void navigator.clipboard.writeText(row.clientId),
          },
        ],
      })
    }
    if (items.length) items.push('separator')
    items.push({
      name: 'Select All Filtered',
      action: () =>
        params.api.forEachNodeAfterFilter((node) => node.setSelected(true)),
    })
    items.push({
      name: 'Clear Selection',
      action: () => params.api.deselectAll(),
    })
    items.push('separator')
    items.push(
      gridLayoutMenu({
        api: params.api,
        configJson: gridConfigJson,
        defaultColumnGroupState: defaultColumnGroupState.current,
        onSave: onSaveGridConfig,
      }),
    )

    return items
  }

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setSingleDialog(null)
        setBulkDialog(null)
        setDrawerOpen(false)
        setMemoEditing(false)
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
      if (key === 'n') {
        event.preventDefault()
        startNew()

        return
      }
      if (key === 'enter') {
        event.preventDefault()
        if (newIntent || mode === 'draft') void saveDraft(true)
        else if (selected?.draftRevisionId)
          void executeCommand('confirm-amendment', selected)

        return
      }
    }
    window.addEventListener('keydown', onKey)

    return () => window.removeEventListener('keydown', onKey)
  })

  const columns = useMemo(
    () =>
      buildSalesColumns({
        currentUserId,
        isMutating,
        now,
        refreshMode,
        onRowCommand: (command, row) => void executeRowCommand(command, row),
      }),
    [currentUserId, isMutating, now, refreshMode],
  )

  const rowClassRules = {
    'sales-row-selected': ({ node }: RowClassParams<SalesRfq>) =>
      Boolean(node.isSelected()),
    'sales-row-quoted': ({ data }: RowClassParams<SalesRfq>) =>
      data?.rfqStatus === 'Active' && data.quoteStatus === 'Quoted',
    'sales-row-draft': ({ data }: RowClassParams<SalesRfq>) =>
      data?.revisionStatus === 'Draft',
    'sales-row-terminal': ({ data }: RowClassParams<SalesRfq>) =>
      Boolean(data && ['Cancelled', 'Hit', 'Away'].includes(data.rfqStatus)),
  }
  const shortcutText = (() => {
    const parts =
      selectedRows.length > 0 ? [`${selectedRows.length} selected`] : []
    if (newIntent || mode === 'draft' || selected?.draftRevisionId)
      parts.push('Alt+Enter Confirm')
    parts.push('Alt+L Live/Pause', 'Alt+N New', 'Right-click for actions')

    return parts.join(' · ')
  })()
  const statusBar = useMemo<{ statusPanels: StatusPanelDef[] }>(
    () => ({
      statusPanels: [
        { statusPanel: 'agSelectedRowCountComponent', align: 'left' },
        { statusPanel: 'agFilteredRowCountComponent', align: 'left' },
        {
          statusPanel: 'salesShortcutStatus',
          align: 'right',
          statusPanelParams: { text: shortcutText },
        },
      ],
    }),
    [shortcutText],
  )

  return (
    <div className="sales-blotter">
      <div className="sales-toolbar">
        <Space size={8} wrap>
          <Button type="primary" size="small" onClick={startNew}>
            New RFQ
          </Button>
          <Typography.Text type="secondary">Filter</Typography.Text>
          <Select
            size="small"
            value={filterPreset}
            options={filterOptions}
            onChange={setFilterPreset}
            style={{ width: 170 }}
          />
          <Segmented
            size="small"
            aria-label="Refresh mode"
            value={refreshMode === 'live' ? 'Live' : 'Paused'}
            options={['Live', 'Paused']}
            onChange={(value) =>
              void onRefreshModeChange(value === 'Live' ? 'live' : 'paused')
            }
          />
          {refreshMode === 'paused' && (
            <Badge
              status={pendingUpdateCount ? 'warning' : 'default'}
              text={`Paused · ${pendingUpdateCount} updates pending`}
            />
          )}
          {refreshMode === 'live' && remoteUpdatePending && (
            <Badge status="processing" text="Update deferred" />
          )}
        </Space>
        <Space size={8}>
          <Button size="small" onClick={() => setDrawerOpen(true)}>
            Recent Revisions
          </Button>
          <Tooltip title="Refresh latest snapshot">
            <Button
              size="small"
              aria-label="Refresh"
              onClick={() => void (onManualRefresh ?? onReload)()}
            >
              ↻
            </Button>
          </Tooltip>
        </Space>
      </div>

      {actionError && (
        <Alert
          className="sales-action-alert"
          type={conflict ? 'warning' : 'error'}
          showIcon
          message={actionError}
          action={
            conflict ? (
              <Button size="small" onClick={() => void onReload()}>
                Review latest
              </Button>
            ) : undefined
          }
        />
      )}
      <div className="sales-main">
        <div className="sales-grid-shell">
          {isError && (
            <Alert type="error" showIcon message="RFQs could not be loaded." />
          )}
          <Spin spinning={isLoading}>
            <div className="sales-grid" data-testid="rfq-grid">
              <AgGridReact<SalesRfq>
                rowData={rfqs}
                columnDefs={columns}
                defaultColDef={{
                  sortable: true,
                  filter: true,
                  resizable: true,
                  suppressHeaderMenuButton: true,
                }}
                getRowId={({ data }) => String(data.caseId)}
                rowHeight={28}
                headerHeight={30}
                groupHeaderHeight={26}
                tooltipShowDelay={400}
                rowSelection={{
                  mode: 'multiRow',
                  selectAll: 'filtered',
                  enableClickSelection: true,
                  enableSelectionWithoutKeys: false,
                  checkboxes: false,
                  headerCheckbox: false,
                }}
                onGridReady={({ api }) => {
                  setGridApi(api)
                  defaultColumnGroupState.current = initializeGridLayout(
                    api,
                    gridConfigJson,
                  )
                }}
                onRowClicked={({ data }: RowClickedEvent<SalesRfq>) =>
                  data && selectRow(data)
                }
                onSelectionChanged={({
                  api,
                }: SelectionChangedEvent<SalesRfq>) =>
                  setSelectedCaseIds(
                    api.getSelectedRows().map((row) => row.caseId),
                  )
                }
                isExternalFilterPresent={() => filterPreset !== 'all'}
                doesExternalFilterPass={(node: IRowNode<SalesRfq>) =>
                  Boolean(
                    node.data &&
                    matchesSalesPreset(node.data, filterPreset, currentUserId),
                  )
                }
                readOnlyEdit
                onCellEditRequest={(event) => void editAmendment(event)}
                rowClassRules={rowClassRules}
                getContextMenuItems={contextMenu}
                statusBar={statusBar}
                components={{
                  salesShortcutStatus: (value: { text: string }) => (
                    <span className="sales-shortcut-status">{value.text}</span>
                  ),
                }}
              />
            </div>
          </Spin>
        </div>
        <aside className={`sales-work-pane sales-pane-${mode}`}>
          <Tabs
            size="small"
            activeKey={workPaneTab}
            onChange={(key) => setWorkPaneTab(key as 'rfq' | 'bulk')}
            items={[
              {
                key: 'rfq',
                label: 'RFQ',
                children: (
                  <>
                    <WorkPaneHeader mode={mode} row={selected} />
                    {(mode === 'new' || mode === 'draft') && (
                      <SalesRfqEditor
                        form={form}
                        mode={mode}
                        selected={selected}
                        clients={clients}
                        securities={securities}
                        traders={traders}
                        defaultsLoading={defaultsLoading}
                        isMutating={isMutating}
                        onClientSearch={onClientSearch}
                        onSecuritySearch={onSecuritySearch}
                        onSecuritySelect={applyDefaults}
                        onSave={saveDraft}
                        onDiscard={async (row) => {
                          await run(() => onDiscard(row.caseId, row.version))
                        }}
                      />
                    )}
                    {mode === 'neutral' && (
                      <Empty
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                        description="Select an RFQ or choose New RFQ"
                      />
                    )}
                    {!['neutral', 'new', 'draft'].includes(mode) &&
                      selected && (
                        <LifecyclePane
                          row={selected}
                          mode={mode}
                          now={now}
                          isMutating={isMutating}
                          contactOwner={{
                            users,
                            currentUserId,
                            targetContactOwnerId,
                            onTargetContactOwnerChange: setTargetContactOwnerId,
                            onChangeContactOwner: () =>
                              targetContactOwnerId &&
                              void run(async () => {
                                await onChangeContactOwner(
                                  selected.caseId,
                                  targetContactOwnerId,
                                  selected.currentVersion,
                                )
                                setTargetContactOwnerId(undefined)
                              }, refreshMode === 'live'),
                          }}
                          memo={{
                            memoEditing,
                            memoDraft,
                            onMemoEdit: () => {
                              setMemoDraft(selected.salesMemo)
                              setMemoEditing(true)
                            },
                            onMemoChange: setMemoDraft,
                            onMemoCancel: () => setMemoEditing(false),
                            onMemoSave: () =>
                              void run(
                                () =>
                                  onUpdateMemo(
                                    selected.caseId,
                                    memoDraft,
                                    selected.salesMemoVersion,
                                  ),
                                refreshMode === 'live',
                              ),
                          }}
                          onCorrectOutcome={() => {
                            const reason = window.prompt('Correction Reason')
                            if (!reason?.trim()) return
                            void run(
                              () =>
                                onCorrectOutcome(
                                  selected.caseId,
                                  selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                                  selected.currentVersion,
                                  reason.trim(),
                                ),
                              refreshMode === 'live',
                            )
                          }}
                          onCommand={(command) =>
                            void executeCommand(command, selected)
                          }
                        />
                      )}
                  </>
                ),
              },
              {
                key: 'bulk',
                label: `Bulk (${selectedRows.length})`,
                children: (
                  <BulkPane
                    rows={selectedRows}
                    userId={currentUserId}
                    onOpen={openBulk}
                  />
                ),
              },
            ]}
          />
        </aside>
      </div>

      {bulkResult && (
        <SalesBulkResultBar
          result={bulkResult}
          expanded={resultExpanded}
          onToggle={() => setResultExpanded((current) => !current)}
        />
      )}

      <Modal
        open={singleDialog !== null}
        title={
          singleDialog
            ? `${singleDialog.command.toUpperCase()} Case ${singleDialog.row.caseId}`
            : ''
        }
        okText="Apply"
        onCancel={() => setSingleDialog(null)}
        onOk={() => {
          if (singleDialog)
            void executeCommand(singleDialog.command, singleDialog.row)
          setSingleDialog(null)
        }}
      >
        {singleDialog && (
          <p>
            {singleDialog.row.securityJapaneseName} ·{' '}
            {singleDialog.row.clientName}
          </p>
        )}
      </Modal>
      <Modal
        open={bulkDialog !== null}
        title={bulkDialog ? `Bulk ${bulkDialog.command}` : ''}
        okText="Apply"
        onCancel={() => setBulkDialog(null)}
        onOk={() => void applyBulk()}
      >
        <List<BulkSnapshotItem>
          size="small"
          dataSource={bulkDialog?.items ?? []}
          renderItem={(item) => (
            <List.Item>
              <Tag color={item.eligible ? 'green' : 'default'}>
                {item.eligible ? '✓' : '–'}
              </Tag>{' '}
              Case {item.row.caseId} · {item.row.securityJapaneseName}{' '}
              {item.reason}
            </List.Item>
          )}
        />
      </Modal>
      <Drawer
        title="Recent Revisions"
        placement="right"
        size={480}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      >
        <Spin spinning={recentRevisionsLoading}>
          <List
            dataSource={recentRevisions}
            locale={{ emptyText: 'No confirmed revisions yet' }}
            renderItem={(item) => (
              <List.Item className="revision-item">
                <List.Item.Meta
                  title={
                    <Space>
                      <Typography.Text>
                        {new Date(item.occurredAt).toLocaleTimeString([], {
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </Typography.Text>
                      <Tag color={item.kind === 'Quote' ? 'gold' : 'blue'}>
                        {item.kind}
                      </Tag>
                      <Typography.Text>Case {item.caseId}</Typography.Text>
                    </Space>
                  }
                  description={
                    <>
                      <div>
                        {item.securityName} · {item.clientName}
                      </div>
                      {item.changes.map((change) => (
                        <div key={change.field}>
                          <Typography.Text type="secondary">
                            {change.field}
                          </Typography.Text>{' '}
                          {change.before ?? '—'} → {change.after ?? '—'}
                        </div>
                      ))}
                    </>
                  }
                />
              </List.Item>
            )}
          />
        </Spin>
      </Drawer>
    </div>
  )
}
