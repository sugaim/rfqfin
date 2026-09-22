import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert,
  Badge,
  Button,
  Descriptions,
  Empty,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Segmented,
  Select,
  Space,
  Spin,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type { InputRef } from 'antd'
import type {
  CellEditRequestEvent,
  ColDef,
  ColGroupDef,
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
  canEditQuote,
  elapsedLabel,
  isConfirmable,
  patchConfirmedQuote,
  patchContactOwner,
  patchLifecycle,
  patchMemo,
  patchOwnership,
  patchPresentation,
  patchWorkingQuote,
  sameSourceTerms,
  searchDateRange,
  traderRouting,
  traderState,
  type CalcState,
  type PricerProvenance,
  type SearchDatePreset,
  type TraderBulkCommand,
  type TraderPaneTab,
  type TraderRefreshMode,
} from '@/features/trader/traderModel'

type UserOption = { userId: string; name: string }
type RfqOutcome = 'Hit' | 'Away'
type GridConfigKey = 'main' | 'search' | 'confirm'
type ResultState = { label: string; items: BulkItemResult[] }
type ScratchState = {
  securityId: string
  notional: number | null
  settlementDate: string
  driver: CalculatedQuotePayload['driver']
  value: number
  slide: number
}

const million = 1_000_000
const calcTimeoutMs = 10_000
const datePresets: SearchDatePreset[] = ['1M', '3M', '6M', '1Y', '2Y', '5Y']
const driverOptions: {
  value: CalculatedQuotePayload['driver']
  label: string
}[] = [
  { value: 'Price', label: 'Price' },
  { value: 'BbgYield', label: 'BBG Yield' },
  { value: 'SimpleYield', label: 'Simple Yield' },
  { value: 'Ysc', label: 'YSC' },
  { value: 'GSpread', label: 'G-Spread' },
  { value: 'Asw', label: 'ASW' },
  { value: 'ISpread', label: 'I-Spread' },
  { value: 'ZSpread', label: 'Z-Spread' },
]

const formatNumber = (value: unknown) =>
  value == null
    ? ''
    : Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 })
const formatPercent = (value: unknown) =>
  value == null ? '' : `${formatNumber(value)}%`
const formatBp = (value: unknown) =>
  value == null ? '' : `${formatNumber(value)} bp`

function isTextInput(target: EventTarget | null) {
  const element = target instanceof HTMLElement ? target : null
  return Boolean(
    element &&
    (element.isContentEditable ||
      element.closest(
        'input, textarea, select, [contenteditable="true"], .ant-select',
      )),
  )
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(
      () => reject(new Error('Calculation timed out.')),
      timeoutMs,
    )
    promise.then(
      (value) => {
        window.clearTimeout(timer)
        resolve(value)
      },
      (error) => {
        window.clearTimeout(timer)
        reject(error)
      },
    )
  })
}

function failureFrom(error: unknown, requestId: number): CalcState {
  const apiError = error as { data?: ApiProblemDetails; message?: string }
  const detail = apiError.data
  return {
    status: 'failed',
    requestId,
    code: detail?.calculationErrorCode ?? detail?.code,
    message: detail?.detail ?? apiError.message ?? 'Calculation failed.',
    traceId: detail?.traceId,
    failureLogId: detail?.failureLogId,
  }
}

export interface TraderScreenProps {
  rfqs: TraderRfq[]
  traders: UserOption[]
  users: UserOption[]
  currentUserId: string
  businessDate?: string
  defaultExpiryMinutes: number | null
  defaultQuoteMode?: 'Calculated' | 'Manual'
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
  onSaveDefaultQuoteMode?: (mode: 'Calculated' | 'Manual') => Promise<void>
  mainGridConfigJson?: string
  searchGridConfigJson?: string
  confirmGridConfigJson?: string
  onSaveGridConfig?: (key: GridConfigKey, configJson: string) => Promise<void>
  onReload: () => void | Promise<unknown>
}

