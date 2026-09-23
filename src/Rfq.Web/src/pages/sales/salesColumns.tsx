import type {
  ColDef,
  ColGroupDef,
  ICellRendererParams,
} from 'ag-grid-community'
import type { SalesRfqResponse } from '@/generated/rfqApi'
import { RowActions } from '@/pages/sales/SalesBulkUi'
import {
  displayState,
  elapsedLabel,
  type SalesRefreshMode,
  type SalesRowCommand,
} from '@/pages/sales/salesModel'

export function hasChangedDraftMessage(
  row: SalesRfqResponse | undefined,
): boolean {
  return Boolean(
    row?.draftRevisionId &&
    row.draftSalesAndTradingMessage !== row.salesAndTradingMessage,
  )
}

const million = 1_000_000

export type SalesColumnDefinition =
  ColDef<SalesRfqResponse> | ColGroupDef<SalesRfqResponse>

export interface BuildSalesColumnsOptions {
  currentUserId: string
  isMutating: boolean
  now: number
  refreshMode: SalesRefreshMode
  onRowCommand: (command: SalesRowCommand, row: SalesRfqResponse) => void
}

export function buildSalesColumns({
  currentUserId,
  isMutating,
  now,
  refreshMode,
  onRowCommand,
}: BuildSalesColumnsOptions): SalesColumnDefinition[] {
  return [
    { field: 'caseId', headerName: 'Case', pinned: 'left', width: 78 },
    {
      colId: 'action',
      headerName: 'Action',
      pinned: 'left',
      width: 188,
      sortable: false,
      filter: false,
      suppressMovable: true,
      cellRenderer: ({ data }: ICellRendererParams<SalesRfqResponse>) =>
        data ? (
          <RowActions
            row={data}
            userId={currentUserId}
            disabled={refreshMode === 'live' || isMutating}
            onCommand={(command) => void onRowCommand(command, data)}
          />
        ) : null,
    },
    {
      groupId: 'client',
      headerName: 'Client',
      marryChildren: true,
      children: [
        {
          colId: 'client',
          headerName: 'Client',
          field: 'clientName',
          pinned: 'left',
          width: 140,
          tooltipField: 'clientName',
        },
        {
          colId: 'client-detail',
          headerName: 'Client Name',
          field: 'clientName',
          columnGroupShow: 'open',
          width: 180,
        },
        {
          colId: 'client-id',
          headerName: 'Client ID',
          field: 'clientId',
          columnGroupShow: 'open',
          width: 120,
        },
      ],
    },
    {
      groupId: 'security',
      headerName: 'Security',
      marryChildren: true,
      children: [
        {
          colId: 'security',
          headerName: 'Security',
          field: 'securityJapaneseName',
          pinned: 'left',
          width: 160,
          tooltipField: 'securityBbgDisplay',
        },
        {
          colId: 'security-ja',
          headerName: 'Japanese Name',
          field: 'securityJapaneseName',
          columnGroupShow: 'open',
          width: 190,
        },
        {
          colId: 'security-bbg',
          headerName: 'BBG Display',
          field: 'securityBbgDisplay',
          columnGroupShow: 'open',
          width: 200,
        },
        {
          colId: 'security-id',
          headerName: 'Security ID',
          field: 'securityId',
          columnGroupShow: 'open',
          width: 130,
        },
      ],
    },
    {
      groupId: 'terms',
      headerName: 'Terms',
      children: [
        {
          field: 'notional',
          headerName: 'Notl',
          width: 95,
          editable: ({ data }) =>
            Boolean(
              data &&
              data.revisionStatus !== 'Draft' &&
              ['Active', 'Presented'].includes(data.rfqStatus),
            ),
          valueGetter: ({ data }) =>
            data?.draftRevisionId ? data.draftNotional : data?.notional,
          valueFormatter: ({ value }) =>
            value == null
              ? ''
              : `${(Number(value) / million).toLocaleString()} MM`,
          cellClass: ({ data }) =>
            data?.draftRevisionId && data.draftNotional !== data.notional
              ? 'amendment-changed-cell'
              : undefined,
        },
        {
          field: 'settlementDate',
          headerName: 'Settle',
          width: 105,
          editable: ({ data }) =>
            Boolean(
              data &&
              data.revisionStatus !== 'Draft' &&
              ['Active', 'Presented'].includes(data.rfqStatus),
            ),
          valueGetter: ({ data }) =>
            data?.draftRevisionId
              ? data.draftSettlementDate
              : data?.settlementDate,
          cellClass: ({ data }) =>
            data?.draftRevisionId &&
            data.draftSettlementDate !== data.settlementDate
              ? 'amendment-changed-cell'
              : undefined,
        },
        { field: 'assignedTraderId', headerName: 'Trader', width: 105 },
      ],
    },
    {
      groupId: 'state',
      headerName: 'State',
      children: [
        {
          colId: 'state',
          headerName: 'State',
          width: 100,
          valueGetter: ({ data }) => (data ? displayState(data) : ''),
        },
        {
          field: 'rfqStatus',
          headerName: 'RFQ Status',
          columnGroupShow: 'open',
          width: 110,
        },
        {
          field: 'quoteStatus',
          headerName: 'Quote Status',
          columnGroupShow: 'open',
          width: 110,
        },
        {
          field: 'quoteRequestReason',
          headerName: 'Reason',
          columnGroupShow: 'open',
          width: 105,
        },
      ],
    },
    {
      colId: 'amend',
      headerName: 'Work',
      width: 78,
      valueGetter: ({ data }) =>
        data?.draftRevisionId
          ? 'AMEND'
          : data?.revisionStatus === 'Draft'
            ? 'DRAFT'
            : '',
    },
    {
      groupId: 'time',
      headerName: 'Time',
      children: [
        {
          colId: 'elapsed',
          headerName: 'Elapsed',
          width: 90,
          valueGetter: ({ data }) =>
            data ? elapsedLabel(data.stateSince, now) : '',
        },
        {
          field: 'createdAt',
          headerName: 'Created',
          columnGroupShow: 'open',
          width: 155,
          valueFormatter: ({ value }) =>
            value ? new Date(String(value)).toLocaleString() : '',
        },
        {
          field: 'stateSince',
          headerName: 'State Since',
          columnGroupShow: 'open',
          width: 155,
          valueFormatter: ({ value }) =>
            value ? new Date(String(value)).toLocaleString() : '',
        },
      ],
    },
    {
      groupId: 'quote',
      headerName: 'Quote',
      children: [
        {
          colId: 'price',
          headerName: 'Price',
          width: 90,
          valueGetter: ({ data }) => data?.confirmedQuote?.price ?? null,
        },
        {
          colId: 'yield',
          headerName: 'Yield',
          columnGroupShow: 'open',
          width: 85,
          valueGetter: ({ data }) => data?.confirmedQuote?.bbgYield ?? null,
          valueFormatter: ({ value }) => (value == null ? '' : `${value}%`),
        },
        {
          colId: 'simple',
          headerName: 'Simple',
          columnGroupShow: 'open',
          width: 85,
          valueGetter: ({ data }) =>
            data?.confirmedQuote?.finalSimpleYield ?? null,
          valueFormatter: ({ value }) => (value == null ? '' : `${value}%`),
        },
        {
          colId: 'g-spread',
          headerName: 'G-Spread',
          columnGroupShow: 'open',
          width: 90,
          valueGetter: ({ data }) => data?.confirmedQuote?.gSpread ?? null,
          valueFormatter: ({ value }) => (value == null ? '' : `${value} bp`),
        },
      ],
    },
    {
      field: 'salesAndTradingMessage',
      headerName: 'Message',
      width: 180,
      tooltipField: 'salesAndTradingMessage',
      editable: ({ data }) =>
        Boolean(
          data &&
          data.revisionStatus !== 'Draft' &&
          ['Active', 'Presented'].includes(data.rfqStatus),
        ),
      valueGetter: ({ data }) =>
        data?.draftSalesAndTradingMessage ?? data?.salesAndTradingMessage,
      cellClass: ({ data }) =>
        hasChangedDraftMessage(data) ? 'amendment-changed-cell' : undefined,
    },
    {
      field: 'currentQuoteId',
      headerName: 'Quote ID',
      hide: true,
      valueFormatter: ({ value }) => (value ? String(value).slice(0, 8) : ''),
    },
  ]
}
