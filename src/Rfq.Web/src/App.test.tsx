import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import type { ReactNode } from 'react'
import { vi } from 'vitest'
import { MemoryRouter, useLocation } from 'react-router'
import { AppShell } from '@/app/AppShell'
import {
  SalesScreen,
  type SalesScreenProps,
} from '@/features/sales/SalesScreen'
import {
  TraderScreen,
  type TraderScreenProps,
} from '@/features/trader/TraderScreen'
import type {
  ClientSearchResult,
  RfqSearchItem,
  SalesRfq,
  SecuritySearchResult,
  TraderRfq,
} from '@/services/api'

type GridRow = SalesRfq | TraderRfq | RfqSearchItem
type GridColumn = {
  field?: string
  colId?: string
  children?: GridColumn[]
  valueGetter?: (params: { data: GridRow }) => unknown
  valueFormatter?: (params: { value: unknown }) => unknown
  cellEditor?: string
  editable?: (params: { data: GridRow }) => boolean
  cellRenderer?: (params: { data: GridRow }) => ReactNode
}

vi.mock('ag-grid-react', async () => {
  const React = await import('react')
  return {
    AgGridReact: ({
      rowData,
      columnDefs,
      onRowClicked,
      onSelectionChanged,
      onCellEditRequest,
      readOnlyEdit,
      rowSelection,
      rowClassRules,
      statusBar,
      components,
    }: {
      rowData: GridRow[]
      columnDefs?: GridColumn[]
      onRowClicked?: (event: { data: GridRow }) => void
      onSelectionChanged?: (event: {
        api: { getSelectedRows: () => GridRow[] }
      }) => void
      onCellEditRequest?: (event: {
        data: GridRow
        newValue: string
        colDef: { field: string }
        column: { getColId: () => string }
        api: { refreshCells: ReturnType<typeof vi.fn> }
        node: object
      }) => void
      readOnlyEdit?: boolean
      rowSelection?: {
        mode: string
        checkboxes?: boolean
        headerCheckbox?: boolean
        enableClickSelection?: boolean
        enableSelectionWithoutKeys?: boolean
        selectAll?: string
      }
      rowClassRules?: Record<
        string,
        (params: {
          data: GridRow
          node: { isSelected: () => boolean }
        }) => boolean
      >
      statusBar?: {
        statusPanels: {
          statusPanel: string
          statusPanelParams?: { text?: string }
        }[]
      }
      components?: Record<string, (props: { text: string }) => ReactNode>
    }) => {
      const flatColumns = (columnDefs ?? []).flatMap(
        function flatten(column): GridColumn[] {
          return column.children ? column.children.flatMap(flatten) : [column]
        },
      )
      const [selectedIds, setSelectedIds] = React.useState<number[]>([])
      const anchorIndex = React.useRef<number | undefined>(undefined)
      const select = (row: GridRow, index: number, event: React.MouseEvent) => {
        let next: number[]
        if (event.shiftKey && anchorIndex.current !== undefined) {
          const [start, end] = [anchorIndex.current, index].sort(
            (left, right) => left - right,
          )
          next = rowData.slice(start, end + 1).map((item) => item.caseId)
        } else if (event.ctrlKey || event.metaKey) {
          next = selectedIds.includes(row.caseId)
            ? selectedIds.filter((caseId) => caseId !== row.caseId)
            : [...selectedIds, row.caseId]
          anchorIndex.current = index
        } else {
          next = [row.caseId]
          anchorIndex.current = index
        }
        setSelectedIds(next)
        onRowClicked?.({ data: row })
        onSelectionChanged?.({
          api: {
            getSelectedRows: () =>
              rowData.filter((item) => next.includes(item.caseId)),
          },
        })
      }
      return (
        <div
          data-testid="grid-selection-config"
          data-checkboxes={String(rowSelection?.checkboxes)}
          data-header-checkbox={String(rowSelection?.headerCheckbox)}
          data-click-selection={String(rowSelection?.enableClickSelection)}
          data-selection-without-keys={String(
            rowSelection?.enableSelectionWithoutKeys,
          )}
          data-select-all={rowSelection?.selectAll}
        >
          {rowData.map((row, index) => {
            const selected = selectedIds.includes(row.caseId)
            const classes = Object.entries(rowClassRules ?? {})
              .filter(([, rule]) =>
                rule({ data: row, node: { isSelected: () => selected } }),
              )
              .map(([name]) => name)
              .join(' ')
            return (
              <div
                key={row.caseId}
                data-testid={`grid-row-${row.caseId}`}
                className={classes}
              >
                <button onClick={(event) => select(row, index, event)}>
                  {row.clientId} {row.clientName} {row.securityId}
                  {'securityJapaneseName' in row
                    ? row.securityJapaneseName
                    : row.securityName}{' '}
                  {'securityBbgDisplay' in row ? row.securityBbgDisplay : ''}{' '}
                  {'rfqStatus' in row ? row.rfqStatus : row.status}{' '}
                  {'revisionStatus' in row ? row.revisionStatus : ''}{' '}
                  {row.quoteStatus}{' '}
                  {'quoteRequestReason' in row ? row.quoteRequestReason : ''}
                </button>
                {readOnlyEdit && onCellEditRequest && (
                  <button
                    onClick={() =>
                      onCellEditRequest({
                        data: row,
                        newValue: '99.5',
                        colDef: { field: 'notional' },
                        column: { getColId: () => 'price' },
                        api: { refreshCells: vi.fn() },
                        node: {},
                      })
                    }
                  >
                    Edit Price {row.caseId}
                  </button>
                )}
                {readOnlyEdit &&
                  onCellEditRequest &&
                  flatColumns.some(
                    (column) => column.field === 'traderMemo',
                  ) && (
                    <button
                      onClick={() =>
                        onCellEditRequest({
                          data: row,
                          newValue: 'post-close desk note',
                          colDef: { field: 'traderMemo' },
                          column: { getColId: () => 'traderMemo' },
                          api: { refreshCells: vi.fn() },
                          node: {},
                        })
                      }
                    >
                      Edit Memo {row.caseId}
                    </button>
                  )}
                {flatColumns
                  .filter(
                    (column) =>
                      column.colId || column.field === 'currentQuoteId',
                  )
                  .map((column) => {
                    const columnKey = column.colId ?? column.field!
                    const value = column.valueGetter
                      ? column.valueGetter({ data: row })
                      : (row as unknown as Record<string, unknown>)[columnKey]
                    const display =
                      column.valueFormatter?.({ value }) ?? value ?? ''
                    return (
                      <output
                        key={columnKey}
                        data-testid={`grid-${row.caseId}-${columnKey}`}
                        data-cell-editor={column.cellEditor ?? ''}
                        data-editable={
                          column.editable?.({ data: row }) ? 'true' : 'false'
                        }
                      >
                        {column.cellRenderer
                          ? column.cellRenderer({ data: row })
                          : String(display)}
                      </output>
                    )
                  })}
              </div>
            )
          })}
          {statusBar?.statusPanels.map((panel, index) => {
            const Component = components?.[panel.statusPanel]
            return Component ? (
              <div key={index}>
                {Component({
                  text: panel.statusPanelParams?.text ?? '',
                })}
              </div>
            ) : null
          })}
        </div>
      )
    },
  }
})

