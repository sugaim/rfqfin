import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router'
import { App } from '@/app/App'

const settings = vi.hoisted(() => ({ theme: 'Light' as 'Light' | 'Dark' }))

vi.mock('@/generated/rfqApi', () => ({
  useGetHealthQuery: () => ({
    data: { status: 'ok' },
    isLoading: false,
    isError: false,
  }),
  useGetBusinessDateQuery: () => ({
    data: { date: '2026-09-22' },
    isLoading: false,
    isError: false,
  }),
  useGetMeQuery: () => ({
    data: {
      userId: 'trader-a',
      roles: ['Trader'],
      deskId: 'jpy-credit',
    },
  }),
  useGetThemeQuery: () => ({
    data: { mode: settings.theme },
    isLoading: false,
  }),
  useGetDefaultQuoteModeQuery: () => ({
    data: { mode: 'Calculated' },
    isLoading: false,
  }),
  useGetQuoteExpiryQuery: () => ({
    data: { type: 'After', minutes: 15 },
    isLoading: false,
  }),
  useSaveThemeMutation: () => [vi.fn(), { isLoading: false }],
  useSaveDefaultQuoteModeMutation: () => [vi.fn(), { isLoading: false }],
  useSaveQuoteExpiryMutation: () => [vi.fn(), { isLoading: false }],
}))

describe('application personal theme loading', () => {
  beforeEach(() => {
    settings.theme = 'Light'
    document.documentElement.removeAttribute('data-app-theme')
    document.documentElement.removeAttribute('data-ag-theme-mode')
  })

  it('restores the persisted theme across Ant, custom CSS and AG Grid', async () => {
    const { container } = render(
      <MemoryRouter initialEntries={['/trader']}>
        <App />
      </MemoryRouter>,
    )

    await waitFor(() =>
      expect(document.documentElement.dataset.appTheme).toBe('light'),
    )
    expect(document.documentElement.dataset.agThemeMode).toBe('light')
    expect(container.querySelector('.ant-menu-light')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Settings' })).toBeInTheDocument()
  })
})
