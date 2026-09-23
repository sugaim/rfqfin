import { useMemo } from 'react'
import { Spin, Tooltip } from 'antd'
import type { ColDef, ColGroupDef } from 'ag-grid-community'
import type {
  CalculatedQuotePayload,
  RfqSearchItem,
  TraderRfq,
} from '@/services/api'
import {
  canEditQuote,
  elapsedLabel,
  traderRouting,
  traderState,
  type CalcState,
} from '@/pages/trader/traderModel'

const million = 1_000_000

const formatNumber = (value: unknown): string =>
  value == null
    ? ''
    : Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 })
const formatPercent = (value: unknown): string =>
  value == null ? '' : `${formatNumber(value)}%`
const formatBp = (value: unknown): string =>
  value == null ? '' : `${formatNumber(value)} bp`

export type TraderColumnDefinition = ColDef<TraderRfq> | ColGroupDef<TraderRfq>

export interface UseTraderActiveColumnsOptions {
  calcStates: Record<number, CalcState>
  currentUserId: string
  now: number
}

export function useTraderActiveColumns({
  calcStates,
  currentUserId,
  now,
}: UseTraderActiveColumnsOptions): TraderColumnDefinition[] {
  return useMemo<(ColDef<TraderRfq> | ColGroupDef<TraderRfq>)[]>(() => {
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
}

export function useTraderSearchColumns(): ColDef<RfqSearchItem>[] {
  return useMemo<ColDef<RfqSearchItem>[]>(
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
}

export function useTraderConfirmColumns(
  expiryMinutes: number | null,
): ColDef<TraderRfq>[] {
  return useMemo<ColDef<TraderRfq>[]>(
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
}
