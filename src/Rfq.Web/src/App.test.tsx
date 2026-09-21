import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import { AppShell, SalesScreen, TraderScreen, type SalesScreenProps, type TraderScreenProps } from './App'
import type { ClientSearchResult, RfqDefaults, SalesRfq, SecuritySearchResult, TraderRfq } from './services/api'

type GridRow = SalesRfq | TraderRfq

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({ rowData, onRowClicked }: {
    rowData: GridRow[]
    onRowClicked?: (event: { data: GridRow }) => void
  }) => (
    <div>
      {rowData.map((row) => (
        <button key={row.caseId} onClick={() => onRowClicked?.({ data: row })}>
          {row.clientId} {row.clientName} {row.securityId} {row.securityJapaneseName}{' '}
          {row.securityBbgDisplay} {row.rfqStatus}{' '}
          {'revisionStatus' in row ? row.revisionStatus : ''}{' '}
          {row.quoteStatus} {row.quoteRequestReason}
        </button>
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
const defaults: RfqDefaults = {
  securityId: 'sec-jgb-375',
  categoryId: 'JGB',
  categoryName: '日本国債',
  contactOwnerId: 'sales-dev',
  contactOwnerName: '開発 営業',
  assignedTraderId: 'trader-a',
  assignedTraderName: '国債 トレーダー',
  systemDate: '2026-09-21',
  standardSettlementDate: '2026-09-23',
}

const baseProps: SalesScreenProps = {
  rfqs: [],
  clients,
  securities,
  traders: [{ userId: 'trader-a', name: '国債 トレーダー' }],
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
  revisionStatus: 'Draft',
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  settlementDate: '2026-09-23',
  standardSettlementDate: '2026-09-23',
  notional: 100000000,
  salesAndTradingMessage: 'initial note',
  version: 3,
  createdAt: '2026-09-21T00:00:00Z',
}

async function selectRequiredMasters() {
  fireEvent.change(screen.getByLabelText('Client'), { target: { value: 'C001' } })
  fireEvent.click(screen.getByText('C001 — 青空銀行'))
  fireEvent.change(screen.getByLabelText('Security'), { target: { value: '375' } })
  fireEvent.click(screen.getByText(/利付国債 第375回/))
  await waitFor(() => expect(screen.getByLabelText('Settlement Date')).toHaveValue('2026-09-23'))
}

describe('AppShell', () => {
  it('shows navigation, system date, and healthy API state', () => {
    render(<AppShell health="ok" systemDate="2026-09-21" />)
    expect(screen.getAllByText('Sales')).toHaveLength(2)
    expect(screen.getByText('Trader')).toBeInTheDocument()
    expect(screen.getByText('EOD')).toBeInTheDocument()
    expect(screen.getByText('API healthy')).toBeInTheDocument()
    expect(screen.getByText('System Date: 2026-09-21')).toBeInTheDocument()
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

  it('populates defaults and saves an incomplete draft', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onCreate={onCreate} onReload={onReload} />)
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Settlement Date'), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }))

    await waitFor(() => expect(onCreate).toHaveBeenCalledWith({
      clientId: 'client-001',
      securityId: 'sec-jgb-375',
      notional: undefined,
      settlementDate: undefined,
      salesAndTradingMessage: undefined,
      assignedTraderId: 'trader-a',
    }))
    expect(onReload).toHaveBeenCalledOnce()
  })

  it('confirms a new RFQ directly without saving first', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} onConfirmNew={onConfirmNew} />)
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notional (MM)'), { target: { value: '250' } })
    fireEvent.change(screen.getByLabelText('Sales / Trading Message'), { target: { value: 'please quote' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(onConfirmNew).toHaveBeenCalledWith({
      clientId: 'client-001',
      securityId: 'sec-jgb-375',
      notional: 250000000,
      settlementDate: '2026-09-23',
      salesAndTradingMessage: 'please quote',
      assignedTraderId: 'trader-a',
    }))
  })

  it('opens a saved draft and confirms it with optimistic version', async () => {
    const onConfirmDraft = vi.fn().mockResolvedValue(undefined)
    render(<SalesScreen {...baseProps} rfqs={[draftRow]} onConfirmDraft={onConfirmDraft} />)
    fireEvent.click(screen.getByText(/client-grid 顧客表示名/))
    expect(screen.getByText('Draft Case 101')).toBeInTheDocument()
    expect(screen.getByLabelText('Notional (MM)')).toHaveValue('100.00')
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(onConfirmDraft).toHaveBeenCalledWith(101, {
      notional: 100000000,
      settlementDate: '2026-09-23',
      salesAndTradingMessage: 'initial note',
      assignedTraderId: 'trader-a',
      expectedVersion: 3,
    }))
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
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  owned: false,
  currentVersion: 4,
  settlementDate: '2026-09-23',
  notional: 100000000,
  createdAt: '2026-09-21T00:00:00Z',
}

const traderProps: TraderScreenProps = {
  rfqs: [traderRow],
  traders: [
    { userId: 'trader-a', name: 'Trader A' },
    { userId: 'trader-b', name: 'Trader B' },
  ],
  currentUserId: 'trader-a',
  isLoading: false,
  isError: false,
  isMutating: false,
  onPickUp: vi.fn().mockResolvedValue(undefined),
  onRelease: vi.fn().mockResolvedValue(undefined),
  onAssign: vi.fn().mockResolvedValue(undefined),
  onTakeOver: vi.fn().mockResolvedValue(undefined),
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
})
