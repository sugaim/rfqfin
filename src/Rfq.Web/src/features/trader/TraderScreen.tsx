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
import type { InputRef } from 'antd'
import type {
  CellEditRequestEvent,
  GridApi,
  RowClassParams,
  RowClickedEvent,
  SelectionChangedEvent,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  ApiProblemDetails,
  BulkItemResult,
  CalculatedQuotePayload,
  CloseRfqResult,
  ConfirmQuoteResult,
  ContactOwnerResult,
  LifecycleResult,
  MemoResult,
  OwnershipResult,
  PresentationResult,
  RfqSearchItem,
  RfqSearchParams,
  RfqSearchResult,
  TraderRfq,
  WorkingQuoteResult,
} from '@/services/api'
import {
  attentionClass,
  calculatedValue,
  isPickUpEligible,
  isConfirmable,
  patchConfirmedQuote,
  patchMemo,
  patchWorkingQuote,
  sameSourceTerms,
  searchDateRange,
  type PricerProvenance,
  type SearchDatePreset,
  type TraderBulkCommand,
  type TraderPaneTab,
  type TraderRefreshMode,
} from '@/features/trader/traderModel'
import {
  applyGridLayout,
  gridLayoutMenu,
  initializeGridLayout,
  type GridColumnGroupState,
} from '@/features/grid/gridLayout'
import {
  useTraderActiveColumns,
  useTraderConfirmColumns,
  useTraderSearchColumns,
} from '@/features/trader/traderColumns'
import { TraderOperationsPane } from '@/features/trader/TraderOperationsPane'
import { TraderSearchSection } from '@/features/trader/TraderSearchSection'
import { useWorkingQuoteCalculation } from '@/features/trader/useWorkingQuoteCalculation'
import {
  TraderPricerPane,
  type ScratchState,
} from '@/features/trader/TraderPricerPane'
import {
  TraderResultBar,
  type TraderResultState,
} from '@/features/trader/TraderResultBar'