export function TraderScreen(props: TraderScreenProps) {
  const {
    rfqs,
    traders,
    users,
    currentUserId,
    businessDate = new Date().toISOString().slice(0, 10),
    defaultExpiryMinutes,
    defaultQuoteMode = 'Calculated',
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
    onSaveDefaultQuoteMode,
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
  const [result, setResult] = useState<ResultState | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)
  const [targetTraderId, setTargetTraderId] = useState<string>()
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [expiryMinutes, setExpiryMinutes] = useState<number | null>(
    defaultExpiryMinutes,
  )
  const [actionError, setActionError] = useState<string | null>(null)
  const [calcStates, setCalcStates] = useState<Record<number, CalcState>>({})
  const [preferencesOpen, setPreferencesOpen] = useState(false)
  const [preferredMode, setPreferredMode] = useState(defaultQuoteMode)
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
  const activeGridRegion = useRef<HTMLDivElement>(null)
  const searchCaseInput = useRef<InputRef>(null)
  const requestSequence = useRef(0)
  const requestByCase = useRef<Record<number, number>>({})
  const inFlightCases = useRef(new Set<number>())
  const generationRef = useRef(refreshGeneration)

  const selectedRows = selectedCaseIds
    .map((id) => rfqs.find((row) => row.caseId === id))
    .filter((row): row is TraderRfq => Boolean(row))
  const selected = rfqs.find((row) => row.caseId === activeCaseId)
  const confirmableRows = selectedRows.filter((row) =>
    isConfirmable(row, currentUserId),
  )
  const pickTargets = rfqs.filter(
    (row) =>
      row.assignedTraderId === currentUserId &&
      !row.owned &&
      ['Active', 'Presented'].includes(row.rfqStatus),
  )
  const calculating = Object.values(calcStates).some(
    (state) => state.status === 'calculating',
  )
  const protectedState =
    calculating || editingCells > 0 || confirmRows !== null || pricerBusy

  useEffect(() => {
    generationRef.current = refreshGeneration
    requestSequence.current += 1
    requestByCase.current = {}
    inFlightCases.current.clear()
    setCalcStates({})
  }, [refreshGeneration])
  useEffect(
    () => onTransientStateChange?.(protectedState),
    [onTransientStateChange, protectedState],
  )
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000)
    return () => window.clearInterval(timer)
  }, [])
  useEffect(() => setPreferredMode(defaultQuoteMode), [defaultQuoteMode])
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

  const applyConfig = (api: GridApi, json: string | undefined) => {
    if (!json) return
    try {
      api.applyColumnState({ state: JSON.parse(json), applyOrder: true })
    } catch {
      api.resetColumnState()
    }
  }
  const saveLayout = async (key: GridConfigKey, api: GridApi | null) => {
    if (api && onSaveGridConfig)
      await onSaveGridConfig(key, JSON.stringify(api.getColumnState()))
  }

  const reconcileAfterSuccess = async () => {
    if (refreshMode === 'live') await onReload()
  }
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

  const editQuote = async (event: CellEditRequestEvent<TraderRfq>) => {
    const row = event.data
    const column = event.column.getColId()
    if (column === 'traderMemo') {
      const attempted = String(event.newValue ?? '')
      const response = await runSingle(
        () => onUpdateMemo(row, attempted),
        patchMemo,
        row,
      )
      if (!response)
        event.api.refreshCells({ rowNodes: [event.node], force: true })
      return
    }
    if (
      !canEditQuote(row, currentUserId) ||
      inFlightCases.current.has(row.caseId)
    ) {
      event.api.refreshCells({ rowNodes: [event.node], force: true })
      return
    }
    const value = Number(event.newValue)
    if (!Number.isFinite(value)) {
      event.api.refreshCells({ rowNodes: [event.node], force: true })
      return
    }
    const requestId = ++requestSequence.current
    requestByCase.current[row.caseId] = requestId
    inFlightCases.current.add(row.caseId)
    const generation = generationRef.current
    setCalcStates((state) => ({
      ...state,
      [row.caseId]: { status: 'calculating', requestId },
    }))
    try {
      let response: WorkingQuoteResult | void
      if (row.workingQuoteMode === 'Manual') {
        response = await withTimeout(
          onUpdateManual(
            row,
            column === 'price' ? value : (row.manual?.price ?? null),
            column === 'finalSimpleYield'
              ? value
              : (row.manual?.finalSimpleYield ?? null),
          ),
          calcTimeoutMs,
        )
      } else {
        const drivers: Record<string, CalculatedQuotePayload['driver']> = {
          price: 'Price',
          bbgYield: 'BbgYield',
          baseSimpleYield: 'SimpleYield',
          ysc: 'Ysc',
          gSpread: 'GSpread',
          asw: 'Asw',
          iSpread: 'ISpread',
          zSpread: 'ZSpread',
        }
        const driver =
          column === 'simpleYieldSlide'
            ? row.calculated?.driver
            : drivers[column]
        const driverValue =
          column === 'simpleYieldSlide'
            ? row.calculated &&
              calculatedValue(row.calculated, row.calculated.driver)
            : value
        if (!driver || driverValue == null)
          throw new Error('A calculation driver is required.')
        response = await withTimeout(
          onCalculate(
            row,
            driver,
            driverValue,
            column === 'simpleYieldSlide'
              ? value
              : (row.calculated?.simpleYieldSlide ?? 0),
          ),
          calcTimeoutMs,
        )
      }
      if (
        requestByCase.current[row.caseId] !== requestId ||
        generationRef.current !== generation
      )
        return
      if (response)
        onPatchRow(row.caseId, (current) =>
          patchWorkingQuote(current, response!),
        )
      inFlightCases.current.delete(row.caseId)
      setCalcStates((state) => {
        const next = { ...state }
        delete next[row.caseId]
        return next
      })
      await reconcileAfterSuccess()
    } catch (error) {
      if (
        requestByCase.current[row.caseId] !== requestId ||
        generationRef.current !== generation
      )
        return
      inFlightCases.current.delete(row.caseId)
      setCalcStates((state) => ({
        ...state,
        [row.caseId]: failureFrom(error, requestId),
      }))
      event.api.refreshCells({ rowNodes: [event.node], force: true })
    }
  }

  const activeColumns = useMemo<
    (ColDef<TraderRfq> | ColGroupDef<TraderRfq>)[]
  >(() => {
    const calculated = (
      id: keyof CalculatedQuotePayload,
      headerName: string,
      formatter = formatNumber,
    ): ColDef<TraderRfq> => ({
      colId: id,
      headerName,
      minWidth: 78,
      valueGetter: ({ data }) =>
        data?.workingQuoteMode === 'Calculated'
          ? data.calculated?.[id]
          : undefined,
      valueFormatter: ({ value }) => formatter(value),
      editable: ({ data }) =>
        Boolean(
          data &&
          canEditQuote(data, currentUserId) &&
          data.workingQuoteMode === 'Calculated' &&
          calcStates[data.caseId]?.status !== 'calculating',
        ),
      cellEditor: 'agNumberCellEditor',
    })
    return [
      {
        headerName: 'RFQ',
        children: [
          { field: 'caseId', headerName: 'Case', width: 78, pinned: 'left' },
          { field: 'clientName', headerName: 'Client', minWidth: 140 },
          {
            field: 'securityJapaneseName',
            headerName: 'Security',
            minWidth: 180,
            tooltipField: 'securityBbgDisplay',
          },
          {
            field: 'notional',
            headerName: 'Notl (MM)',
            width: 105,
            valueFormatter: ({ value }) =>
              value == null ? '' : formatNumber(Number(value) / million),
          },
          { field: 'settlementDate', headerName: 'Settle', width: 105 },
        ],
      },
      {
        headerName: 'Routing / State',
        children: [
          {
            colId: 'routing',
            headerName: 'Trader',
            minWidth: 125,
            valueGetter: ({ data }) =>
              data ? traderRouting(data, currentUserId) : '',
          },
          {
            colId: 'state',
            headerName: 'State',
            width: 92,
            valueGetter: ({ data }) => (data ? traderState(data) : ''),
          },
        ],
      },
      {
        headerName: 'Quote',
        children: [
          {
            field: 'workingQuoteMode',
            headerName: 'Mode',
            width: 70,
            valueFormatter: ({ value }) =>
              value === 'Manual' ? 'Man' : 'Calc',
          },
          {
            colId: 'price',
            headerName: 'Px',
            width: 78,
            valueGetter: ({ data }) =>
              data?.workingQuoteMode === 'Manual'
                ? data.manual?.price
                : data?.calculated?.price,
            valueFormatter: ({ value }) => formatNumber(value),
            editable: ({ data }) =>
              Boolean(
                data &&
                canEditQuote(data, currentUserId) &&
                calcStates[data.caseId]?.status !== 'calculating',
              ),
            cellEditor: 'agNumberCellEditor',
          },
          calculated('bbgYield', 'Yld', formatPercent),
          calculated('baseSimpleYield', 'SY', formatPercent),
          {
            colId: 'finalSimpleYield',
            headerName: 'Final SY',
            width: 92,
            valueGetter: ({ data }) =>
              data?.workingQuoteMode === 'Manual'
                ? data.manual?.finalSimpleYield
                : data?.calculated?.finalSimpleYield,
            valueFormatter: ({ value }) => formatPercent(value),
            editable: ({ data }) =>
              Boolean(
                data &&
                canEditQuote(data, currentUserId) &&
                data.workingQuoteMode === 'Manual' &&
                calcStates[data.caseId]?.status !== 'calculating',
              ),
            cellEditor: 'agNumberCellEditor',
          },
          calculated('ysc', 'YSC', formatBp),
          calculated('gSpread', 'GSpd', formatBp),
          calculated('asw', 'ASW', formatBp),
          calculated('iSpread', 'ISpd', formatBp),
          calculated('zSpread', 'ZSpd', formatBp),
          {
            colId: 'simpleYieldSlide',
            headerName: 'Slide',
            width: 82,
            valueGetter: ({ data }) =>
              data?.workingQuoteMode === 'Calculated'
                ? data.calculated?.simpleYieldSlide
                : undefined,
            valueFormatter: ({ value }) => formatPercent(value),
            editable: ({ data }) =>
              Boolean(
                data &&
                canEditQuote(data, currentUserId) &&
                data.workingQuoteMode === 'Calculated' &&
                data.calculated &&
                calcStates[data.caseId]?.status !== 'calculating',
              ),
            cellEditor: 'agNumberCellEditor',
          },
        ],
      },
      {
        headerName: 'Info',
        children: [
          {
            colId: 'elapsed',
            headerName: 'Elapsed',
            width: 82,
            valueGetter: ({ data }) =>
              data ? elapsedLabel(data.stateSince, now) : '',
          },
          {
            field: 'salesAndTradingMessage',
            headerName: 'Msg',
            minWidth: 140,
            tooltipField: 'salesAndTradingMessage',
          },
          {
            field: 'traderMemo',
            headerName: 'Trader Memo',
            minWidth: 150,
            tooltipField: 'traderMemo',
            editable: true,
            cellEditor: 'agLargeTextCellEditor',
            cellEditorParams: { rows: 4, cols: 30 },
          },
          {
            colId: 'calcStatus',
            headerName: 'Calc',
            width: 70,
            cellRenderer: ({ data }: { data?: TraderRfq }) => {
              const state = data ? calcStates[data.caseId] : undefined
              if (!state) return null
              if (state.status === 'calculating') return <Spin size="small" />
              const details = [
                state.code,
                state.message,
                state.traceId && `Trace ${state.traceId}`,
                state.failureLogId && `Log ${state.failureLogId}`,
              ]
                .filter(Boolean)
                .join(' · ')
              return (
                <Tooltip title={details}>
                  <span className="calc-failed">!</span>
                </Tooltip>
              )
            },
          },
        ],
      },
    ]
  }, [calcStates, currentUserId, now])

  const searchColumns = useMemo<ColDef<RfqSearchItem>[]>(
    () => [
      { field: 'caseId', headerName: 'Case', width: 78 },
      {
        field: 'createdAt',
        headerName: 'Date',
        width: 105,
        valueFormatter: ({ value }) => String(value ?? '').slice(0, 10),
      },
      { field: 'clientName', headerName: 'Client', minWidth: 130 },
      { field: 'securityName', headerName: 'Security', minWidth: 165 },
      {
        field: 'notional',
        headerName: 'Notl',
        width: 85,
        valueFormatter: ({ value }) =>
          value == null ? '' : formatNumber(Number(value) / million),
      },
      { field: 'contactOwnerId', headerName: 'Owner', width: 105 },
      { field: 'assignedTraderId', headerName: 'Trader', width: 105 },
      { field: 'status', headerName: 'Status', width: 90 },
      { field: 'price', headerName: 'Px', width: 75 },
      {
        field: 'finalSimpleYield',
        headerName: 'SY',
        width: 75,
        valueFormatter: ({ value }) => formatPercent(value),
      },
      {
        field: 'ysc',
        headerName: 'YSC',
        width: 75,
        valueFormatter: ({ value }) => formatBp(value),
      },
    ],
    [],
  )

  const confirmColumns = useMemo<ColDef<TraderRfq>[]>(
    () => [
      { field: 'caseId', headerName: 'Case', width: 75 },
      { field: 'clientName', headerName: 'Client', minWidth: 125 },
      { field: 'securityJapaneseName', headerName: 'Security', minWidth: 160 },
      {
        field: 'notional',
        headerName: 'Notl',
        width: 85,
        valueFormatter: ({ value }) =>
          value == null ? '' : formatNumber(Number(value) / million),
      },
      { field: 'workingQuoteMode', headerName: 'Mode', width: 75 },
      {
        colId: 'confirmPrice',
        headerName: 'Px',
        width: 75,
        valueGetter: ({ data }) =>
          data?.workingQuoteMode === 'Manual'
            ? data.manual?.price
            : data?.calculated?.price,
      },
      {
        colId: 'confirmYield',
        headerName: 'SY / Final SY',
        width: 105,
        valueGetter: ({ data }) =>
          data?.workingQuoteMode === 'Manual'
            ? data.manual?.finalSimpleYield
            : data?.calculated?.finalSimpleYield,
        valueFormatter: ({ value }) => formatPercent(value),
      },
      {
        colId: 'confirmYsc',
        headerName: 'YSC',
        width: 75,
        valueGetter: ({ data }) => data?.calculated?.ysc,
        valueFormatter: ({ value }) => formatBp(value),
      },
      {
        colId: 'confirmSlide',
        headerName: 'Slide',
        width: 75,
        valueGetter: ({ data }) => data?.calculated?.simpleYieldSlide,
        valueFormatter: ({ value }) => formatPercent(value),
      },
      {
        colId: 'expiry',
        headerName: 'Expiry',
        width: 85,
        valueGetter: () =>
          expiryMinutes === null ? 'None' : `${expiryMinutes}m`,
      },
    ],
    [expiryMinutes],
  )

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

  const manualRefresh = async () => {
    generationRef.current += 1
    requestSequence.current += 1
    requestByCase.current = {}
    inFlightCases.current.clear()
    setCalcStates({})
    await (onManualRefresh ?? onReload)()
  }

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (!event.altKey) {
        if (event.key === 'Escape') {
          setConfirmRows(null)
          setPreferencesOpen(false)
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
                    applyConfig(api, mainGridConfigJson)
                  }}
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

          <section
            className={`trader-search-section ${searchOpen ? '' : 'collapsed'}`}
            aria-label="RFQ Search"
          >
            <header>
              <Button
                type="text"
                size="small"
                onClick={() => setSearchOpen((value) => !value)}
              >
                {searchOpen ? '▾' : '▸'} RFQ Search
              </Button>
              {searchOpen && (
                <Space size={4}>
                  <Button
                    type="text"
                    size="small"
                    onClick={() => setSearchFiltersOpen((value) => !value)}
                  >
                    {searchFiltersOpen ? 'Hide filters' : 'Filters'}
                  </Button>
                  <Button
                    type="text"
                    size="small"
                    disabled={!searchGrid.current || !onSaveGridConfig}
                    onClick={() =>
                      void saveLayout('search', searchGrid.current)
                    }
                  >
                    Save
                  </Button>
                  <Button
                    type="text"
                    size="small"
                    disabled={!searchGrid.current}
                    onClick={() => searchGrid.current?.resetColumnState()}
                  >
                    Reset
                  </Button>
                </Space>
              )}
            </header>
            {searchOpen && (
              <div
                className={`trader-search-body ${searchFiltersOpen ? '' : 'filters-collapsed'}`}
              >
                {searchFiltersOpen && (
                  <div
                    className="trader-search-filters"
                    onKeyDown={(event) => {
                      if (event.key === 'Enter') void executeSearch()
                    }}
                  >
                    <Input
                      ref={searchCaseInput}
                      size="small"
                      aria-label="Search Case"
                      placeholder="Case"
                      value={searchFilters.caseId ?? ''}
                      onChange={(event) =>
                        setSearchFilters((value) => ({
                          ...value,
                          caseId: event.target.value,
                        }))
                      }
                    />
                    <Segmented
                      size="small"
                      aria-label="Search date preset"
                      value={searchPreset}
                      options={datePresets}
                      onChange={(value) =>
                        setSearchPreset(value as SearchDatePreset)
                      }
                    />
                    {[
                      'clientId',
                      'securityId',
                      'categoryId',
                      'contactOwnerId',
                      'salesId',
                      'assignedTraderId',
                    ].map((key) => (
                      <Input
                        key={key}
                        size="small"
                        aria-label={`Search ${key}`}
                        placeholder={key}
                        value={searchFilters[key] ?? ''}
                        onChange={(event) =>
                          setSearchFilters((value) => ({
                            ...value,
                            [key]: event.target.value,
                          }))
                        }
                      />
                    ))}
                    <Select
                      size="small"
                      aria-label="Search RFQ Status"
                      placeholder="RFQ Status"
                      allowClear
                      value={searchFilters.status || undefined}
                      onChange={(status) =>
                        setSearchFilters((value) => ({
                          ...value,
                          status: status ?? '',
                        }))
                      }
                      options={[
                        'Draft',
                        'Active',
                        'Presented',
                        'Cancelled',
                        'Hit',
                        'Away',
                      ].map((value) => ({ value, label: value }))}
                    />
                    <Button
                      size="small"
                      type="primary"
                      loading={searching}
                      onClick={() => void executeSearch()}
                    >
                      Search
                    </Button>
                  </div>
                )}
                <div className="trader-search-results">
                  {searchResult.requiresNarrowing && (
                    <Alert
                      banner
                      type="warning"
                      message="Result cap reached. Narrow the search."
                    />
                  )}
                  <AgGridReact<RfqSearchItem>
                    rowData={searchResult.items}
                    columnDefs={searchColumns}
                    getRowId={({ data }) => String(data.caseId)}
                    defaultColDef={{
                      sortable: true,
                      filter: true,
                      resizable: true,
                    }}
                    rowSelection={undefined}
                    rowHeight={27}
                    headerHeight={29}
                    onGridReady={({ api }) => {
                      searchGrid.current = api
                      applyConfig(api, searchGridConfigJson)
                    }}
                  />
                </div>
              </div>
            )}
          </section>
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
                    <OperationsPane
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
                      onRun={runSingle}
                      onBulk={runBulk}
                      onPickUp={onPickUp}
                      onRelease={onRelease}
                      onAssign={onAssign}
                      onTakeOver={onTakeOver}
                      onChangeMode={onChangeMode}
                      onPresent={onPresent}
                      onUnpresent={onUnpresent}
                      onWithdraw={onWithdraw}
                      onClose={onClose}
                      onCancel={onCancel}
                      onReopen={onReopen}
                      onCorrectOutcome={onCorrectOutcome}
                      onChangeContactOwner={onChangeContactOwner}
                      onPreferences={() => setPreferencesOpen(true)}
                      onSaveMain={() =>
                        void saveLayout('main', mainGrid.current)
                      }
                      onResetMain={() => mainGrid.current?.resetColumnState()}
                    />
                  ),
                },
                {
                  key: 'pricer',
                  label: 'Pricer',
                  children: (
                    <PricerPane
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
        <ResultBar
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
          <Space size={4}>
            <Button
              type="text"
              size="small"
              disabled={!confirmGrid.current || !onSaveGridConfig}
              onClick={() => void saveLayout('confirm', confirmGrid.current)}
            >
              Save columns
            </Button>
            <Button
              type="text"
              size="small"
              disabled={!confirmGrid.current}
              onClick={() => confirmGrid.current?.resetColumnState()}
            >
              Reset
            </Button>
          </Space>
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
              applyConfig(api, confirmGridConfigJson)
            }}
          />
        </div>
      </Modal>

      <Modal
        title="Trader Preferences"
        open={preferencesOpen}
        onCancel={() => setPreferencesOpen(false)}
        okText="Save"
        onOk={() =>
          void onSaveDefaultQuoteMode?.(preferredMode).then(() =>
            setPreferencesOpen(false),
          )
        }
      >
        <Typography.Text>Default Quote Mode</Typography.Text>
        <Segmented
          block
          value={preferredMode}
          options={['Calculated', 'Manual']}
          onChange={(value) =>
            setPreferredMode(value as 'Calculated' | 'Manual')
          }
        />
        <Typography.Paragraph type="secondary">
          Applies only to newly created Working Quotes.
        </Typography.Paragraph>
      </Modal>
    </div>
  )
}