const clients: ClientSearchResult[] = [
  { clientId: 'client-001', code: 'C001', name: '青空銀行' },
]
const securities: SecuritySearchResult[] = [
  {
    securityId: 'sec-jgb-375',
    japaneseName: '利付国債 第375回',
    bbgDisplay: 'JGB 0.5 03/20/2030 #375',
    internalCode: '0-02-0375-00001',
    isin: 'JP1103751P43',
    categoryId: 'JGB',
    categoryName: '日本国債',
  },
]
const defaults = {
  categoryId: 'JGB',
  categoryName: '日本国債',
  contactOwnerName: '開発 営業',
  defaultAssignedTraderId: 'trader-a',
  defaultAssignedTraderName: 'Trader A',
  assignedTraderName: '国債 トレーダー',
  standardSettlementDate: '2026-09-23',
}

const baseProps: SalesScreenProps = {
  rfqs: [],
  clients,
  securities,
  traders: [{ userId: 'trader-a', name: '国債 トレーダー' }],
  users: [
    { userId: 'sales-dev', name: '開発 営業' },
    { userId: 'sales-a', name: '営業 一郎' },
    { userId: 'trader-a', name: '国債 トレーダー' },
  ],
  currentUserId: 'sales-dev',
  isLoading: false,
  isError: false,
  isMutating: false,
  onClientSearch: vi.fn(),
  onSecuritySearch: vi.fn(),
  onResolveDefaults: vi.fn().mockResolvedValue(defaults),
  onCreate: vi.fn().mockResolvedValue(undefined),
  onUpdate: vi.fn().mockResolvedValue(undefined),
  onConfirmNew: vi.fn().mockResolvedValue(undefined),
  onConfirmDraft: vi.fn().mockResolvedValue(undefined),
  onDiscard: vi.fn().mockResolvedValue(undefined),
  onPresent: vi.fn().mockResolvedValue(undefined),
  onUnpresent: vi.fn().mockResolvedValue(undefined),
  onClose: vi.fn().mockResolvedValue(undefined),
  onBulkClose: vi.fn().mockResolvedValue([]),
  onCorrectOutcome: vi.fn().mockResolvedValue(undefined),
  onChangeContactOwner: vi.fn().mockResolvedValue(undefined),
  onUpdateMemo: vi.fn().mockResolvedValue(undefined),
  onReload: vi.fn(),
}

