import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { Outlet, RouterProvider, createMemoryRouter } from 'react-router'
import { vi } from 'vitest'
import { AppShell } from '@/app/AppShell'
import { PostProcessWorkspace } from '@/features/post-process/PostProcessWorkspace'
import type { PostProcessItem } from '@/services/api'

const { item } = vi.hoisted(() => ({
  item: {
    caseId: 101,
    createdAt: '2026-09-22T01:00:00Z',
    createdBusinessDate: '2026-09-22',
    clientId: 'client-a',
    clientName: 'Client A',
    securityId: 'security-a',
    securityName: 'Security A',
    securityBbgDisplay: 'SEC A',
    notional: 1_000_000,
    settlementDate: '2026-09-24',
    contactOwnerId: 'sales-dev',
    salesId: 'sales-dev',
    assignedTraderId: 'trader-dev',
    rfqStatus: 'Active' as const,
    currentVersion: 3,
    salesAndTradingMessage: '',
    myMemo: '',
    myMemoVersion: 1,
    price: 99.25,
    finalSimpleYield: 1.5,
    yield: 1.4,
    ysc: 1.3,
    gSpread: 20,
    closedBusinessDate: null,
    lastCorrectionReason: null,
    lastChangedBy: 'sales-dev',
    lastChangedAt: '2026-09-22T01:00:00Z',
  },
}))

vi.mock('@/services/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/services/api')>()),
  useGetPostProcessQuery: () => ({
    data: [item],
    isLoading: false,
    isFetching: false,
    isError: false,
    refetch: vi.fn().mockResolvedValue(undefined),
  }),
  useCommitPostProcessMutation: () => [
    vi.fn(() => ({ unwrap: () => Promise.resolve([]) })),
    { isLoading: false },
  ],
}))

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({
    rowData,
    columnDefs,
  }: {
    rowData: PostProcessItem[]
    columnDefs: {
      colId?: string
      cellRenderer?: (params: { data: PostProcessItem }) => ReactNode
    }[]
  }) => (
    <div>
      {rowData.map((row) => (
        <div key={row.caseId}>
          {columnDefs
            .find((column) => column.colId === 'action')
            ?.cellRenderer?.({ data: row })}
        </div>
      ))}
    </div>
  ),
}))

function Root() {
  return (
    <AppShell health="ok" currentUserId="sales-dev">
      <Outlet context={{ currentUserId: 'sales-dev' }} />
    </AppShell>
  )
}

describe('Post Process navigation guard', () => {
  it('keeps pending changes on cancelled SPA navigation and discards them on confirm', async () => {
    const router = createMemoryRouter(
      [
        {
          path: '/',
          element: <Root />,
          children: [
            { path: 'post-process', element: <PostProcessWorkspace /> },
            { path: 'sales', element: <div>Sales Workspace</div> },
          ],
        },
      ],
      { initialEntries: ['/post-process'] },
    )
    render(<RouterProvider router={router} />)

    fireEvent.click(screen.getByRole('button', { name: 'Away' }))
    await screen.findByRole('button', { name: 'Confirm Changes (1)' })

    fireEvent.click(screen.getByRole('menuitem', { name: 'Sales' }))
    expect(
      await screen.findByText('Discard uncommitted Post Process changes?'),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Stay' }))

    expect(router.state.location.pathname).toBe('/post-process')
    expect(screen.getByRole('button', { name: 'Confirm Changes (1)' }))

    fireEvent.click(screen.getByRole('menuitem', { name: 'Sales' }))
    fireEvent.click(
      await screen.findByRole('button', { name: 'Discard and leave' }),
    )

    await waitFor(() => expect(router.state.location.pathname).toBe('/sales'))
    expect(screen.getByText('Sales Workspace')).toBeInTheDocument()
  }, 10_000)
})