type UserOption = { userId: string; name: string }
type RfqOutcome = 'Hit' | 'Away'
type GridConfigKey = 'main' | 'search' | 'confirm'

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
  onPickUp: (
    row: TraderRfq,
    confirmed: boolean,
  ) => Promise<OwnershipResult | void>
  onRelease: (row: TraderRfq) => Promise<OwnershipResult | void>
  onAssign: (
    row: TraderRfq,
    targetTraderId: string,
  ) => Promise<OwnershipResult | void>
  onTakeOver: (row: TraderRfq) => Promise<OwnershipResult | void>
  onCalculate: (
    row: TraderRfq,
    driver: CalculatedQuotePayload['driver'],
    value: number,
    simpleYieldSlide: number,
  ) => Promise<WorkingQuoteResult | void>
  onChangeMode: (
    row: TraderRfq,
    mode: 'Calculated' | 'Manual',
  ) => Promise<WorkingQuoteResult | void>
  onUpdateManual: (
    row: TraderRfq,
    price: number | null,
    finalSimpleYield: number | null,
  ) => Promise<WorkingQuoteResult | void>
  onConfirmQuote: (
    row: TraderRfq,
    expiryMinutes: number | null,
  ) => Promise<ConfirmQuoteResult | void>
  onPresent?: (row: TraderRfq) => Promise<PresentationResult | void>
  onUnpresent?: (row: TraderRfq) => Promise<PresentationResult | void>
  onWithdraw?: (row: TraderRfq) => Promise<LifecycleResult | void>
  onClose: (
    row: TraderRfq,
    outcome: RfqOutcome,
  ) => Promise<CloseRfqResult | void>
  onCancel?: (row: TraderRfq) => Promise<LifecycleResult | void>
  onReopen?: (row: TraderRfq) => Promise<LifecycleResult | void>
  onCorrectOutcome: (
    row: TraderRfq,
    outcome: RfqOutcome,
    reason: string,
  ) => Promise<CloseRfqResult | void>
  onChangeContactOwner: (
    row: TraderRfq,
    targetUserId: string,
  ) => Promise<ContactOwnerResult | void>
  onUpdateMemo: (row: TraderRfq, memo: string) => Promise<MemoResult | void>
  onBulk?: (
    command: TraderBulkCommand,
    rows: TraderRfq[],
    expiryMinutes: number | null,
    targetTraderId?: string,
  ) => Promise<BulkItemResult[]>
  onSearch?: (params: RfqSearchParams) => Promise<RfqSearchResult>
  onScratchPrice?: (input: {
    securityId: string
    settlementDate: string
    driver: CalculatedQuotePayload['driver']
    value: number
    simpleYieldSlide: number
  }) => Promise<CalculatedQuotePayload>
  mainGridConfigJson?: string
  searchGridConfigJson?: string
  confirmGridConfigJson?: string
  onSaveGridConfig?: (key: GridConfigKey, configJson: string) => Promise<void>
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
    onPickUp,
    onRelease,
    onAssign,
    onTakeOver,
    onCalculate,
    onChangeMode,
    onUpdateManual,
    onConfirmQuote,
    onPresent,
    onUnpresent,
    onWithdraw,
    onClose,
    onCancel,
    onReopen,
    onCorrectOutcome,
    onChangeContactOwner,
    onUpdateMemo,
    onBulk = async () => [],
    onSearch = async () => ({ items: [], requiresNarrowing: false }),
    onScratchPrice,
    mainGridConfigJson,
    searchGridConfigJson,
    confirmGridConfigJson,
    onSaveGridConfig,
    onReload,
  } = props
  const [selectedCaseIds, setSelectedCaseIds] = useState<number[]>([])
  const [activeCaseId, setActiveCaseId] = useState<number>()
  const [rightPaneOpen, setRightPaneOpen] = useState(true)
  const [rightTab, setRightTab] = useState<TraderPaneTab>('operations')
  const [searchOpen, setSearchOpen] = useState(true)
  const [searchFiltersOpen, setSearchFiltersOpen] = useState(true)
  const [searchPreset, setSearchPreset] = useState<SearchDatePreset>('1Y')
  const [searchFilters, setSearchFilters] = useState<Record<string, string>>({})
  const [searchResult, setSearchResult] = useState<RfqSearchResult>({
    items: [],
    requiresNarrowing: false,
  })
  const [searching, setSearching] = useState(false)
  const [confirmRows, setConfirmRows] = useState<TraderRfq[] | null>(null)
  const [result, setResult] = useState<TraderResultState | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)
  const [targetTraderId, setTargetTraderId] = useState<string>()
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [expiryMinutes, setExpiryMinutes] = useState<number | null>(
    defaultExpiryMinutes,
  )
  const [actionError, setActionError] = useState<string | null>(null)
  const [scratch, setScratch] = useState<ScratchState>({
    securityId: '',
    notional: null,
    settlementDate: '',
    driver: 'Price',
    value: 100,
    slide: 0,
  })
  const [scratchResult, setScratchResult] =
    useState<CalculatedQuotePayload | null>(null)
  const [pricerProvenance, setPricerProvenance] =
    useState<PricerProvenance | null>(null)
  const [pricerBusy, setPricerBusy] = useState(false)
  const [editingCells, setEditingCells] = useState(0)
  const [now, setNow] = useState(() => Date.now())
  const mainGrid = useRef<GridApi<TraderRfq> | null>(null)
  const searchGrid = useRef<GridApi<RfqSearchItem> | null>(null)
  const confirmGrid = useRef<GridApi<TraderRfq> | null>(null)
  const defaultColumnGroupStates = useRef<
    Record<GridConfigKey, GridColumnGroupState>
  >({ main: [], search: [], confirm: [] })
  const activeGridRegion = useRef<HTMLDivElement>(null)
  const searchCaseInput = useRef<InputRef>(null)

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

  const configFor = (key: GridConfigKey) =>
    key === 'main'
      ? mainGridConfigJson
      : key === 'search'
        ? searchGridConfigJson
        : confirmGridConfigJson

  const layoutMenu = <T,>(key: GridConfigKey, api: GridApi<T>) =>
    gridLayoutMenu({
      api,
      configJson: configFor(key),
      defaultColumnGroupState: defaultColumnGroupStates.current[key],
      onSave: onSaveGridConfig
        ? (configJson) => onSaveGridConfig(key, configJson)
        : undefined,
    })

  useEffect(() => {
    if (mainGrid.current)
      applyGridLayout(
        mainGrid.current,
        mainGridConfigJson,
        defaultColumnGroupStates.current.main,
      )
  }, [mainGridConfigJson])

  useEffect(() => {
    if (searchGrid.current)
      applyGridLayout(
        searchGrid.current,
        searchGridConfigJson,
        defaultColumnGroupStates.current.search,
      )
  }, [searchGridConfigJson])

  useEffect(() => {
    if (confirmGrid.current)
      applyGridLayout(
        confirmGrid.current,
        confirmGridConfigJson,
        defaultColumnGroupStates.current.confirm,
      )
  }, [confirmGridConfigJson])

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
    onCalculate,
    onUpdateManual,
    onPatchRow,
    onSuccess: reconcileAfterSuccess,
  })
  const protectedState =
    calculating || editingCells > 0 || confirmRows !== null || pricerBusy

  useEffect(
    () => onTransientStateChange?.(protectedState),
    [onTransientStateChange, protectedState],
  )

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
      const items = await onBulk(command, rows, expiryMinutes, assignedTraderId)
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
      () => onUpdateMemo(row, attempted),
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
        () => onConfirmQuote(row, expiryMinutes),
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

  const executeSearch = async () => {
    setSearching(true)
    setActionError(null)
    try {
      const range = searchDateRange(businessDate, searchPreset)
      const params: RfqSearchParams = { ...range }
      for (const [key, value] of Object.entries(searchFilters)) {
        if (!value.trim()) continue
        if (key === 'caseId') params.caseId = Number(value)
        else (params as Record<string, unknown>)[key] = value.trim()
      }
      setSearchResult(await onSearch(params))
    } catch {
      setActionError('RFQ Search could not be completed.')
    } finally {
      setSearching(false)
    }
  }

  const setScratchIdentity = (
    key: 'securityId' | 'notional' | 'settlementDate',
    value: string | number | null,
  ) => {
    setScratch((current) => ({ ...current, [key]: value }))
    setPricerProvenance(null)
  }
  const loadSelected = () => {
    if (!selected) return
    const driver = selected.calculated?.driver ?? 'Price'
    setScratch({
      securityId: selected.securityId,
      notional: selected.notional,
      settlementDate: selected.settlementDate ?? '',
      driver,
      value:
        calculatedValue(selected.calculated, driver) ??
        selected.manual?.price ??
        100,
      slide: selected.calculated?.simpleYieldSlide ?? 0,
    })
    setScratchResult(null)
    setPricerProvenance({
      sourceCaseId: selected.caseId,
      securityId: selected.securityId,
      notional: selected.notional,
      settlementDate: selected.settlementDate,
    })
  }
  const sourceRow = pricerProvenance
    ? rfqs.find((row) => row.caseId === pricerProvenance.sourceCaseId)
    : undefined
  const canApplyPricer = Boolean(
    sourceRow &&
    sourceRow.workingQuoteMode === 'Manual' &&
    sameSourceTerms(sourceRow, pricerProvenance) &&
    scratchResult,
  )
  const calculateScratch = async () => {
    if (!onScratchPrice) return
    setPricerBusy(true)
    try {
      setScratchResult(
        await onScratchPrice({
          securityId: scratch.securityId,
          settlementDate: scratch.settlementDate,
          driver: scratch.driver,
          value: scratch.value,
          simpleYieldSlide: scratch.slide,
        }),
      )
    } catch {
      setActionError('Scratch calculation failed.')
    } finally {
      setPricerBusy(false)
    }
  }
  const applyPricer = async () => {
    if (!sourceRow || !scratchResult || !canApplyPricer) return
    await runSingle(
      () =>
        onUpdateManual(
          sourceRow,
          scratchResult.price,
          scratchResult.finalSimpleYield,
        ),
      patchWorkingQuote,
      sourceRow,
    )
  }

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
        setSearchOpen(true)
        window.setTimeout(() => searchCaseInput.current?.focus(), 0)
      } else if (event.key.toLowerCase() === 'a') {
        event.preventDefault()
        activeGridRegion.current?.focus()
      } else if (
        event.key === 'Enter' &&
        !isTextInput(event.target) &&
        document.activeElement !== searchCaseInput.current
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
                  onGridReady={({ api }) => {
                    mainGrid.current = api
                    defaultColumnGroupStates.current.main =
                      initializeGridLayout(api, mainGridConfigJson)
                  }}
                  getContextMenuItems={({ api }) => [layoutMenu('main', api)]}
                  onRowClicked={({ data }: RowClickedEvent<TraderRfq>) => {
                    if (data) {
                      setActiveCaseId(data.caseId)
                      setTargetTraderId(undefined)
                      setTargetContactOwnerId(undefined)
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
            open={searchOpen}
            filtersOpen={searchFiltersOpen}
            caseInputRef={searchCaseInput}
            preset={searchPreset}
            filters={searchFilters}
            searching={searching}
            result={searchResult}
            columns={searchColumns}
            onToggle={() => setSearchOpen((value) => !value)}
            onToggleFilters={() => setSearchFiltersOpen((value) => !value)}
            onPresetChange={setSearchPreset}
            onFiltersChange={setSearchFilters}
            onSearch={executeSearch}
            onGridReady={(api) => {
              searchGrid.current = api
              defaultColumnGroupStates.current.search = initializeGridLayout(
                api,
                searchGridConfigJson,
              )
            }}
            getLayoutMenu={(api) => layoutMenu('search', api)}
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
                      targetTraderId={targetTraderId}
                      setTargetTraderId={setTargetTraderId}
                      targetContactOwnerId={targetContactOwnerId}
                      setTargetContactOwnerId={setTargetContactOwnerId}
                      run={runSingle}
                      runBulk={runBulk}
                      ownership={{
                        pickUp: onPickUp,
                        release: onRelease,
                        assign: onAssign,
                        takeOver: onTakeOver,
                      }}
                      quote={{
                        changeMode: onChangeMode,
                        withdraw: onWithdraw,
                      }}
                      lifecycle={{
                        present: onPresent,
                        unpresent: onUnpresent,
                        close: onClose,
                        cancel: onCancel,
                        reopen: onReopen,
                        correctOutcome: onCorrectOutcome,
                      }}
                      contactOwner={{ change: onChangeContactOwner }}
                    />
                  ),
                },
                {
                  key: 'pricer',
                  label: 'Pricer',
                  children: (
                    <TraderPricerPane
                      selected={selected}
                      scratch={scratch}
                      setScratch={setScratch}
                      setScratchIdentity={setScratchIdentity}
                      result={scratchResult}
                      provenance={pricerProvenance}
                      sourceRow={sourceRow}
                      canApply={canApplyPricer}
                      busy={pricerBusy}
                      onLoad={loadSelected}
                      onCalculate={() => void calculateScratch()}
                      onApply={() => void applyPricer()}
                      onClear={() => {
                        setScratch({
                          securityId: '',
                          notional: null,
                          settlementDate: '',
                          driver: 'Price',
                          value: 100,
                          slide: 0,
                        })
                        setScratchResult(null)
                        setPricerProvenance(null)
                      }}
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
            onGridReady={({ api }) => {
              confirmGrid.current = api
              defaultColumnGroupStates.current.confirm = initializeGridLayout(
                api,
                confirmGridConfigJson,
              )
            }}
            getContextMenuItems={({ api }) => [layoutMenu('confirm', api)]}
          />
        </div>
      </Modal>
    </div>
  )
}
