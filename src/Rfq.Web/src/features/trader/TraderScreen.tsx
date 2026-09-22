import { type ReactElement, useEffect, useRef, useState } from 'react'
import {
  Alert,
  Badge,
  Button,
  Modal,
  Segmented,
  Select,
  Space,
  Spin,
  Tabs,
  Tooltip,
  Typography,
} from 'antd'
import type {
  CellEditRequestEvent,
  RowClassParams,
  RowClickedEvent,
  SelectionChangedEvent,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type { ApiProblemDetails, TraderRfq } from '@/services/api'
import {
  attentionClass,
  isPickUpEligible,
  isConfirmable,
  patchConfirmedQuote,
  patchMemo,
  patchWorkingQuote,
  type TraderBulkCommand,
  type TraderPaneTab,
  type TraderRefreshMode,
} from '@/features/trader/traderModel'
import {
  useTraderActiveColumns,
  useTraderConfirmColumns,
  useTraderSearchColumns,
} from '@/features/trader/traderColumns'
import { TraderOperationsPane } from '@/features/trader/operations/TraderOperationsPane'
import { useTraderOperationIntents } from '@/features/trader/operations/useTraderOperationIntents'
import { TraderSearchSection } from '@/features/trader/TraderSearchSection'
import { useWorkingQuoteCalculation } from '@/features/trader/useWorkingQuoteCalculation'
import { TraderPricerPane } from '@/features/trader/TraderPricerPane'
import {
  TraderResultBar,
  type TraderResultState,
} from '@/features/trader/TraderResultBar'
import type {
  TraderBulkActions,
  TraderContactOwnerActions,
  TraderGridLayoutActions,
  TraderLifecycleActions,
  TraderMemoActions,
  TraderOwnershipActions,
  TraderPricerActions,
  TraderSearchActions,
  TraderWorkingQuoteActions,
  UserOption,
} from '@/features/trader/traderContracts'
import { useTraderGridLayouts } from '@/features/trader/useTraderGridLayouts'
import { useTraderScratchPricer } from '@/features/trader/useTraderScratchPricer'
import { useTraderSearch } from '@/features/trader/useTraderSearch'

function isTextInput(target: EventTarget | null): boolean {
  const element = target instanceof HTMLElement ? target : null

  return Boolean(
    element &&
    (element.isContentEditable ||
      element.closest(
        'input, textarea, select, [contenteditable="true"], .ant-select',
      )),
  )
}

export interface TraderScreenProps {
  rfqs: TraderRfq[]
  traders: UserOption[]
  users: UserOption[]
  currentUserId: string
  businessDate?: string
  defaultExpiryMinutes: number | null
  isLoading: boolean
  isError: boolean
  isMutating: boolean
  refreshMode?: TraderRefreshMode
  pendingUpdateCount?: number
  remoteUpdatePending?: boolean
  refreshGeneration?: number
  onRefreshModeChange?: (mode: TraderRefreshMode) => void | Promise<void>
  onManualRefresh?: () => void | Promise<void>
  onTransientStateChange?: (active: boolean) => void
  onPatchRow?: (caseId: number, update: (row: TraderRfq) => TraderRfq) => void
  ownership: TraderOwnershipActions
  workingQuote: TraderWorkingQuoteActions
  lifecycle: TraderLifecycleActions
  contactOwner: TraderContactOwnerActions
  memo: TraderMemoActions
  bulk?: TraderBulkActions
  search?: TraderSearchActions
  pricer?: TraderPricerActions
  gridLayout?: TraderGridLayoutActions
  onReload: () => void | Promise<unknown>
}

export function TraderScreen(props: TraderScreenProps): ReactElement {
  const {
    rfqs,
    traders,
    users,
    currentUserId,
    businessDate = new Date().toISOString().slice(0, 10),
    defaultExpiryMinutes,
    isLoading,
    isError,
    isMutating,
    refreshMode = 'live',
    pendingUpdateCount = 0,
    remoteUpdatePending = false,
    refreshGeneration = 0,
    onRefreshModeChange = async () => undefined,
    onManualRefresh,
    onTransientStateChange,
    onPatchRow = () => undefined,
    ownership,
    workingQuote,
    lifecycle,
    contactOwner,
    memo,
    bulk = { execute: async () => [] },
    search = {
      execute: async () => ({ items: [], requiresNarrowing: false }),
    },
    pricer: pricerActions,
    gridLayout = { configs: {} },
    onReload,
  } = props
  const [selectedCaseIds, setSelectedCaseIds] = useState<number[]>([])
  const [activeCaseId, setActiveCaseId] = useState<number>()
  const [rightPaneOpen, setRightPaneOpen] = useState(true)
  const [rightTab, setRightTab] = useState<TraderPaneTab>('operations')
  const [confirmRows, setConfirmRows] = useState<TraderRfq[] | null>(null)
  const [result, setResult] = useState<TraderResultState | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)
  const [expiryMinutes, setExpiryMinutes] = useState<number | null>(
    defaultExpiryMinutes,
  )
  const [actionError, setActionError] = useState<string | null>(null)
  const [editingCells, setEditingCells] = useState(0)
  const [now, setNow] = useState(() => Date.now())
  const activeGridRegion = useRef<HTMLDivElement>(null)

  const selectedRows = selectedCaseIds
    .map((id) => rfqs.find((row) => row.caseId === id))
    .filter((row): row is TraderRfq => Boolean(row))
  const selected = rfqs.find((row) => row.caseId === activeCaseId)
  const confirmableRows = selectedRows.filter((row) =>
    isConfirmable(row, currentUserId),
  )
  const pickTargets = rfqs.filter(
    (row) => row.assignedTraderId === currentUserId && isPickUpEligible(row),
  )
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000)

    return () => window.clearInterval(timer)
  }, [])
  useEffect(() => {
    setSelectedCaseIds((ids) =>
      ids.filter((id) => rfqs.some((row) => row.caseId === id)),
    )
    if (
      activeCaseId !== undefined &&
      !rfqs.some((row) => row.caseId === activeCaseId)
    )
      setActiveCaseId(undefined)
  }, [activeCaseId, rfqs])

  const gridLayouts = useTraderGridLayouts(gridLayout)
  const searchController = useTraderSearch({
    businessDate,
    actions: search,
    onError: setActionError,
  })

  const reconcileAfterSuccess = async () => {
    if (refreshMode === 'live') await onReload()
  }
  const {
    calcStates,
    calculating,
    editQuote: editWorkingQuote,
    invalidate: invalidateCalculations,
  } = useWorkingQuoteCalculation({
    currentUserId,
    refreshGeneration,
    onCalculate: workingQuote.calculate,
    onUpdateManual: workingQuote.updateManual,
    onPatchRow,
    onSuccess: reconcileAfterSuccess,
  })

  const runSingle = async <T,>(
    action: () => Promise<T | void>,
    patch?: (row: TraderRfq, value: T) => TraderRfq,
    row = selected,
  ) => {
    setActionError(null)
    try {
      const value = await action()
      if (refreshMode === 'paused' && row && value !== undefined && patch)
        onPatchRow(row.caseId, (current) => patch(current, value as T))
      await reconcileAfterSuccess()

      return value
    } catch (error) {
      const detail = (error as { data?: ApiProblemDetails }).data?.detail
      setActionError(detail ?? 'The Trader operation could not be completed.')

      return undefined
    }
  }

  const showPane = (tab: TraderPaneTab) => {
    setRightPaneOpen(true)
    setRightTab(tab)
  }
  const runBulk = async (
    label: string,
    command: TraderBulkCommand,
    rows: TraderRfq[],
    assignedTraderId?: string,
  ) => {
    setActionError(null)
    try {
      const items = await bulk.execute(
        command,
        rows,
        expiryMinutes,
        assignedTraderId,
      )
      setResult({ label, items })
      setResultExpanded(false)
      await reconcileAfterSuccess()
    } catch {
      setActionError(`${label} could not be completed.`)
    }
  }

  const editQuote = async (
    event: CellEditRequestEvent<TraderRfq>,
  ): Promise<void> => {
    const row = event.data
    if (event.column.getColId() !== 'traderMemo') {
      await editWorkingQuote(event)

      return
    }

    const attempted = String(event.newValue ?? '')
    const response = await runSingle(
      () => memo.update(row, attempted),
      patchMemo,
      row,
    )
    if (!response)
      event.api.refreshCells({ rowNodes: [event.node], force: true })
  }

  const activeColumns = useTraderActiveColumns({
    calcStates,
    currentUserId,
    now,
  })
  const searchColumns = useTraderSearchColumns()
  const confirmColumns = useTraderConfirmColumns(expiryMinutes)

  const openConfirmation = () => {
    if (confirmableRows.length) setConfirmRows(confirmableRows)
  }
  const applyConfirmation = async () => {
    const rows = confirmRows ?? []
    setConfirmRows(null)
    if (rows.length === 1) {
      const row = rows[0]
      const response = await runSingle(
        () => workingQuote.confirm(row, expiryMinutes),
        patchConfirmedQuote,
        row,
      )
      if (response)
        setResult({
          label: 'Confirm',
          items: [
            {
              caseId: row.caseId,
              status: 'Succeeded',
              code: null,
              message: null,
            },
          ],
        })

      return
    }
    if (rows.length > 1) await runBulk('Bulk Confirm', 'confirm', rows)
  }

  const operationController = useTraderOperationIntents({
    selected,
    selectedRows,
    currentUserId,
    run: runSingle,
    runBulk,
    ownership,
    workingQuote,
    lifecycle,
    contactOwner,
  })
  const pricerController = useTraderScratchPricer({
    rfqs,
    selected,
    actions: pricerActions,
    onApply: async (row, price, finalSimpleYield) => {
      await runSingle(
        () => workingQuote.updateManual(row, price, finalSimpleYield),
        patchWorkingQuote,
        row,
      )
    },
    onError: setActionError,
  })
  const protectedState =
    calculating ||
    editingCells > 0 ||
    confirmRows !== null ||
    pricerController.busy

  useEffect(
    () => onTransientStateChange?.(protectedState),
    [onTransientStateChange, protectedState],
  )

  const manualRefresh = async (): Promise<void> => {
    invalidateCalculations()
    await (onManualRefresh ?? onReload)()
  }

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (!event.altKey) {
        if (event.key === 'Escape') {
          setConfirmRows(null)
        }

        return
      }
      if (event.key.toLowerCase() === 'l') {
        event.preventDefault()
        void onRefreshModeChange(refreshMode === 'live' ? 'paused' : 'live')
      } else if (event.key.toLowerCase() === 's') {
        event.preventDefault()
        searchController.openAndFocus()
      } else if (event.key.toLowerCase() === 'a') {
        event.preventDefault()
        activeGridRegion.current?.focus()
      } else if (
        event.key === 'Enter' &&
        !isTextInput(event.target) &&
        document.activeElement !== searchController.caseInputRef.current
      ) {
        event.preventDefault()
        openConfirmation()
      }
    }
    window.addEventListener('keydown', handler)

    return () => window.removeEventListener('keydown', handler)
  })

  const rowClassRules = {
    'trader-row-selected': ({ node }: RowClassParams<TraderRfq>) =>
      Boolean(node.isSelected()),
    'trader-row-attention-high': ({ data }: RowClassParams<TraderRfq>) =>
      Boolean(
        data &&
        attentionClass(data, currentUserId) === 'trader-row-attention-high',
      ),
    'trader-row-attention-work': ({ data }: RowClassParams<TraderRfq>) =>
      Boolean(
        data &&
        attentionClass(data, currentUserId) === 'trader-row-attention-work',
      ),
    'trader-row-terminal': ({ data }: RowClassParams<TraderRfq>) =>
      Boolean(
        data && attentionClass(data, currentUserId) === 'trader-row-terminal',
      ),
  }

  return (
    <div className="trader-blotter">
      <div className="trader-toolbar">
        <Space size={6}>
          <Button
            size="small"
            disabled={!pickTargets.length || isMutating}
            onClick={() => void runBulk('Pick', 'pick', pickTargets)}
          >
            Pick ({pickTargets.length})
          </Button>
          <Button
            size="small"
            type="primary"
            disabled={!confirmableRows.length || isMutating}
            onClick={openConfirmation}
          >
            Confirm
          </Button>
          <Button size="small" onClick={() => showPane('operations')}>
            Ops
          </Button>
          <Button size="small" onClick={() => showPane('pricer')}>
            Pricer
          </Button>
        </Space>
        <Space size={7}>
          <Segmented
            size="small"
            aria-label="Trader refresh mode"
            value={refreshMode === 'live' ? 'Live' : 'Paused'}
            options={['Live', 'Paused']}
            onChange={(value) =>
              void onRefreshModeChange(value === 'Live' ? 'live' : 'paused')
            }
          />
          {refreshMode === 'paused' && (
            <Badge
              status={pendingUpdateCount ? 'warning' : 'default'}
              text={`${pendingUpdateCount} pending`}
            />
          )}
          {refreshMode === 'live' && remoteUpdatePending && (
            <Badge status="processing" text="Deferred" />
          )}
          <Tooltip title="Refresh">
            <Button
              size="small"
              aria-label="Refresh"
              onClick={() => void manualRefresh()}
            >
              ↻
            </Button>
          </Tooltip>
        </Space>
      </div>
      {isError && (
        <Alert
          type="error"
          showIcon
          message="Trader RFQs could not be loaded."
        />
      )}
      {actionError && (
        <Alert
          closable
          onClose={() => setActionError(null)}
          type="error"
          showIcon
          message={actionError}
        />
      )}

      <div
        className={`trader-workspace ${rightPaneOpen ? '' : 'trader-pane-collapsed'}`}
      >
        <div className="trader-left-workspace">
          <section className="trader-active-section" aria-label="Active RFQs">
            <header>
              <Typography.Text strong>Active RFQs</Typography.Text>
              <span className="trader-shortcuts">
                Esc · Alt+L · Alt+S · Alt+A · Alt+Enter
              </span>
            </header>
            <Spin spinning={isLoading}>
              <div
                className="trader-active-grid"
                ref={activeGridRegion}
                tabIndex={-1}
                data-testid="trader-rfq-grid"
              >
                <AgGridReact<TraderRfq>
                  rowData={rfqs}
                  columnDefs={activeColumns}
                  getRowId={({ data }) => String(data.caseId)}
                  readOnlyEdit
                  singleClickEdit={false}
                  stopEditingWhenCellsLoseFocus
                  onCellEditRequest={(event) => void editQuote(event)}
                  onCellEditingStarted={() =>
                    setEditingCells((value) => value + 1)
                  }
                  onCellEditingStopped={() =>
                    setEditingCells((value) => Math.max(0, value - 1))
                  }
                  rowSelection={{
                    mode: 'multiRow',
                    selectAll: 'filtered',
                    enableClickSelection: true,
                    enableSelectionWithoutKeys: false,
                    checkboxes: false,
                    headerCheckbox: false,
                  }}
                  rowClassRules={rowClassRules}
                  defaultColDef={{
                    sortable: true,
                    filter: true,
                    resizable: true,
                    suppressHeaderMenuButton: true,
                  }}
                  rowHeight={28}
                  headerHeight={30}
                  groupHeaderHeight={26}
                  tooltipShowDelay={350}
                  onGridReady={({ api }) => gridLayouts.initialize('main', api)}
                  getContextMenuItems={({ api }) => [
                    gridLayouts.menu('main', api),
                  ]}
                  onRowClicked={({ data }: RowClickedEvent<TraderRfq>) => {
                    if (data) {
                      setActiveCaseId(data.caseId)
                      operationController.clearTargets()
                    }
                  }}
                  onSelectionChanged={({
                    api,
                  }: SelectionChangedEvent<TraderRfq>) =>
                    setSelectedCaseIds(
                      api.getSelectedRows().map((row) => row.caseId),
                    )
                  }
                />
              </div>
            </Spin>
          </section>

          <TraderSearchSection
            open={searchController.open}
            filtersOpen={searchController.filtersOpen}
            caseInputRef={searchController.caseInputRef}
            preset={searchController.preset}
            filters={searchController.filters}
            searching={searchController.searching}
            result={searchController.result}
            columns={searchColumns}
            onToggle={searchController.toggle}
            onToggleFilters={searchController.toggleFilters}
            onPresetChange={searchController.setPreset}
            onFiltersChange={searchController.setFilters}
            onSearch={searchController.execute}
            onGridReady={(api) => gridLayouts.initialize('search', api)}
            getLayoutMenu={(api) => gridLayouts.menu('search', api)}
          />
        </div>

        {rightPaneOpen ? (
          <aside className="trader-side-pane">
            <Button
              className="trader-pane-close"
              type="text"
              size="small"
              aria-label="Collapse right pane"
              onClick={() => setRightPaneOpen(false)}
            >
              ×
            </Button>
            <Tabs
              size="small"
              activeKey={rightTab}
              onChange={(key) => setRightTab(key as TraderPaneTab)}
              items={[
                {
                  key: 'operations',
                  label: 'Operations',
                  children: (
                    <TraderOperationsPane
                      selected={selected}
                      selectedRows={selectedRows}
                      currentUserId={currentUserId}
                      traders={traders}
                      users={users}
                      isMutating={isMutating}
                      controller={operationController}
                    />
                  ),
                },
                {
                  key: 'pricer',
                  label: 'Pricer',
                  children: (
                    <TraderPricerPane
                      selected={selected}
                      scratch={pricerController.scratch}
                      setScratch={pricerController.setScratch}
                      setScratchIdentity={pricerController.setIdentity}
                      result={pricerController.result}
                      provenance={pricerController.provenance}
                      sourceRow={pricerController.sourceRow}
                      canApply={pricerController.canApply}
                      busy={pricerController.busy}
                      onLoad={pricerController.loadSelected}
                      onCalculate={() => void pricerController.calculate()}
                      onApply={() => void pricerController.apply()}
                      onClear={pricerController.clear}
                    />
                  ),
                },
              ]}
            />
          </aside>
        ) : (
          <Button
            className="trader-pane-reopen"
            size="small"
            onClick={() => setRightPaneOpen(true)}
          >
            Pane
          </Button>
        )}
      </div>

      {result && (
        <TraderResultBar
          result={result}
          expanded={resultExpanded}
          onToggle={() => setResultExpanded((value) => !value)}
        />
      )}

      <Modal
        title="Quote Confirmation"
        open={confirmRows !== null}
        onCancel={() => setConfirmRows(null)}
        onOk={() => void applyConfirmation()}
        okText="Apply"
        width={940}
        destroyOnHidden
      >
        <div className="confirm-toolbar">
          <Select
            size="small"
            aria-label="Confirmation expiry"
            value={expiryMinutes === null ? 'none' : String(expiryMinutes)}
            onChange={(value) =>
              setExpiryMinutes(value === 'none' ? null : Number(value))
            }
            options={[
              { value: 'none', label: 'Expiry: None' },
              ...[5, 15, 30].map((value) => ({
                value: String(value),
                label: `Expiry: ${value} min`,
              })),
            ]}
          />
        </div>
        <div className="trader-confirm-grid">
          <AgGridReact<TraderRfq>
            rowData={confirmRows ?? []}
            columnDefs={confirmColumns}
            getRowId={({ data }) => String(data.caseId)}
            rowHeight={28}
            headerHeight={30}
            defaultColDef={{ resizable: true }}
            onGridReady={({ api }) => gridLayouts.initialize('confirm', api)}
            getContextMenuItems={({ api }) => [
              gridLayouts.menu('confirm', api),
            ]}
          />
        </div>
      </Modal>
    </div>
  )
}
