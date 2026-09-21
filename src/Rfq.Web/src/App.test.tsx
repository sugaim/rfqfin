import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import { AppShell, SalesScreen, type SalesScreenProps } from './App'
import type {
  ClientSearchResult,
  RfqDefaults,
  SalesRfq,
  SecuritySearchResult,
} from './services/api'

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({ rowData }: { rowData: SalesRfq[] }) => (
    <div>
      {rowData.map((row) => (
        <div key={row.caseId}>
          {row.clientId} {row.clientName} {row.securityId}{' '}
          {row.securityJapaneseName} {row.securityBbgDisplay} {row.rfqStatus}
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
  isCreating: false,
  onClientSearch: vi.fn(),
  onSecuritySearch: vi.fn(),
  onResolveDefaults: vi.fn().mockResolvedValue(defaults),
  onCreate: vi.fn().mockResolvedValue(undefined),
  onReload: vi.fn(),
}

describe('AppShell', () => {
  it('shows navigation and healthy API state', () => {
    render(<AppShell health="ok" systemDate="2026-09-21" />)

    expect(screen.getAllByText('Sales')).toHaveLength(2)
    expect(screen.getByText('Trader')).toBeInTheDocument()
    expect(screen.getByText('EOD')).toBeInTheDocument()
    expect(screen.getByText('API healthy')).toBeInTheDocument()
    expect(screen.getByText('System Date: 2026-09-21')).toBeInTheDocument()
  })
})

describe('SalesScreen', () => {
  const rows: SalesRfq[] = [
    {
      caseId: 101,
      clientId: 'client-grid',
      clientName: '顧客表示名',
      securityId: 'security-grid',
      securityJapaneseName: '銘柄表示名',
      securityBbgDisplay: 'SECURITY 1 09/21/2030',
      categoryId: 'JGB',
      rfqStatus: 'Draft',
      currentRevisionId: 'revision-1',
      revisionStatus: 'Draft',
      contactOwnerId: 'sales-dev',
      assignedTraderId: 'trader-a',
      settlementDate: '2026-09-23',
      standardSettlementDate: '2026-09-23',
      createdAt: '2026-09-21T00:00:00Z',
    },
  ]

  it('renders returned drafts in the grid', () => {
    render(<SalesScreen {...baseProps} rfqs={rows} />)

    expect(
      screen.getByText(
        /client-grid 顧客表示名 security-grid 銘柄表示名 SECURITY 1 09\/21\/2030 Draft/,
      ),
    ).toBeInTheDocument()
  })

  it('populates category, owners, and settlement when a security is selected', async () => {
    render(<SalesScreen {...baseProps} />)

    fireEvent.change(screen.getByLabelText('Security'), {
      target: { value: '375' },
    })
    fireEvent.click(screen.getByText(/利付国債 第375回/))

    await waitFor(() => {
      expect(baseProps.onResolveDefaults).toHaveBeenCalledWith(
        'sec-jgb-375',
      )
      expect(screen.getByLabelText('Category')).toHaveValue('日本国債')
      expect(screen.getByLabelText('Contact Owner')).toHaveValue('開発 営業')
      expect(screen.getByLabelText('Standard Settlement')).toHaveValue('2026-09-23')
      expect(screen.getByLabelText('Settlement Date')).toHaveValue('2026-09-23')
    })
  })

  it('saves the resolved form through the API callback and reloads', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        onCreate={onCreate}
        onReload={onReload}
      />,
    )

    fireEvent.change(screen.getByLabelText('Client'), {
      target: { value: 'C001' },
    })
    fireEvent.click(screen.getByText('C001 — 青空銀行'))
    fireEvent.change(screen.getByLabelText('Security'), {
      target: { value: '375' },
    })
    fireEvent.click(screen.getByText(/利付国債 第375回/))

    await waitFor(() => {
      expect(screen.getByLabelText('Settlement Date')).toHaveValue('2026-09-23')
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }))

    await waitFor(() => {
      expect(onCreate).toHaveBeenCalledWith({
        clientId: 'client-001',
        securityId: 'sec-jgb-375',
        settlementDate: '2026-09-23',
        assignedTraderId: 'trader-a',
      })
    })
    expect(onReload).toHaveBeenCalledOnce()
  })
})
