import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppRoutes } from '@/app/router'

vi.mock('@/services/api', () => ({
  useGetHealthQuery: () => ({
    data: { status: 'ok' },
    isLoading: false,
    isError: false,
  }),
  useGetBusinessDateQuery: () => ({
    data: { date: '2026-09-21' },
    isLoading: false,
    isError: false,
  }),
  useGetMeQuery: () => ({ data: { userId: 'sales-dev' } }),
  useGetEventsQuery: () => ({ data: [], refetch: vi.fn() }),
}))

vi.mock('@/features/sales/SalesWorkspace', () => ({
  SalesWorkspace: () => <div>Sales Workspace</div>,
}))
vi.mock('@/features/trader/TraderWorkspace', () => ({
  TraderWorkspace: () => <div>Trader Workspace</div>,
}))
vi.mock('@/features/daily-review/DailyReviewWorkspace', () => ({
  DailyReviewWorkspace: () => <div>Daily Review Workspace</div>,
}))

describe('App routing', () => {
  beforeEach(() => window.localStorage.clear())

  it.each([
    ['/sales', 'Sales Workspace'],
    ['/trader', 'Trader Workspace'],
    ['/daily-review', 'Daily Review Workspace'],
  ])('renders %s directly', (path, expected) => {
    render(
      <MemoryRouter initialEntries={[path]}>
        <AppRoutes />
      </MemoryRouter>,
    )
    expect(screen.getByText(expected)).toBeInTheDocument()
  })

  it('redirects root to the trader workspace for a trader development identity', async () => {
    window.localStorage.setItem('rfq-development-user', 'trader-a')

    render(
      <MemoryRouter initialEntries={['/']}>
        <AppRoutes />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Trader Workspace')).toBeInTheDocument()
  })

  it('redirects root to the sales workspace for a non-trader development identity', async () => {
    window.localStorage.setItem('rfq-development-user', 'sales-dev')

    render(
      <MemoryRouter initialEntries={['/']}>
        <AppRoutes />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Sales Workspace')).toBeInTheDocument()
  })
})
