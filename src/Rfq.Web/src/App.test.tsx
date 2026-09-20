import { render, screen } from '@testing-library/react'
import { AppShell } from './App'

describe('AppShell', () => {
  it('shows navigation placeholders and healthy API state', () => {
    render(<AppShell health="ok" />)

    expect(screen.getAllByText('Sales')).toHaveLength(2)
    expect(screen.getByText('Trader')).toBeInTheDocument()
    expect(screen.getByText('EOD')).toBeInTheDocument()
    expect(screen.getByText('API healthy')).toBeInTheDocument()
  })
})
