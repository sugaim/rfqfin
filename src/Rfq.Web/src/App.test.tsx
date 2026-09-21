import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import { AppShell, SalesScreen } from './App'
import type { SalesRfq } from './services/api'

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({ rowData }: { rowData: SalesRfq[] }) => (
    <div>
      {rowData.map((row) => (
        <div key={row.caseId}>
          {row.clientId} {row.securityId} {row.rfqStatus}
        </div>
      ))}
    </div>
  ),
}))

describe('AppShell', () => {
  it('shows navigation and healthy API state', () => {
    render(<AppShell health="ok" />)

    expect(screen.getAllByText('Sales')).toHaveLength(2)
    expect(screen.getByText('Trader')).toBeInTheDocument()
    expect(screen.getByText('EOD')).toBeInTheDocument()
    expect(screen.getByText('API healthy')).toBeInTheDocument()
  })
})

describe('SalesScreen', () => {
  const rows: SalesRfq[] = [
    {
      caseId: 101,
      clientId: 'client-grid',
      securityId: 'security-grid',
      rfqStatus: 'Draft',
      currentRevisionId: 'revision-1',
      revisionStatus: 'Draft',
      createdAt: '2026-09-21T00:00:00Z',
    },
  ]

  it('renders returned drafts in the grid', () => {
    render(
      <SalesScreen
        rfqs={rows}
        isLoading={false}
        isError={false}
        isCreating={false}
        onCreate={vi.fn()}
        onReload={vi.fn()}
      />,
    )

    expect(screen.getByText(/client-grid security-grid Draft/)).toBeInTheDocument()
  })

  it('saves the form through the API callback and explicitly reloads', async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        rfqs={[]}
        isLoading={false}
        isError={false}
        isCreating={false}
        onCreate={onCreate}
        onReload={onReload}
      />,
    )

    fireEvent.change(screen.getByLabelText('Client ID'), {
      target: { value: 'client-form' },
    })
    fireEvent.change(screen.getByLabelText('Security ID'), {
      target: { value: 'security-form' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }))

    await waitFor(() => {
      expect(onCreate).toHaveBeenCalledWith({
        clientId: 'client-form',
        securityId: 'security-form',
      })
    })
    expect(onReload).toHaveBeenCalledOnce()
  })
})
