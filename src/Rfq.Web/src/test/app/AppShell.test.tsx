import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, useLocation } from 'react-router'
import { AppShell } from '@/app/AppShell'

describe('AppShell', () => {
  it('shows navigation, system date, and healthy API state', () => {
    render(
      <MemoryRouter initialEntries={['/sales']}>
        <AppShell health="ok" businessDate="2026-09-21" />
      </MemoryRouter>,
    )
    expect(screen.getAllByText('Sales')).toHaveLength(2)
    expect(screen.getByText('Trader')).toBeInTheDocument()
    expect(screen.getByText('Post Process')).toBeInTheDocument()
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
