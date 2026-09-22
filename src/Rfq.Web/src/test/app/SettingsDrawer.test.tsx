import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router'
import { AppShell } from '@/app/AppShell'

function renderShell(
  overrides: Partial<React.ComponentProps<typeof AppShell>> = {},
) {
  const props: React.ComponentProps<typeof AppShell> = {
    health: 'ok',
    isTrader: true,
    themeMode: 'Dark',
    quoteMode: 'Calculated',
    quoteExpiry: { type: 'After', minutes: 15 },
    onSaveSettings: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  }
  const view = render(
    <MemoryRouter initialEntries={['/trader']}>
      <AppShell {...props} />
    </MemoryRouter>,
  )
  return { ...view, props }
}

function segmentedOption(name: string) {
  return screen.getByRole('radio', { name }).closest('label')!
}

describe('personal Settings drawer', () => {
  it('is visible only for Traders and opens a drawer with persisted values', () => {
    const sales = renderShell({ isTrader: false })
    expect(
      screen.queryByRole('button', { name: 'Settings' }),
    ).not.toBeInTheDocument()
    sales.unmount()

    renderShell({
      themeMode: 'Light',
      quoteMode: 'Manual',
      quoteExpiry: { type: 'After', minutes: 60 },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Settings' }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(segmentedOption('Light')).toHaveClass('ant-segmented-item-selected')
    expect(segmentedOption('Manual')).toHaveClass('ant-segmented-item-selected')
    expect(
      screen.getByLabelText('Default Quote Expiry').closest('.ant-select'),
    ).toHaveTextContent('1 hour')
  })

  it('discards an unsaved draft when closed', async () => {
    renderShell()
    fireEvent.click(screen.getByRole('button', { name: 'Settings' }))
    fireEvent.click(segmentedOption('Light'))
    expect(segmentedOption('Light')).toHaveClass('ant-segmented-item-selected')

    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Settings' }))
    expect(segmentedOption('Dark')).toHaveClass('ant-segmented-item-selected')
  })

  it('persists all draft values only after Save', async () => {
    const onSaveSettings = vi.fn().mockResolvedValue(undefined)
    renderShell({ onSaveSettings })
    fireEvent.click(screen.getByRole('button', { name: 'Settings' }))
    fireEvent.click(segmentedOption('Light'))
    fireEvent.click(segmentedOption('Manual'))
    fireEvent.mouseDown(screen.getByLabelText('Default Quote Expiry'))
    fireEvent.click((await screen.findAllByText('1 hour')).at(-1)!)
    expect(onSaveSettings).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() =>
      expect(onSaveSettings).toHaveBeenCalledWith({
        theme: 'Light',
        quoteMode: 'Manual',
        quoteExpiry: { type: 'After', minutes: 60 },
      }),
    )
  })

  it('uses a Light navigation menu in Light mode', () => {
    const { container } = renderShell({ themeMode: 'Light' })
    expect(container.querySelector('.ant-menu-light')).toBeInTheDocument()
    expect(container.querySelector('.ant-menu-dark')).not.toBeInTheDocument()
  })
})
