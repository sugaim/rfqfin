import { type ReactElement, useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert,
  Badge,
  Button,
  Drawer,
  Empty,
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
  IRowNode,
  RowClickedEvent,
  RowClassParams,
  SelectionChangedEvent,
  StatusPanelDef,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  ClientSearchResult,
  SalesRecentRevision,
  SalesRfq,
  SecuritySearchResult,
} from '@/services/api'
import {
  isTextEditingTarget,
  matchesSalesPreset,
  reconcileSelection,
  type SalesRowCommand,
  type SalesFilterPreset,
  type SalesRefreshMode,
} from '@/features/sales/salesModel'
import {
  applyGridLayout,
  initializeGridLayout,
  type GridColumnGroupState,
} from '@/features/grid/gridLayout'
import { buildSalesColumns } from '@/features/sales/salesColumns'
import { SalesRfqEditor } from '@/features/sales/SalesRfqEditor'
import { BulkPane, SalesBulkResultBar } from '@/features/sales/SalesBulkUi'
import { LifecyclePane } from '@/features/sales/work-pane/LifecyclePane'
import { WorkPaneHeader } from '@/features/sales/work-pane/WorkPaneHeader'
import type {
  SalesAmendmentActions,
  SalesBulkActions,
  SalesContactOwnerActions,
  SalesDraftActions,
  SalesGridLayoutActions,
  SalesLifecycleActions,
  SalesLookupActions,
  SalesMemoActions,
  UserOption,
} from '@/features/sales/salesContracts'
import { useSalesOperations } from '@/features/sales/useSalesOperations'
import {
  useSalesBulkOperations,
  type SalesBulkSnapshotItem,
} from '@/features/sales/useSalesBulkOperations'
import { useSalesRfqEditor } from '@/features/sales/useSalesRfqEditor'
import { buildSalesContextMenu } from '@/features/sales/buildSalesContextMenu'
export type { SalesRefreshMode } from '@/features/sales/salesModel'

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
  lookup: SalesLookupActions
  draft: SalesDraftActions
  lifecycle: SalesLifecycleActions
  amendment: SalesAmendmentActions
  contactOwner: SalesContactOwnerActions
  memo: SalesMemoActions
  bulk: SalesBulkActions
  onReload: () => void | Promise<unknown>
  gridLayout?: SalesGridLayoutActions
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
    lookup,
    draft,
    lifecycle,
    amendment,
    contactOwner,
    memo,
    bulk,
    onReload,
    gridLayout = {},
  } = props
  const [gridApi, setGridApi] = useState<GridApi<SalesRfq> | null>(null)
  const defaultColumnGroupState = useRef<GridColumnGroupState>([])
  const [selectedCaseIds, setSelectedCaseIds] = useState<number[]>([])
  const [activeCaseId, setActiveCaseId] = useState<number>()
  const [workPaneTab, setWorkPaneTab] = useState<'rfq' | 'bulk'>('rfq')
  const [filterPreset, setFilterPreset] = useState<SalesFilterPreset>('all')
  const [memoEditing, setMemoEditing] = useState(false)
  const [memoDraft, setMemoDraft] = useState('')
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [drawerOpen, setDrawerOpen] = useState(false)
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
  const operations = useSalesOperations({
    currentUserId,
    refreshMode,
    draft,
    lifecycle,
    amendment,
    onReload,
    onRowActionApplied,
    onSuccess: () => {
      setMemoEditing(false)
      setTargetContactOwnerId(undefined)
    },
  })
  const editor = useSalesRfqEditor({
    rfqs,
    activeCaseId,
    gridApi,
    draft,
    lookup,
    run: operations.run,
    onError: operations.setActionError,
    onStartNew: () => {
      setWorkPaneTab('rfq')
      setActiveCaseId(undefined)
      setSelectedCaseIds([])
      setTargetContactOwnerId(undefined)
    },
    onSelectRow: (row) => {
      setActiveCaseId(row.caseId)
      setMemoDraft(row.salesMemo)
      setMemoEditing(false)
      setTargetContactOwnerId(undefined)
    },
  })
  const bulkController = useSalesBulkOperations({
    selectedRows,
    currentUserId,
    actions: bulk,
    onReload,
    onError: operations.setActionError,
  })
  const { mode, newIntent } = editor
  const isProtected =
    newIntent ||
    mode === 'draft' ||
    memoEditing ||
    operations.singleDialog !== null ||
    bulkController.dialog !== null

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
    if (!gridApi || !gridLayout.configJson) return
    applyGridLayout(
      gridApi,
      gridLayout.configJson,
      defaultColumnGroupState.current,
    )
  }, [gridApi, gridLayout.configJson])

  const editAmendment = async (event: CellEditRequestEvent<SalesRfq>) => {
    const row = event.data
    if (!row || row.revisionStatus === 'Draft') return
    setActiveCaseId(row.caseId)
    const field = event.colDef.field
    await operations.run(
      () =>
        amendment.save(
          row,
          field === 'notional'
            ? Number(event.newValue) * 1_000_000
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

  const contextMenu = (params: GetContextMenuItemsParams<SalesRfq>) =>
    buildSalesContextMenu({
      params,
      currentUserId,
      gridLayout,
      defaultColumnGroupState: defaultColumnGroupState.current,
      onRequestCommand: operations.request,
      onExecuteCommand: (command, row) => void operations.execute(command, row),
    })

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        operations.clearDialog()
        bulkController.cancel()
        setDrawerOpen(false)
        setMemoEditing(false)
        if (newIntent) editor.cancelNew()

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
        editor.startNew()

        return
      }
      if (key === 'enter') {
        event.preventDefault()
        if (newIntent || mode === 'draft') void editor.save(true)
        else if (selected?.draftRevisionId)
          void operations.execute('confirm-amendment', selected)

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
        onRowCommand: (command, row) =>
          void operations.executeRow(command, row),
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
          <Button type="primary" size="small" onClick={editor.startNew}>
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

      {operations.actionError && (
        <Alert
          className="sales-action-alert"
          type={operations.conflict ? 'warning' : 'error'}
          showIcon
          message={operations.actionError}
          action={
            operations.conflict ? (
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
                    gridLayout.configJson,
                  )
                }}
                onRowClicked={({ data }: RowClickedEvent<SalesRfq>) =>
                  data && editor.selectRow(data)
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
                        form={editor.form}
                        mode={mode}
                        selected={selected}
                        clients={clients}
                        securities={securities}
                        traders={traders}
                        defaultsLoading={editor.defaultsLoading}
                        isMutating={isMutating}
                        onClientSearch={lookup.searchClients}
                        onSecuritySearch={lookup.searchSecurities}
                        onSecuritySelect={editor.applyDefaults}
                        onSave={editor.save}
                        onDiscard={editor.discard}
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
                              void operations.run(async () => {
                                await contactOwner.change(
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
                              void operations.run(
                                () =>
                                  memo.update(
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
                            void operations.run(
                              () =>
                                lifecycle.correctOutcome(
                                  selected.caseId,
                                  selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                                  selected.currentVersion,
                                  reason.trim(),
                                ),
                              refreshMode === 'live',
                            )
                          }}
                          onCommand={(command) =>
                            void operations.execute(command, selected)
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
                    onOpen={bulkController.open}
                  />
                ),
              },
            ]}
          />
        </aside>
      </div>

      {bulkController.result && (
        <SalesBulkResultBar
          result={bulkController.result}
          expanded={bulkController.resultExpanded}
          onToggle={bulkController.toggleResult}
        />
      )}

      <Modal
        open={operations.singleDialog !== null}
        title={
          operations.singleDialog
            ? `${operations.singleDialog.command.toUpperCase()} Case ${operations.singleDialog.row.caseId}`
            : ''
        }
        okText="Apply"
        onCancel={operations.clearDialog}
        onOk={() => {
          if (operations.singleDialog)
            void operations.execute(
              operations.singleDialog.command,
              operations.singleDialog.row,
            )
          operations.clearDialog()
        }}
      >
        {operations.singleDialog && (
          <p>
            {operations.singleDialog.row.securityJapaneseName} ·{' '}
            {operations.singleDialog.row.clientName}
          </p>
        )}
      </Modal>
      <Modal
        open={bulkController.dialog !== null}
        title={
          bulkController.dialog ? `Bulk ${bulkController.dialog.command}` : ''
        }
        okText="Apply"
        onCancel={bulkController.cancel}
        onOk={() => void bulkController.apply()}
      >
        <List<SalesBulkSnapshotItem>
          size="small"
          dataSource={bulkController.dialog?.items ?? []}
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