const draftRow: SalesRfq = {
  caseId: 101,
  clientId: 'client-grid',
  clientName: '顧客表示名',
  securityId: 'security-grid',
  securityJapaneseName: '銘柄表示名',
  securityBbgDisplay: 'SECURITY 1 09/21/2030',
  categoryId: 'JGB',
  rfqStatus: 'Draft',
  quoteStatus: null,
  quoteRequestReason: null,
  currentRevisionId: 'revision-1',
  currentQuoteId: null,
  closedQuoteId: null,
  currentVersion: 3,
  revisionStatus: 'Draft',
  salesId: 'sales-dev',
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  settlementDate: '2026-09-23',
  standardSettlementDate: '2026-09-23',
  notional: 100000000,
  salesAndTradingMessage: 'initial note',
  salesMemo: '',
  salesMemoVersion: 1,
  version: 3,
  createdAt: '2026-09-21T00:00:00Z',
  stateSince: '2026-09-21T00:00:00Z',
  confirmedQuote: null,
}

async function selectRequiredMasters() {
  fireEvent.change(screen.getByLabelText('Client'), {
    target: { value: 'C001' },
  })
  fireEvent.click(screen.getByText(/青空銀行 · C001/))
  fireEvent.change(screen.getByLabelText('Security'), {
    target: { value: '375' },
  })
  fireEvent.click(screen.getByText(/利付国債 第375回/))
  await waitFor(() =>
    expect(screen.getByLabelText('Settle')).toHaveValue('2026-09-23'),
  )
}

describe('AppShell', () => {
  it('shows navigation, system date, and healthy API state', () => {
    render(
      <MemoryRouter initialEntries={['/sales']}>
        <AppShell health="ok" businessDate="2026-09-21" />
      </MemoryRouter>,
    )
    expect(screen.getAllByText('Sales')).toHaveLength(2)
    expect(screen.getByText('Trader')).toBeInTheDocument()
    expect(screen.getByText('Daily Review')).toBeInTheDocument()
    expect(screen.getByText('API healthy')).toBeInTheDocument()
    expect(screen.getByText('Business Date: 2026-09-21')).toBeInTheDocument()
  })

  it('changes the URL through top-level navigation', () => {
    function LocationProbe() {
      return <output>{useLocation().pathname}</output>
    }

    render(
      <MemoryRouter initialEntries={['/sales']}>
        <AppShell health="ok">
          <LocationProbe />
        </AppShell>
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByText('Trader'))
    expect(screen.getByText('/trader')).toBeInTheDocument()
  })
})

