import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import { MemoryRouter, useLocation } from 'react-router'
import { AppShell } from './app/AppShell'
import { SalesScreen, type SalesScreenProps } from './features/sales/SalesScreen'
import { TraderScreen, type TraderScreenProps } from './features/trader/TraderScreen'
import type { ClientSearchResult, SalesRfq, SecuritySearchResult, TraderRfq } from './services/api'

type GridRow = SalesRfq | TraderRfq
type GridColumn = {
  field?: string
  colId?: string
  valueGetter?: (params: { data: GridRow }) => unknown
  valueFormatter?: (params: { value: unknown }) => unknown
  cellEditor?: string
  editable?: (params: { data: GridRow }) => boolean
}

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({ rowData, columnDefs, onRowClicked, onCellEditRequest, readOnlyEdit }: {
    rowData: GridRow[]
    columnDefs?: GridColumn[]
    onRowClicked?: (event: { data: GridRow }) => void
    onCellEditRequest?: (event: {
      data: GridRow
      newValue: string
      column: { getColId: () => string }
      api: { refreshCells: ReturnType<typeof vi.fn> }
      node: object
    }) => void
    readOnlyEdit?: boolean
  }) => (
    <div>
      {rowData.map((row) => (
        <div key={row.caseId}>
          <button onClick={() => onRowClicked?.({ data: row })}>
            {row.clientId} {row.clientName} {row.securityId} {row.securityJapaneseName}{' '}
            {row.securityBbgDisplay} {row.rfqStatus}{' '}
            {'revisionStatus' in row ? row.revisionStatus : ''}{' '}
            {row.quoteStatus} {row.quoteRequestReason}
          </button>
          {readOnlyEdit && onCellEditRequest && (
            <button
              onClick={() => onCellEditRequest({
                data: row,
                newValue: '99.5',
                column: { getColId: () => 'price' },
                api: { refreshCells: vi.fn() },
                node: {},
              })}
            >
              Edit Price {row.caseId}
            </button>
          )}
          {columnDefs?.filter((column) => column.colId || column.field === 'currentQuoteId').map((column) => {
            const columnKey = column.colId ?? column.field!
            const value = column.valueGetter
              ? column.valueGetter({ data: row })
              : (row as unknown as Record<string, unknown>)[columnKey]
            const display = column.valueFormatter?.({ value }) ?? value ?? ''
            return (
              <output
                key={columnKey}
                data-testid={`grid-${row.caseId}-${columnKey}`}
                data-cell-editor={column.cellEditor ?? ''}
                data-editable={column.editable?.({ data: row }) ? 'true' : 'false'}
              >
                {String(display)}
              </output>
            )
          })}
        </div>
      ))}
    </div>
  ),
}))

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
  fireEvent.change(screen.getByLabelText('Client'), { target: { value: 'C001' } })
  fireEvent.click(screen.getByText(/青空銀行 · C001/))
  fireEvent.change(screen.getByLabelText('Security'), { target: { value: '375' } })
  fireEvent.click(screen.getByText(/利付国債 第375回/))
  await waitFor(() => expect(screen.getByLabelText('Settle')).toHaveValue('2026-09-23'))
}

describe('AppShell', () => {
  it('shows navigation, system date, and healthy API state', () => {
    render(<MemoryRouter initialEntries={['/sales']}>
      <AppShell health="ok" businessDate="2026-09-21" />
    </MemoryRouter>)
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

    render(<MemoryRouter initialEntries={['/sales']}>
      <AppShell health="ok"><LocationProbe /></AppShell>
    </MemoryRouter>)

    fireEvent.click(screen.getByText('Trader'))
    expect(screen.getByText('/trader')).toBeInTheDocument()
  })
})