function OperationsPane({
  selected,
  selectedRows,
  currentUserId,
  traders,
  users,
  isMutating,
  targetTraderId,
  setTargetTraderId,
  targetContactOwnerId,
  setTargetContactOwnerId,
  onRun,
  onBulk,
  onPickUp,
  onRelease,
  onAssign,
  onTakeOver,
  onChangeMode,
  onPresent,
  onUnpresent,
  onWithdraw,
  onClose,
  onCancel,
  onReopen,
  onCorrectOutcome,
  onChangeContactOwner,
  onPreferences,
  onSaveMain,
  onResetMain,
}: {
  selected?: TraderRfq
  selectedRows: TraderRfq[]
  currentUserId: string
  traders: UserOption[]
  users: UserOption[]
  isMutating: boolean
  targetTraderId?: string
  setTargetTraderId: (value?: string) => void
  targetContactOwnerId?: string
  setTargetContactOwnerId: (value?: string) => void
  onRun: <T>(
    action: () => Promise<T | void>,
    patch?: (row: TraderRfq, value: T) => TraderRfq,
    row?: TraderRfq,
  ) => Promise<T | void>
  onBulk: (
    label: string,
    command: TraderBulkCommand,
    rows: TraderRfq[],
    trader?: string,
  ) => Promise<void>
  onPickUp: TraderScreenProps['onPickUp']
  onRelease: TraderScreenProps['onRelease']
  onAssign: TraderScreenProps['onAssign']
  onTakeOver: TraderScreenProps['onTakeOver']
  onChangeMode: TraderScreenProps['onChangeMode']
  onPresent?: TraderScreenProps['onPresent']
  onUnpresent?: TraderScreenProps['onUnpresent']
  onWithdraw?: TraderScreenProps['onWithdraw']
  onClose: TraderScreenProps['onClose']
  onCancel?: TraderScreenProps['onCancel']
  onReopen?: TraderScreenProps['onReopen']
  onCorrectOutcome: TraderScreenProps['onCorrectOutcome']
  onChangeContactOwner: TraderScreenProps['onChangeContactOwner']
  onPreferences: () => void
  onSaveMain: () => void
  onResetMain: () => void
}) {
  if (!selected)
    return (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description="Select an Active RFQ"
      />
    )
  const open = ['Active', 'Presented'].includes(selected.rfqStatus)
  const owner = selected.contactOwnerId === currentUserId
  const mine = selected.assignedTraderId === currentUserId
  const quoted = selected.quoteStatus === 'Quoted'
  const multi = selectedRows.length > 1
  return (
    <div className="trader-operations">
      <Descriptions
        size="small"
        column={1}
        colon={false}
        items={[
          { key: 'case', label: 'Case', children: `#${selected.caseId}` },
          {
            key: 'security',
            label: 'Security',
            children: selected.securityJapaneseName,
          },
          {
            key: 'state',
            label: 'State',
            children: <Tag>{traderState(selected)}</Tag>,
          },
          {
            key: 'routing',
            label: 'Routing',
            children: traderRouting(selected, currentUserId),
          },
        ]}
      />
      <Typography.Text type="secondary">Ownership / routing</Typography.Text>
      <Space wrap>
        <Button
          size="small"
          disabled={!open || selected.owned || isMutating}
          onClick={() =>
            void onRun(
              () =>
                onPickUp(selected, selected.assignedTraderId !== currentUserId),
              patchOwnership,
              selected,
            )
          }
        >
          Pick Up
        </Button>
        <Button
          size="small"
          disabled={!open || !selected.owned || !mine || isMutating}
          onClick={() =>
            void onRun(() => onRelease(selected), patchOwnership, selected)
          }
        >
          Release
        </Button>
        <Select
          size="small"
          aria-label="Assign to trader"
          placeholder="Assign"
          value={targetTraderId}
          onChange={setTargetTraderId}
          options={traders.map((user) => ({
            value: user.userId,
            label: user.name,
          }))}
          disabled={!open || selected.owned || isMutating}
        />
        <Button
          size="small"
          disabled={!targetTraderId || !open || selected.owned || isMutating}
          onClick={() =>
            targetTraderId &&
            void onRun(
              () => onAssign(selected, targetTraderId),
              patchOwnership,
              selected,
            )
          }
        >
          Assign
        </Button>
        <Popconfirm
          title={`Take over Case ${selected.caseId}?`}
          onConfirm={() =>
            void onRun(() => onTakeOver(selected), patchOwnership, selected)
          }
        >
          <Button
            size="small"
            danger
            disabled={!open || !selected.owned || mine || isMutating}
          >
            Take Over
          </Button>
        </Popconfirm>
      </Space>
      <Typography.Text type="secondary">Quote</Typography.Text>
      <Space wrap>
        <Select
          size="small"
          aria-label="Quote mode"
          value={selected.workingQuoteMode}
          disabled={!canEditQuote(selected, currentUserId) || isMutating}
          options={['Calculated', 'Manual'].map((value) => ({
            value,
            label: value,
          }))}
          onChange={(mode) =>
            void onRun(
              () => onChangeMode(selected, mode),
              patchWorkingQuote,
              selected,
            )
          }
        />
        <Button
          size="small"
          disabled={
            !onWithdraw ||
            selected.rfqStatus === 'Presented' ||
            !quoted ||
            !mine ||
            !selected.owned ||
            isMutating
          }
          onClick={() =>
            onWithdraw &&
            void onRun(() => onWithdraw(selected), patchLifecycle, selected)
          }
        >
          Withdraw
        </Button>
      </Space>
      {owner && (
        <>
          <Typography.Text type="secondary">
            Contact Owner lifecycle
          </Typography.Text>
          <Space wrap>
            <Button
              size="small"
              disabled={
                !onPresent || selected.rfqStatus !== 'Active' || !quoted
              }
              onClick={() =>
                onPresent &&
                void onRun(
                  () => onPresent(selected),
                  patchPresentation,
                  selected,
                )
              }
            >
              Present
            </Button>
            <Button
              size="small"
              disabled={!onUnpresent || selected.rfqStatus !== 'Presented'}
              onClick={() =>
                onUnpresent &&
                void onRun(
                  () => onUnpresent(selected),
                  patchPresentation,
                  selected,
                )
              }
            >
              Unpresent
            </Button>
            {(['Hit', 'Away'] as const).map((outcome) => (
              <Popconfirm
                key={outcome}
                title={`${outcome} Case ${selected.caseId}?`}
                onConfirm={() =>
                  void onRun(
                    () => onClose(selected, outcome),
                    undefined,
                    selected,
                  )
                }
              >
                <Button size="small" disabled={!open || !quoted}>
                  {outcome}
                </Button>
              </Popconfirm>
            ))}
            <Popconfirm
              title={`Cancel Case ${selected.caseId}?`}
              onConfirm={() =>
                onCancel &&
                void onRun(() => onCancel(selected), patchLifecycle, selected)
              }
            >
              <Button size="small" danger disabled={!onCancel || !open}>
                Cancel
              </Button>
            </Popconfirm>
            <Button
              size="small"
              disabled={!onReopen || selected.rfqStatus !== 'Cancelled'}
              onClick={() =>
                onReopen &&
                void onRun(() => onReopen(selected), patchLifecycle, selected)
              }
            >
              Reopen
            </Button>
            <Popconfirm
              title="Correct closed outcome?"
              onConfirm={() =>
                void onRun(
                  () =>
                    onCorrectOutcome(
                      selected,
                      selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                    ),
                  undefined,
                  selected,
                )
              }
            >
              <Button
                size="small"
                disabled={!['Hit', 'Away'].includes(selected.rfqStatus)}
              >
                Correct
              </Button>
            </Popconfirm>
          </Space>
          <Space.Compact block>
            <Select
              size="small"
              aria-label="Contact Owner"
              placeholder="Contact Owner"
              value={targetContactOwnerId}
              onChange={setTargetContactOwnerId}
              options={users
                .filter((user) => user.userId !== selected.contactOwnerId)
                .map((user) => ({ value: user.userId, label: user.name }))}
            />
            <Button
              size="small"
              disabled={!targetContactOwnerId}
              onClick={() =>
                targetContactOwnerId &&
                void onRun(
                  () => onChangeContactOwner(selected, targetContactOwnerId),
                  patchContactOwner,
                  selected,
                )
              }
            >
              Change
            </Button>
          </Space.Compact>
        </>
      )}
      {multi && (
        <>
          <Typography.Text type="secondary">
            Selected ({selectedRows.length})
          </Typography.Text>
          <Space wrap>
            <Button
              size="small"
              onClick={() => void onBulk('Bulk Pick', 'pick', selectedRows)}
            >
              Pick
            </Button>
            <Button
              size="small"
              onClick={() =>
                void onBulk('Bulk Release', 'release', selectedRows)
              }
            >
              Release
            </Button>
            <Button
              size="small"
              disabled={!targetTraderId}
              onClick={() =>
                void onBulk(
                  'Bulk Assign',
                  'assign',
                  selectedRows,
                  targetTraderId,
                )
              }
            >
              Assign
            </Button>
            <Button
              size="small"
              onClick={() =>
                void onBulk('Bulk Withdraw', 'withdraw', selectedRows)
              }
            >
              Withdraw
            </Button>
            <Popconfirm
              title={`Away ${selectedRows.length} selected Cases?`}
              onConfirm={() => void onBulk('Bulk Away', 'away', selectedRows)}
            >
              <Button size="small">Away</Button>
            </Popconfirm>
            <Popconfirm
              title={`Cancel ${selectedRows.length} selected Cases?`}
              onConfirm={() =>
                void onBulk('Bulk Cancel', 'cancel', selectedRows)
              }
            >
              <Button size="small" danger>
                Cancel
              </Button>
            </Popconfirm>
          </Space>
        </>
      )}
      <div className="trader-pane-footer">
        <Button type="text" size="small" onClick={onPreferences}>
          Preferences
        </Button>
        <Button type="text" size="small" onClick={onSaveMain}>
          Save Grid
        </Button>
        <Button type="text" size="small" onClick={onResetMain}>
          Reset Grid
        </Button>
      </div>
    </div>
  )
}