describe('SalesScreen', () => {
  const quotedRow = (caseId: number): SalesRfq => ({
    ...draftRow,
    caseId,
    clientId: `client-${caseId}`,
    rfqStatus: 'Active',
    revisionStatus: 'Confirmed',
    quoteStatus: 'Quoted',
    quoteRequestReason: null,
    currentQuoteId: `00000000-0000-0000-0000-${String(caseId).padStart(12, '0')}`,
    currentVersion: 7,
  })

  it('configures Excel-like selection without visible checkboxes', () => {
    render(<SalesScreen {...baseProps} rfqs={[quotedRow(101)]} />)
    const config = screen.getByTestId('grid-selection-config')
    expect(config).toHaveAttribute('data-checkboxes', 'false')
    expect(config).toHaveAttribute('data-header-checkbox', 'false')
    expect(config).toHaveAttribute('data-click-selection', 'true')
    expect(config).toHaveAttribute('data-selection-without-keys', 'false')
    expect(config).toHaveAttribute('data-select-all', 'filtered')
  })

  it('keeps the active RFQ pane stable while Ctrl and Shift change bulk selection', () => {
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[quotedRow(101), quotedRow(102), quotedRow(103)]}
      />,
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-101')).getByRole('button', {
        name: /client-101/,
      }),
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-102')).getByRole('button', {
        name: /client-102/,
      }),
      { ctrlKey: true },
    )

    expect(screen.getByText('Case 102')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'RFQ' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('tab', { name: 'Bulk (2)' })).toBeInTheDocument()
    expect(screen.getByTestId('grid-row-101')).toHaveClass('sales-row-selected')
    expect(screen.getByTestId('grid-row-102')).toHaveClass('sales-row-selected')

    fireEvent.click(
      within(screen.getByTestId('grid-row-103')).getByRole('button', {
        name: /client-103/,
      }),
      { shiftKey: true },
    )
    expect(screen.getByText('Case 103')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'RFQ' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByTestId('grid-row-101')).not.toHaveClass(
      'sales-row-selected',
    )
    expect(screen.getByTestId('grid-row-102')).toHaveClass('sales-row-selected')
    expect(screen.getByTestId('grid-row-103')).toHaveClass('sales-row-selected')
  })

  it('keeps the Bulk tab present and shows an empty state with insufficient selection', () => {
    render(<SalesScreen {...baseProps} rfqs={[quotedRow(101)]} />)
    fireEvent.click(screen.getByRole('tab', { name: 'Bulk (0)' }))
    expect(
      screen.getByText('Select multiple RFQs for bulk operations'),
    ).toBeInTheDocument()
  })

  it('renders lifecycle and quote status returned by the API', () => {
    const confirmed = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Requested',
      quoteRequestReason: 'Initial',
    }
    render(<SalesScreen {...baseProps} rfqs={[confirmed]} />)
    expect(
      screen.getByText(/Active Confirmed Requested Initial/),
    ).toBeInTheDocument()
  })

  it('populates creation context and saves a draft with nullable notional', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen {...baseProps} onCreate={onCreate} onReload={onReload} />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }))

    await waitFor(() =>
      expect(onCreate).toHaveBeenCalledWith({
        clientId: 'client-001',
        securityId: 'sec-jgb-375',
        notional: undefined,
        settlementDate: '2026-09-23',
        standardSettlementDate: '2026-09-23',
        salesAndTradingMessage: '',
        assignedTraderId: 'trader-a',
      }),
    )
    expect(onReload).toHaveBeenCalledOnce()
  }, 15_000)

  it('confirms a new RFQ directly without saving first', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onConfirmNew={onConfirmNew} />)
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notl (MM)'), {
      target: { value: '250' },
    })
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'please quote' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() =>
      expect(onConfirmNew).toHaveBeenCalledWith({
        clientId: 'client-001',
        securityId: 'sec-jgb-375',
        notional: 250000000,
        settlementDate: '2026-09-23',
        standardSettlementDate: '2026-09-23',
        salesAndTradingMessage: 'please quote',
        assignedTraderId: 'trader-a',
      }),
    )
  }, 15_000)

  it('uses Alt+Enter to confirm the active New RFQ form', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onConfirmNew={onConfirmNew} />)
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notl (MM)'), {
      target: { value: '50' },
    })
    fireEvent.keyDown(window, { key: 'Enter', altKey: true })

    await waitFor(() =>
      expect(onConfirmNew).toHaveBeenCalledWith(
        expect.objectContaining({
          clientId: 'client-001',
          securityId: 'sec-jgb-375',
          notional: 50000000,
        }),
      ),
    )
  })

  it('opens a saved draft and confirms it with optimistic version', async () => {
    const onConfirmDraft = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[draftRow]}
        onConfirmDraft={onConfirmDraft}
      />,
    )
    fireEvent.click(screen.getByText(/client-grid 顧客表示名/))
    expect(screen.getByText('Case 101')).toBeInTheDocument()
    expect(screen.getByLabelText('Notl (MM)')).toHaveValue('100.00')
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() =>
      expect(onConfirmDraft).toHaveBeenCalledWith(101, {
        notional: 100000000,
        settlementDate: '2026-09-23',
        standardSettlementDate: '2026-09-23',
        salesAndTradingMessage: 'initial note',
        assignedTraderId: 'trader-a',
        expectedVersion: 3,
      }),
    )
  })

  it('lets the Contact Owner Present an Active quoted RFQ', async () => {
    const onPresent = vi.fn().mockResolvedValue(undefined)
    const quoted = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Quoted',
      quoteRequestReason: null,
      currentQuoteId: '00000000-0000-0000-0000-000000000301',
      currentVersion: 7,
    }
    render(<SalesScreen {...baseProps} rfqs={[quoted]} onPresent={onPresent} />)
    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Present' }))

    await waitFor(() => expect(onPresent).toHaveBeenCalledWith(101, 7))
  })

  it.each([
    ['Hit', 'Hit'],
    ['Away', 'Away'],
  ] as const)(
    'executes paused row %s directly without confirmation',
    async (label, outcome) => {
      const onClose = vi.fn().mockResolvedValue(undefined)
      render(
        <SalesScreen
          {...baseProps}
          refreshMode="paused"
          rfqs={[quotedRow(101)]}
          onClose={onClose}
        />,
      )

      fireEvent.click(screen.getByRole('button', { name: `Row 101 ${label}` }))

      await waitFor(() => expect(onClose).toHaveBeenCalledWith(101, outcome, 7))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    },
  )

  it('executes paused row Cancel directly without confirmation', async () => {
    const onCancel = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
        onCancel={onCancel}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Row 101 Cancel' }))
    await waitFor(() =>
      expect(onCancel).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 101 }),
      ),
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('keeps row actions disabled in Live and enables them in Paused', () => {
    const { rerender } = render(
      <SalesScreen {...baseProps} refreshMode="live" rfqs={[quotedRow(101)]} />,
    )
    expect(screen.getByRole('button', { name: 'Row 101 Hit' })).toBeDisabled()

    rerender(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
      />,
    )
    expect(screen.getByRole('button', { name: 'Row 101 Hit' })).toBeEnabled()
  })

  it('preserves all eight bulk operations, excludes Bulk Hit, and moves results to the Result Bar', async () => {
    const onBulk = vi.fn().mockResolvedValue([
      { caseId: 101, status: 'Succeeded', code: null, message: null },
      {
        caseId: 102,
        status: 'Failed',
        code: 'VersionConflict',
        message: 'Changed remotely',
      },
    ])
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[
          quotedRow(101),
          quotedRow(102),
          {
            ...quotedRow(103),
            quoteStatus: 'Requested',
            quoteRequestReason: 'Initial',
          },
        ]}
        onBulk={onBulk}
      />,
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-101')).getByRole('button', {
        name: /client-101/,
      }),
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-102')).getByRole('button', {
        name: /client-102/,
      }),
      { ctrlKey: true },
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-103')).getByRole('button', {
        name: /client-103/,
      }),
      { ctrlKey: true },
    )
    fireEvent.click(screen.getByRole('tab', { name: 'Bulk (3)' }))

    for (const name of [
      'Away',
      'Cancel',
      'Present',
      'Unpresent',
      'Confirm Drafts',
      'Discard Drafts',
      'Confirm Amendments',
      'Discard Amendments',
    ])
      expect(screen.getByRole('button', { name })).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Hit' }),
    ).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Away' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    expect(
      await screen.findByText('Bulk Away: 1 ok / 1 skipped / 1 failed'),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Details' }))
    expect(screen.getByText('Changed remotely')).toBeInTheDocument()
    expect(
      screen.getByText('Not eligible in current state'),
    ).toBeInTheDocument()
  }, 15_000)

  it('toggles Live/Pause, shows pending updates, and refreshes manually without changing mode', () => {
    const onRefreshModeChange = vi.fn()
    const onManualRefresh = vi.fn()
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        pendingUpdateCount={4}
        onRefreshModeChange={onRefreshModeChange}
        onManualRefresh={onManualRefresh}
      />,
    )
    expect(screen.getByText('Paused · 4 updates pending')).toBeInTheDocument()
    fireEvent.click(screen.getByText('Live'))
    expect(onRefreshModeChange).toHaveBeenCalledOnce()
    expect(onRefreshModeChange).toHaveBeenCalledWith('live')
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }))
    expect(onManualRefresh).toHaveBeenCalledOnce()
    expect(onRefreshModeChange).toHaveBeenCalledOnce()
  })

  it('keeps only Alt+L, Alt+N, Alt+Enter, and Esc workflow shortcuts', () => {
    const onRefreshModeChange = vi.fn()
    const onClose = vi.fn()
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[quotedRow(101)]}
        onRefreshModeChange={onRefreshModeChange}
        onClose={onClose}
      />,
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-101')).getByRole('button', {
        name: /client-101/,
      }),
    )

    for (const key of ['p', 'u', 'r', 'h', 'a', 'c'])
      fireEvent.keyDown(window, { key, altKey: true })
    expect(onClose).not.toHaveBeenCalled()
    fireEvent.keyDown(window, { key: 'l', altKey: true })
    expect(onRefreshModeChange).toHaveBeenCalledWith('paused')
    expect(screen.getByText(/Alt\+L Live\/Pause/)).toBeInTheDocument()
    expect(screen.queryByText(/Alt\+H Hit/)).not.toBeInTheDocument()
  })

  it('preserves Contact Owner handoff in the redesigned Work Pane', async () => {
    const onChangeContactOwner = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    const quoted = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Quoted',
      currentVersion: 7,
    }
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quoted]}
        onChangeContactOwner={onChangeContactOwner}
        onReload={onReload}
      />,
    )
    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.mouseDown(screen.getByLabelText('Contact Owner'))
    fireEvent.click(await screen.findByText('営業 一郎'))
    fireEvent.click(screen.getByRole('button', { name: 'Change' }))
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(onChangeContactOwner).toHaveBeenCalledWith(101, 'sales-a', 7),
    )
    expect(onReload).not.toHaveBeenCalled()
  })

  it('shows the current quote ID in the same abbreviated form as Trader', () => {
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Active',
            revisionStatus: 'Confirmed',
            quoteStatus: 'Quoted',
            quoteRequestReason: null,
            currentQuoteId: '12345678-1234-1234-1234-123456789abc',
          },
        ]}
      />,
    )

    expect(screen.getByTestId('grid-101-currentQuoteId')).toHaveTextContent(
      '12345678',
    )
    expect(screen.getByTestId('grid-101-currentQuoteId')).not.toHaveTextContent(
      '12345678-1234-1234-1234-123456789abc',
    )
  })

  it('lets the Contact Owner close a quoted RFQ without presentation', async () => {
    const onClose = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        onClose={onClose}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Active',
            revisionStatus: 'Confirmed',
            quoteStatus: 'Quoted',
            quoteRequestReason: null,
            currentQuoteId: '12345678-1234-1234-1234-123456789abc',
            currentVersion: 7,
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Hit' }))

    await waitFor(() => expect(onClose).toHaveBeenCalledWith(101, 'Hit', 7))
  })

  it('updates the Sales-only Memo after close', async () => {
    const onUpdateMemo = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        onUpdateMemo={onUpdateMemo}
        onReload={onReload}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Away',
            revisionStatus: 'Confirmed',
            quoteStatus: null,
            currentQuoteId: null,
            closedQuoteId: '12345678-1234-1234-1234-123456789abc',
            salesMemo: 'existing note',
            salesMemoVersion: 3,
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    fireEvent.change(screen.getByLabelText('Sales-only Memo'), {
      target: { value: 'post-close follow-up' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(onUpdateMemo).toHaveBeenCalledWith(101, 'post-close follow-up', 3),
    )
    expect(onReload).not.toHaveBeenCalled()
  })

  it('keeps the paused snapshot after correcting a closed outcome', async () => {
    const onCorrectOutcome = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[{ ...quotedRow(101), rfqStatus: 'Hit', quoteStatus: null }]}
        onCorrectOutcome={onCorrectOutcome}
        onReload={onReload}
      />,
    )

    fireEvent.click(screen.getByText(/client-101/))
    fireEvent.click(screen.getByRole('button', { name: 'Correct outcome' }))

    await waitFor(() =>
      expect(onCorrectOutcome).toHaveBeenCalledWith(101, 'Away', 7),
    )
    expect(onReload).not.toHaveBeenCalled()
  })

  it('keeps the paused snapshot after an inline Amendment edit', async () => {
    const onSaveAmendment = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
        onSaveAmendment={onSaveAmendment}
        onReload={onReload}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 101' }))

    await waitFor(() =>
      expect(onSaveAmendment).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 101 }),
        99_500_000,
        '2026-09-23',
        'initial note',
      ),
    )
    expect(onReload).not.toHaveBeenCalled()
  })

  it('opens the typed Recent Revisions drawer with changed fields only', async () => {
    render(
      <SalesScreen
        {...baseProps}
        recentRevisions={[
          {
            kind: 'Quote',
            occurredAt: '2026-09-22T01:42:00Z',
            caseId: 101,
            clientId: 'client-grid',
            clientName: 'Client Grid',
            securityId: 'security-grid',
            securityName: 'Security Grid',
            changes: [{ field: 'Price', before: '99.85', after: '99.72' }],
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Recent Revisions' }))

    expect(await screen.findByText('Case 101')).toBeInTheDocument()
    expect(screen.getByText(/99.85/)).toHaveTextContent('99.85 → 99.72')
  })
})

const traderRow: TraderRfq = {
  caseId: 201,
  clientId: 'client-001',
  clientName: 'Client One',
  securityId: 'sec-jgb-375',
  securityJapaneseName: 'JGB 375',
  securityBbgDisplay: 'JGB 0.5 03/20/2030 #375',
  categoryId: 'JGB',
  rfqStatus: 'Active',
  quoteStatus: 'Requested',
  quoteRequestReason: 'Initial',
  currentRevisionId: '00000000-0000-0000-0000-000000000201',
  currentQuoteId: null,
  closedQuoteId: null,
  confirmedAt: null,
  expiresAt: null,
  quoteSeedRevisionId: null,
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  owned: false,
  currentVersion: 4,
  settlementDate: '2026-09-23',
  notional: 100000000,
  salesAndTradingMessage: 'Please quote',
  workingQuoteMode: 'Calculated',
  calculated: null,
  manual: null,
  workingQuoteVersion: 1,
  traderMemo: '',
  traderMemoVersion: 1,
  createdAt: '2026-09-21T00:00:00Z',
  stateSince: '2026-09-21T00:00:00Z',
}

const traderProps: TraderScreenProps = {
  rfqs: [traderRow],
  traders: [
    { userId: 'trader-a', name: 'Trader A' },
    { userId: 'trader-b', name: 'Trader B' },
  ],
  users: [
    { userId: 'sales-dev', name: '開発 営業' },
    { userId: 'trader-a', name: '国債 トレーダー' },
    { userId: 'trader-b', name: '社債 トレーダー' },
  ],
  currentUserId: 'trader-a',
  defaultExpiryMinutes: 5,
  isLoading: false,
  isError: false,
  isMutating: false,
  onPickUp: vi.fn().mockResolvedValue(undefined),
  onRelease: vi.fn().mockResolvedValue(undefined),
  onAssign: vi.fn().mockResolvedValue(undefined),
  onTakeOver: vi.fn().mockResolvedValue(undefined),
  onCalculate: vi.fn().mockResolvedValue(undefined),
  onChangeMode: vi.fn().mockResolvedValue(undefined),
  onUpdateManual: vi.fn().mockResolvedValue(undefined),
  onConfirmQuote: vi.fn().mockResolvedValue(undefined),
  onClose: vi.fn().mockResolvedValue(undefined),
  onBulk: vi.fn().mockResolvedValue([]),
  onCorrectOutcome: vi.fn().mockResolvedValue(undefined),
  onChangeContactOwner: vi.fn().mockResolvedValue(undefined),
  onUpdateMemo: vi.fn().mockResolvedValue(undefined),
  onReload: vi.fn(),
}

describe('TraderScreen', () => {
  it('picks all self-assigned unowned RFQs independently of selection', async () => {
    const onBulk = vi.fn().mockResolvedValue([])
    render(<TraderScreen {...traderProps} onBulk={onBulk} />)
    fireEvent.click(screen.getByRole('button', { name: 'Pick (1)' }))

    await waitFor(() =>
      expect(onBulk).toHaveBeenCalledWith(
        'pick',
        [expect.objectContaining({ caseId: 201 })],
        5,
        undefined,
      ),
    )
    const activeGrid = screen.getAllByTestId('grid-selection-config')[0]
    expect(activeGrid).toHaveAttribute('data-checkboxes', 'false')
    expect(screen.getByTestId('grid-row-201')).toHaveClass(
      'trader-row-attention-high',
    )
  })

  it('searches with the default 1Y range and the selected 5Y preset', async () => {
    const onSearch = vi
      .fn()
      .mockResolvedValue({ items: [], requiresNarrowing: false })
    render(
      <TraderScreen
        {...traderProps}
        businessDate="2026-09-22"
        onSearch={onSearch}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() =>
      expect(onSearch).toHaveBeenLastCalledWith(
        expect.objectContaining({
          createdFrom: '2025-09-22',
          createdTo: '2026-09-22',
        }),
      ),
    )

    fireEvent.click(screen.getByText('5Y'))
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() =>
      expect(onSearch).toHaveBeenLastCalledWith(
        expect.objectContaining({
          createdFrom: '2021-09-22',
          createdTo: '2026-09-22',
        }),
      ),
    )
  }, 10_000)

  it('requires confirmation to take over an RFQ owned by another trader', async () => {
    const onTakeOver = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, assignedTraderId: 'trader-b', owned: true }]}
        onTakeOver={onTakeOver}
      />,
    )
    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Take Over' }))
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(onTakeOver).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, currentVersion: 4 }),
      ),
    )
  }, 10_000)

  it('lets the owner switch the working quote to manual mode', async () => {
    const onChangeMode = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        onChangeMode={onChangeMode}
      />,
    )
    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.mouseDown(screen.getByLabelText('Quote mode'))
    fireEvent.click((await screen.findAllByText('Manual')).at(-1)!)

    await waitFor(() =>
      expect(onChangeMode).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, workingQuoteVersion: 1 }),
        'Manual',
      ),
    )
  })

  it('sends an edited calculated Price as the calculation driver', async () => {
    const onCalculate = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        onCalculate={onCalculate}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    await waitFor(() =>
      expect(onCalculate).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, workingQuoteVersion: 1 }),
        'Price',
        99.5,
        0,
      ),
    )
  })

  it('patches an authoritative calculation response without refreshing while Paused', async () => {
    const onPatchRow = vi.fn()
    const onReload = vi.fn()
    const payload = {
      driver: 'Price' as const,
      driverValue: 99.5,
      price: 99.5,
      bbgYield: 0.8,
      baseSimpleYield: 0.81,
      simpleYieldSlide: 0.03,
      finalSimpleYield: 0.84,
      internalYield: 0.83,
      gSpread: 5,
      asw: 8,
      ysc: 6,
      iSpread: 7,
      zSpread: 6,
    }
    const onCalculate = vi.fn().mockResolvedValue({
      caseId: 201,
      revisionId: traderRow.currentRevisionId,
      mode: 'Calculated',
      calculated: payload,
      manual: null,
      version: 2,
      currentVersion: 4,
    })
    render(
      <TraderScreen
        {...traderProps}
        refreshMode="paused"
        rfqs={[{ ...traderRow, owned: true }]}
        onCalculate={onCalculate}
        onPatchRow={onPatchRow}
        onReload={onReload}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    await waitFor(() =>
      expect(onPatchRow).toHaveBeenCalledWith(201, expect.any(Function)),
    )
    expect(onReload).not.toHaveBeenCalled()
  })

  it('unlocks the row and exposes a compact calculation failure state', async () => {
    const onCalculate = vi.fn().mockRejectedValue({
      data: {
        code: 'CalculationFailure',
        calculationErrorCode: 'PRICER_DOWN',
        detail: 'Pricing is unavailable.',
        traceId: 'trace-1',
        failureLogId: 'log-1',
      },
    })
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        onCalculate={onCalculate}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    await waitFor(() =>
      expect(screen.getByTestId('grid-201-calcStatus')).toHaveTextContent('!'),
    )
    expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
      'data-editable',
      'true',
    )
  })

  it('shows yields as percent and spreads as basis points with numeric editors', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[
          {
            ...traderRow,
            owned: true,
            calculated: {
              driver: 'Price',
              driverValue: 99.5,
              price: 99.5,
              bbgYield: 0.8,
              baseSimpleYield: 0.81,
              simpleYieldSlide: 0.03,
              finalSimpleYield: 0.84,
              internalYield: 0.83,
              gSpread: 5,
              asw: 8,
              ysc: 6,
              iSpread: 7,
              zSpread: 6,
            },
          },
        ]}
      />,
    )

    expect(screen.getByTestId('grid-201-bbgYield')).toHaveTextContent('0.8%')
    expect(screen.getByTestId('grid-201-baseSimpleYield')).toHaveTextContent(
      '0.81%',
    )
    expect(screen.getByTestId('grid-201-simpleYieldSlide')).toHaveTextContent(
      '0.03%',
    )
    expect(screen.getByTestId('grid-201-finalSimpleYield')).toHaveTextContent(
      '0.84%',
    )
    expect(screen.getByTestId('grid-201-gSpread')).toHaveTextContent('5 bp')
    expect(screen.getByTestId('grid-201-bbgYield')).toHaveAttribute(
      'data-cell-editor',
      'agNumberCellEditor',
    )
    expect(screen.getByTestId('grid-201-gSpread')).toHaveAttribute(
      'data-cell-editor',
      'agNumberCellEditor',
    )
  })

  it('hides every calculated numeric result when switched to empty Manual mode', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[
          {
            ...traderRow,
            owned: true,
            workingQuoteMode: 'Manual',
            manual: { price: null, finalSimpleYield: null },
            calculated: {
              driver: 'Price',
              driverValue: 99.5,
              price: 99.5,
              bbgYield: 0.8,
              baseSimpleYield: 0.81,
              simpleYieldSlide: 0.03,
              finalSimpleYield: 0.84,
              internalYield: 0.83,
              gSpread: 5,
              asw: 8,
              ysc: 6,
              iSpread: 7,
              zSpread: 6,
            },
          },
        ]}
      />,
    )

    for (const column of [
      'price',
      'bbgYield',
      'baseSimpleYield',
      'gSpread',
      'simpleYieldSlide',
      'finalSimpleYield',
    ]) {
      expect(screen.getByTestId(`grid-201-${column}`)).toBeEmptyDOMElement()
    }
  })

  it('confirms a populated quote using the trader default expiry', async () => {
    const onConfirmQuote = vi.fn().mockResolvedValue(undefined)
    const populated = {
      ...traderRow,
      owned: true,
      calculated: {
        driver: 'Price' as const,
        driverValue: 99.5,
        price: 99.5,
        bbgYield: 0.8,
        baseSimpleYield: 0.81,
        simpleYieldSlide: 0.03,
        finalSimpleYield: 0.84,
        internalYield: 0.83,
        gSpread: 5,
        asw: 8,
        ysc: 6,
        iSpread: 7,
        zSpread: 6,
      },
    }
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[populated]}
        onConfirmQuote={onConfirmQuote}
      />,
    )
    fireEvent.click(screen.getByText(/client-001 Client One/))
    const confirmButton = screen.getByRole('button', { name: 'Confirm' })
    await waitFor(() => expect(confirmButton).toBeEnabled())
    fireEvent.click(confirmButton)
    fireEvent.click(await screen.findByRole('button', { name: 'Apply' }))

    await waitFor(() =>
      expect(onConfirmQuote).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201 }),
        5,
      ),
    )
  }, 10_000)

  it('locks quote editing after QuoteStatus becomes Quoted', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[
          {
            ...traderRow,
            owned: true,
            quoteStatus: 'Quoted',
            quoteRequestReason: null,
            currentQuoteId: '00000000-0000-0000-0000-000000000301',
          },
        ]}
      />,
    )

    expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
      'data-editable',
      'false',
    )
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled()
  })

  it('updates the Trader-only Memo after close', async () => {
    const onUpdateMemo = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        onUpdateMemo={onUpdateMemo}
        rfqs={[
          {
            ...traderRow,
            rfqStatus: 'Hit',
            quoteStatus: null,
            quoteRequestReason: null,
            currentQuoteId: null,
            closedQuoteId: '00000000-0000-0000-0000-000000000301',
            owned: false,
            traderMemo: 'existing desk note',
            traderMemoVersion: 4,
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Edit Memo 201' }))

    await waitFor(() =>
      expect(onUpdateMemo).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, traderMemoVersion: 4 }),
        'post-close desk note',
      ),
    )
  })
})