describe('SalesScreen', () => {
  it('renders lifecycle and quote status returned by the API', () => {
    const confirmed = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Requested',
      quoteRequestReason: 'Initial',
    }
    render(<SalesScreen {...baseProps} rfqs={[confirmed]} />)
    expect(screen.getByText(/Active Confirmed Requested Initial/)).toBeInTheDocument()
  })

  it('populates creation context and saves a draft with nullable notional', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onCreate={onCreate} onReload={onReload} />)
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }))

    await waitFor(() => expect(onCreate).toHaveBeenCalledWith({
      clientId: 'client-001',
      securityId: 'sec-jgb-375',
      notional: undefined,
      settlementDate: '2026-09-23',
      standardSettlementDate: '2026-09-23',
      salesAndTradingMessage: '',
      assignedTraderId: 'trader-a',
    }))
    expect(onReload).toHaveBeenCalledOnce()
  })

  it('confirms a new RFQ directly without saving first', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onConfirmNew={onConfirmNew} />)
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notl (MM)'), { target: { value: '250' } })
    fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'please quote' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(onConfirmNew).toHaveBeenCalledWith({
      clientId: 'client-001',
      securityId: 'sec-jgb-375',
      notional: 250000000,
      settlementDate: '2026-09-23',
      standardSettlementDate: '2026-09-23',
      salesAndTradingMessage: 'please quote',
      assignedTraderId: 'trader-a',
    }))
  })

  it('uses Alt+Enter to confirm the active New RFQ form', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onConfirmNew={onConfirmNew} />)
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notl (MM)'), { target: { value: '50' } })
    fireEvent.keyDown(window, { key: 'Enter', altKey: true })

    await waitFor(() => expect(onConfirmNew).toHaveBeenCalledWith(expect.objectContaining({
      clientId: 'client-001',
      securityId: 'sec-jgb-375',
      notional: 50000000,
    })))
  })

  it('opens a saved draft and confirms it with optimistic version', async () => {
    const onConfirmDraft = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} rfqs={[draftRow]} onConfirmDraft={onConfirmDraft} />)
    fireEvent.click(screen.getByText(/client-grid 顧客表示名/))
    expect(screen.getByText('Case 101')).toBeInTheDocument()
    expect(screen.getByLabelText('Notl (MM)')).toHaveValue('100.00')
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(onConfirmDraft).toHaveBeenCalledWith(101, {
      notional: 100000000,
      settlementDate: '2026-09-23',
      standardSettlementDate: '2026-09-23',
      salesAndTradingMessage: 'initial note',
      assignedTraderId: 'trader-a',
      expectedVersion: 3,
    }))
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

  it('shows the current quote ID in the same abbreviated form as Trader', () => {
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[{
          ...draftRow,
          rfqStatus: 'Active',
          revisionStatus: 'Confirmed',
          quoteStatus: 'Quoted',
          quoteRequestReason: null,
          currentQuoteId: '12345678-1234-1234-1234-123456789abc',
        }]}
      />,
    )

    expect(screen.getByTestId('grid-101-currentQuoteId')).toHaveTextContent('12345678')
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
        rfqs={[{
          ...draftRow,
          rfqStatus: 'Active',
          revisionStatus: 'Confirmed',
          quoteStatus: 'Quoted',
          quoteRequestReason: null,
          currentQuoteId: '12345678-1234-1234-1234-123456789abc',
          currentVersion: 7,
        }]}
      />,
    )

    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Hit' }))

    await waitFor(() => expect(onClose).toHaveBeenCalledWith(101, 'Hit', 7))
  })

  it('updates the Sales-only Memo after close', async () => {
    const onUpdateMemo = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        onUpdateMemo={onUpdateMemo}
        rfqs={[{
          ...draftRow,
          rfqStatus: 'Away',
          revisionStatus: 'Confirmed',
          quoteStatus: null,
          currentQuoteId: null,
          closedQuoteId: '12345678-1234-1234-1234-123456789abc',
          salesMemo: 'existing note',
          salesMemoVersion: 3,
        }]}
      />,
    )

    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    fireEvent.change(screen.getByLabelText('Sales-only Memo'), {
      target: { value: 'post-close follow-up' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onUpdateMemo).toHaveBeenCalledWith(
      101,
      'post-close follow-up',
      3,
    ))
  })

  it('opens the typed Recent Revisions drawer with changed fields only', async () => {
    render(<SalesScreen {...baseProps} recentRevisions={[{
      kind: 'Quote',
      occurredAt: '2026-09-22T01:42:00Z',
      caseId: 101,
      clientId: 'client-grid',
      clientName: 'Client Grid',
      securityId: 'security-grid',
      securityName: 'Security Grid',
      changes: [{ field: 'Price', before: '99.85', after: '99.72' }],
    }]} />)

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
  workingQuoteMode: 'Calculated',
  calculated: null,
  manual: null,
  workingQuoteVersion: 1,
  traderMemo: '',
  traderMemoVersion: 1,
  createdAt: '2026-09-21T00:00:00Z',
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
  onBulkClose: vi.fn().mockResolvedValue([]),
  onCorrectOutcome: vi.fn().mockResolvedValue(undefined),
  onChangeContactOwner: vi.fn().mockResolvedValue(undefined),
  onUpdateMemo: vi.fn().mockResolvedValue(undefined),
  onReload: vi.fn(),
}

describe('TraderScreen', () => {
  it('picks up a self-assigned unowned RFQ without confirmation', async () => {
    const onPickUp = vi.fn().mockResolvedValue(undefined)
    render(<TraderScreen {...traderProps} onPickUp={onPickUp} />)
    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Pick Up' }))

    await waitFor(() => expect(onPickUp).toHaveBeenCalledWith(201, 4, false))
  })

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

    await waitFor(() => expect(onTakeOver).toHaveBeenCalledWith(201, 4, true))
  })

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

    await waitFor(() => expect(onChangeMode).toHaveBeenCalledWith(
      expect.objectContaining({ caseId: 201, workingQuoteVersion: 1 }),
      'Manual',
    ))
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

    await waitFor(() => expect(onCalculate).toHaveBeenCalledWith(
      expect.objectContaining({ caseId: 201, workingQuoteVersion: 1 }),
      'Price',
      99.5,
      0,
    ))
  })

  it('shows yields as percent and spreads as basis points with numeric editors', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{
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
          },
        }]}
      />,
    )

    expect(screen.getByTestId('grid-201-bbgYield')).toHaveTextContent('0.8%')
    expect(screen.getByTestId('grid-201-baseSimpleYield')).toHaveTextContent('0.81%')
    expect(screen.getByTestId('grid-201-simpleYieldSlide')).toHaveTextContent('0.03%')
    expect(screen.getByTestId('grid-201-finalSimpleYield')).toHaveTextContent('0.84%')
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
        rfqs={[{
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
          },
        }]}
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
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Quote' }))
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() => expect(onConfirmQuote).toHaveBeenCalledWith(
      expect.objectContaining({ caseId: 201 }),
      5,
    ))
  })

  it('locks quote editing after QuoteStatus becomes Quoted', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{
          ...traderRow,
          owned: true,
          quoteStatus: 'Quoted',
          quoteRequestReason: null,
          currentQuoteId: '00000000-0000-0000-0000-000000000301',
        }]}
      />,
    )

    expect(screen.getByTestId('grid-201-price')).toHaveAttribute('data-editable', 'false')
    expect(screen.getByRole('button', { name: 'Confirm Quote' })).toBeDisabled()
  })

  it('updates the Trader-only Memo after close', async () => {
    const onUpdateMemo = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        onUpdateMemo={onUpdateMemo}
        rfqs={[{
          ...traderRow,
          rfqStatus: 'Hit',
          quoteStatus: null,
          quoteRequestReason: null,
          currentQuoteId: null,
          closedQuoteId: '00000000-0000-0000-0000-000000000301',
          owned: false,
          traderMemo: 'existing desk note',
          traderMemoVersion: 4,
        }]}
      />,
    )

    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.change(screen.getByLabelText('Trader-only Memo'), {
      target: { value: 'post-close desk note' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save Trader Memo' }))

    await waitFor(() => expect(onUpdateMemo).toHaveBeenCalledWith(
      201,
      'post-close desk note',
      4,
    ))
  })
})