function PricerPane({
  selected,
  scratch,
  setScratch,
  setScratchIdentity,
  result,
  provenance,
  sourceRow,
  canApply,
  busy,
  onLoad,
  onCalculate,
  onApply,
  onClear,
}: {
  selected?: TraderRfq
  scratch: ScratchState
  setScratch: React.Dispatch<React.SetStateAction<ScratchState>>
  setScratchIdentity: (
    key: 'securityId' | 'notional' | 'settlementDate',
    value: string | number | null,
  ) => void
  result: CalculatedQuotePayload | null
  provenance: PricerProvenance | null
  sourceRow?: TraderRfq
  canApply: boolean
  busy: boolean
  onLoad: () => void
  onCalculate: () => void
  onApply: () => void
  onClear: () => void
}) {
  return (
    <div className="trader-pricer">
      <Space>
        <Button size="small" disabled={!selected} onClick={onLoad}>
          Load Selected RFQ
        </Button>
        <Button size="small" onClick={onClear}>
          Clear
        </Button>
      </Space>
      <Typography.Text type={provenance ? 'success' : 'secondary'}>
        {provenance ? `Source #${provenance.sourceCaseId}` : 'Detached scratch'}
      </Typography.Text>
      {provenance && sourceRow && !sameSourceTerms(sourceRow, provenance) && (
        <Alert
          type="warning"
          showIcon
          message="Source RFQ terms changed; Apply is disabled."
        />
      )}
      <Input
        size="small"
        aria-label="Pricer Security"
        placeholder="Security"
        value={scratch.securityId}
        onChange={(event) =>
          setScratchIdentity('securityId', event.target.value)
        }
      />
      <InputNumber
        size="small"
        aria-label="Pricer Notional MM"
        placeholder="Notl (MM)"
        value={scratch.notional == null ? null : scratch.notional / million}
        onChange={(value) =>
          setScratchIdentity(
            'notional',
            value == null ? null : Number(value) * million,
          )
        }
      />
      <Input
        size="small"
        aria-label="Pricer Settlement"
        type="date"
        value={scratch.settlementDate}
        onChange={(event) =>
          setScratchIdentity('settlementDate', event.target.value)
        }
      />
      <Select
        size="small"
        aria-label="Pricer Calc Type"
        value={scratch.driver}
        options={driverOptions}
        onChange={(driver) => setScratch((value) => ({ ...value, driver }))}
      />
      <InputNumber
        size="small"
        aria-label="Pricer Parameter"
        value={scratch.value}
        onChange={(value) =>
          setScratch((state) => ({ ...state, value: Number(value ?? 0) }))
        }
      />
      <InputNumber
        size="small"
        aria-label="Pricer Slide"
        value={scratch.slide}
        onChange={(value) =>
          setScratch((state) => ({ ...state, slide: Number(value ?? 0) }))
        }
      />
      <Button
        size="small"
        type="primary"
        loading={busy}
        disabled={!scratch.securityId || !scratch.settlementDate}
        onClick={onCalculate}
      >
        Calculate
      </Button>
      {result && (
        <div className="pricer-results">
          {[
            ['Px', result.price],
            ['Yld', `${result.bbgYield}%`],
            ['SY', `${result.finalSimpleYield}%`],
            ['YSC', `${result.ysc} bp`],
            ['GSpd', `${result.gSpread} bp`],
            ['ASW', `${result.asw} bp`],
            ['ISpd', `${result.iSpread} bp`],
            ['ZSpd', `${result.zSpread} bp`],
          ].map(([label, value]) => (
            <div key={label}>
              <span>{label}</span>
              <strong>{value}</strong>
            </div>
          ))}
        </div>
      )}
      <Button size="small" disabled={!canApply} onClick={onApply}>
        {provenance ? `Apply to #${provenance.sourceCaseId}` : 'Apply'}
      </Button>
      <Typography.Paragraph type="secondary">
        Apply updates Manual Px and Final SY only. It does not Confirm.
      </Typography.Paragraph>
    </div>
  )
}

