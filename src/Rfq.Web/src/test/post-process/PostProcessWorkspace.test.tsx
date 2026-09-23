import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { Outlet, RouterProvider, createMemoryRouter } from 'react-router'
import { beforeEach, vi } from 'vitest'
import { AppShell } from '@/app/AppShell'
import { PostProcessWorkspace } from '@/pages/post-process/PostProcessWorkspace'
import type { PostProcessItemResponse } from '@/generated/rfqApi'

const { item, refetchPostProcess, commitPostProcess } = vi.hoisted(() => ({
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
  refetchPostProcess: vi.fn(() => ({
    unwrap: () => Promise.resolve([]),
  })),
  commitPostProcess: vi.fn(() => ({
    unwrap: () =>
      Promise.resolve([
        {
          caseId: 101,
          status: 'Applied' as const,
          failureCode: null,
          message: null,
        },
      ]),
  })),
}))

vi.mock('@/generated/rfqApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/generated/rfqApi')>()),
  useGetPostProcessQuery: () => ({
    data: [item],
    isLoading: false,
    isFetching: false,
    isError: false,
    refetch: refetchPostProcess,
  }),
  useCommitPostProcessChangesMutation: () => [
    commitPostProcess,
    { isLoading: false },
  ],
  useGetGridConfigQuery: () => ({ data: undefined }),
  useSaveGridConfigMutation: () => [
    vi.fn(() => ({ unwrap: () => Promise.resolve(undefined) })),
    { isLoading: false },
  ],
}))

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({
    rowData,
    columnDefs,
  }: {
    rowData: PostProcessItemResponse[]
    columnDefs: {
      colId?: string
      cellRenderer?: (params: { data: PostProcessItemResponse }) => ReactNode
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
  beforeEach(() => {
    refetchPostProcess.mockClear()
    commitPostProcess.mockClear()
  })

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

  it('explicitly refetches the authoritative query after commit', async () => {
    const router = createMemoryRouter(
      [
        {
          path: '/',
          element: <Root />,
          children: [
            { path: 'post-process', element: <PostProcessWorkspace /> },
          ],
        },
      ],
      { initialEntries: ['/post-process'] },
    )
    render(<RouterProvider router={router} />)

    fireEvent.click(screen.getByRole('button', { name: 'Away' }))
    fireEvent.click(
      await screen.findByRole('button', { name: 'Confirm Changes (1)' }),
    )
    fireEvent.click(await screen.findByRole('button', { name: 'Commit' }))

    await waitFor(() => expect(commitPostProcess).toHaveBeenCalledOnce())
    expect(commitPostProcess).toHaveBeenCalledWith({
      postProcessCommitRequest: {
        items: [
          {
            caseId: 101,
            expectedCurrentVersion: 3,
            lifecycleChange: {
              type: 'Away',
              correctionReason: null,
            },
            memoChange: null,
          },
        ],
      },
    })
    await waitFor(() => expect(refetchPostProcess).toHaveBeenCalledOnce())
  })
})