function ResultBar({
  result,
  expanded,
  onToggle,
}: {
  result: ResultState
  expanded: boolean
  onToggle: () => void
}) {
  const ok = result.items.filter((item) => item.status === 'Succeeded').length
  const skipped = result.items.filter(
    (item) => item.status === 'Skipped',
  ).length
  const failed = result.items.filter((item) => item.status === 'Failed').length
  const detailRows = result.items.filter((item) => item.status !== 'Succeeded')
  return (
    <section
      className={`bulk-result-bar ${failed ? 'bulk-result-error' : skipped ? 'bulk-result-warning' : 'bulk-result-success'}`}
    >
      {expanded && (
        <div className="bulk-result-details">
          <table>
            <thead>
              <tr>
                <th>Case</th>
                <th>Result</th>
                <th>Code</th>
                <th>Message</th>
              </tr>
            </thead>
            <tbody>
              {(detailRows.length ? detailRows : result.items).map((item) => (
                <tr key={item.caseId}>
                  <td>{item.caseId}</td>
                  <td>{item.status}</td>
                  <td>{item.code}</td>
                  <td>{item.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="bulk-result-summary">
        <span>
          {result.label}: {ok} ok / {skipped} skipped / {failed} failed
        </span>
        <Button type="text" size="small" onClick={onToggle}>
          {expanded ? 'Collapse' : 'Details'}
        </Button>
      </div>
    </section>
  )
}
