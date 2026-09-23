import '@/test/support/agGridMock'
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import { vi } from 'vitest'
import { SalesScreen, type SalesScreenProps } from '@/pages/sales/SalesScreen'
import type {
  ClientSearchResult,
  SalesRfq,
  SecuritySearchResult,
} from '@/services/api'

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
const defaults = {
  categoryId: 'JGB',
  categoryName: '日本国債',
  contactOwnerName: '開発 営業',
  defaultAssignedTraderId: 'trader-a',
  defaultAssignedTraderName: 'Trader A',
  assignedTraderName: '国債 トレーダー',
  standardSettlementDate: '2026-09-23',
}
const draftResponse = {
  caseId: 101,
  revisionId: '00000000-0000-0000-0000-000000000101',
  rfqStatus: 'Draft',
  revisionStatus: 'Draft',
  quoteStatus: null,
  quoteRequestReason: null,
  categoryId: 'JGB',
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  notional: 100_000_000,
  settlementDate: '2026-09-23',
  standardSettlementDate: '2026-09-23',
  salesAndTradingMessage: 'initial note',
  version: 3,
  createdAt: '2026-09-21T00:00:00Z',
}

const baseProps: SalesScreenProps = {
  rfqs: [],
  clients,
  securities,
  traders: [{ userId: 'trader-a', name: '国債 トレーダー' }],
  users: [
    { userId: 'sales-dev', name: '開発 営業' },
    { userId: 'sales-a', name: '営業 一郎' },
    { userId: 'trader-a', name: '国債 トレーダー' },
  ],
  currentUserId: 'sales-dev',
  isLoading: false,
  isError: false,
  isMutating: false,
  lookup: {
    searchClients: vi.fn(),
    searchSecurities: vi.fn(),
    resolveDefaults: vi.fn().mockResolvedValue(defaults),
  },
  draft: {
    create: vi.fn().mockResolvedValue(draftResponse),
    update: vi.fn().mockResolvedValue(draftResponse),
    confirmNew: vi.fn().mockResolvedValue(draftResponse),
    confirm: vi.fn().mockResolvedValue(draftResponse),
    discard: vi.fn().mockResolvedValue(undefined),
  },
  lifecycle: {
    present: vi.fn().mockResolvedValue(undefined),
    unpresent: vi.fn().mockResolvedValue(undefined),
    close: vi.fn().mockResolvedValue(undefined),
    cancel: vi.fn().mockResolvedValue(undefined),
    reopen: vi.fn().mockResolvedValue(undefined),
    createFromExisting: vi.fn().mockResolvedValue(undefined),
    correctOutcome: vi.fn().mockResolvedValue(undefined),
  },
  amendment: {
    start: vi.fn().mockResolvedValue(undefined),
    save: vi.fn().mockResolvedValue(undefined),
    confirm: vi.fn().mockResolvedValue(undefined),
    discard: vi.fn().mockResolvedValue(undefined),
  },
  contactOwner: { change: vi.fn().mockResolvedValue(undefined) },
  memo: { update: vi.fn().mockResolvedValue(undefined) },
  bulk: { execute: vi.fn().mockResolvedValue([]) },
  onReconcileCases: vi.fn().mockResolvedValue(undefined),
}

const withDraft = (
  actions: Partial<SalesScreenProps['draft']>,
): Pick<SalesScreenProps, 'draft'> => ({
  draft: { ...baseProps.draft, ...actions },
})
const withLifecycle = (
  actions: Partial<SalesScreenProps['lifecycle']>,
): Pick<SalesScreenProps, 'lifecycle'> => ({
  lifecycle: { ...baseProps.lifecycle, ...actions },
})
const withAmendment = (
  actions: Partial<SalesScreenProps['amendment']>,
): Pick<SalesScreenProps, 'amendment'> => ({
  amendment: { ...baseProps.amendment, ...actions },
})

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
  currentQuoteId: null,
  closedQuoteId: null,
  currentVersion: 3,
  revisionStatus: 'Draft',
  salesId: 'sales-dev',
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  settlementDate: '2026-09-23',
  standardSettlementDate: '2026-09-23',
  notional: 100000000,
  salesAndTradingMessage: 'initial note',
  salesMemo: '',
  salesMemoVersion: 1,
  version: 3,
  createdAt: '2026-09-21T00:00:00Z',
  stateSince: '2026-09-21T00:00:00Z',
  confirmedQuote: null,
}

async function selectRequiredMasters() {
  fireEvent.change(screen.getByLabelText('Client'), {
    target: { value: 'C001' },
  })
  fireEvent.click(screen.getByText(/青空銀行 · C001/))
  fireEvent.change(screen.getByLabelText('Security'), {
    target: { value: '375' },
  })
  fireEvent.click(screen.getByText(/利付国債 第375回/))
  await waitFor(() =>
    expect(screen.getByLabelText('Settle')).toHaveValue('2026-09-23'),
  )
}

describe('SalesScreen', () => {
  const quotedRow = (caseId: number): SalesRfq => ({
    ...draftRow,
    caseId,
    clientId: `client-${caseId}`,
    rfqStatus: 'Active',
    revisionStatus: 'Confirmed',
    quoteStatus: 'Quoted',
    quoteRequestReason: null,
    currentQuoteId: `00000000-0000-0000-0000-${String(caseId).padStart(12, '0')}`,
    currentVersion: 7,
  })

  it('configures Excel-like selection without visible checkboxes', () => {
    render(<SalesScreen {...baseProps} rfqs={[quotedRow(101)]} />)
    const config = screen.getByTestId('grid-selection-config')
    expect(config).toHaveAttribute('data-checkboxes', 'false')
    expect(config).toHaveAttribute('data-header-checkbox', 'false')
    expect(config).toHaveAttribute('data-click-selection', 'true')
    expect(config).toHaveAttribute('data-selection-without-keys', 'false')
    expect(config).toHaveAttribute('data-select-all', 'filtered')
  })

  it('keeps the active RFQ pane stable while Ctrl and Shift change bulk selection', () => {
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[quotedRow(101), quotedRow(102), quotedRow(103)]}
      />,
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-101')).getByRole('button', {
        name: /client-101/,
      }),
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-102')).getByRole('button', {
        name: /client-102/,
      }),
      { ctrlKey: true },
    )

    expect(screen.getByText('Case 102')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'RFQ' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('tab', { name: 'Bulk (2)' })).toBeInTheDocument()
    expect(screen.getByTestId('grid-row-101')).toHaveClass('sales-row-selected')
    expect(screen.getByTestId('grid-row-102')).toHaveClass('sales-row-selected')

    fireEvent.click(
      within(screen.getByTestId('grid-row-103')).getByRole('button', {
        name: /client-103/,
      }),
      { shiftKey: true },
    )
    expect(screen.getByText('Case 103')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'RFQ' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByTestId('grid-row-101')).not.toHaveClass(
      'sales-row-selected',
    )
    expect(screen.getByTestId('grid-row-102')).toHaveClass('sales-row-selected')
    expect(screen.getByTestId('grid-row-103')).toHaveClass('sales-row-selected')
  })

  it('keeps the Bulk tab present and shows an empty state with insufficient selection', () => {
    render(<SalesScreen {...baseProps} rfqs={[quotedRow(101)]} />)
    fireEvent.click(screen.getByRole('tab', { name: 'Bulk (0)' }))
    expect(
      screen.getByText('Select multiple RFQs for bulk operations'),
    ).toBeInTheDocument()
  })

  it('renders lifecycle and quote status returned by the API', () => {
    const confirmed = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Requested',
      quoteRequestReason: 'Initial',
    }
    render(<SalesScreen {...baseProps} rfqs={[confirmed]} />)
    expect(
      screen.getByText(/Active Confirmed Requested Initial/),
    ).toBeInTheDocument()
  })

  it('populates creation context and saves a draft with nullable notional', async () => {
    const onCreate = vi.fn().mockResolvedValue(draftResponse)
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        {...withDraft({ create: onCreate })}
        onReconcileCases={onReload}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }))

    await waitFor(() =>
      expect(onCreate).toHaveBeenCalledWith({
        clientId: 'client-001',
        securityId: 'sec-jgb-375',
        notional: undefined,
        settlementDate: '2026-09-23',
        standardSettlementDate: '2026-09-23',
        salesAndTradingMessage: '',
        assignedTraderId: 'trader-a',
      }),
    )
    expect(onReload).toHaveBeenCalledOnce()
  }, 15_000)

  it('confirms a new RFQ directly without saving first', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        {...withDraft({ confirmNew: onConfirmNew })}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notl (MM)'), {
      target: { value: '250' },
    })
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'please quote' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() =>
      expect(onConfirmNew).toHaveBeenCalledWith({
        clientId: 'client-001',
        securityId: 'sec-jgb-375',
        notional: 250000000,
        settlementDate: '2026-09-23',
        standardSettlementDate: '2026-09-23',
        salesAndTradingMessage: 'please quote',
        assignedTraderId: 'trader-a',
      }),
    )
  }, 15_000)

  it('does not protect Live refresh merely because unsaved New input is active', () => {
    const onTransientStateChange = vi.fn()
    render(
      <SalesScreen
        {...baseProps}
        onTransientStateChange={onTransientStateChange}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    fireEvent.focus(screen.getByLabelText('Message'))
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'local input' },
    })

    expect(onTransientStateChange).not.toHaveBeenCalledWith(true)
  })

  it('uses Alt+Enter to confirm the active New RFQ form', async () => {
    const onConfirmNew = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        {...withDraft({ confirmNew: onConfirmNew })}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'New RFQ' }))
    await selectRequiredMasters()
    fireEvent.change(screen.getByLabelText('Notl (MM)'), {
      target: { value: '50' },
    })
    fireEvent.keyDown(window, { key: 'Enter', altKey: true })

    await waitFor(() =>
      expect(onConfirmNew).toHaveBeenCalledWith(
        expect.objectContaining({
          clientId: 'client-001',
          securityId: 'sec-jgb-375',
          notional: 50000000,
        }),
      ),
    )
  })

  it('opens a saved draft and confirms it with optimistic version', async () => {
    const onConfirmDraft = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[draftRow]}
        {...withDraft({ confirm: onConfirmDraft })}
      />,
    )
    fireEvent.click(screen.getByText(/client-grid 顧客表示名/))
    expect(screen.getByText('Case 101')).toBeInTheDocument()
    expect(screen.getByLabelText('Notl (MM)')).toHaveValue('100.00')
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    await waitFor(() =>
      expect(onConfirmDraft).toHaveBeenCalledWith(101, {
        notional: 100000000,
        settlementDate: '2026-09-23',
        standardSettlementDate: '2026-09-23',
        salesAndTradingMessage: 'initial note',
        assignedTraderId: 'trader-a',
        expectedVersion: 3,
      }),
    )
  })

  it('autosaves only changed persisted Draft fields after editing completes', async () => {
    const onUpdate = vi.fn().mockResolvedValue({
      ...draftResponse,
      salesAndTradingMessage: 'changed note',
      version: 4,
    })
    const onReconcileCases = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[draftRow]}
        {...withDraft({ update: onUpdate })}
        onReconcileCases={onReconcileCases}
      />,
    )
    fireEvent.click(screen.getByText(/client-grid/))
    const message = screen.getByLabelText('Message')
    fireEvent.focus(message)
    fireEvent.blur(message)
    expect(onUpdate).not.toHaveBeenCalled()

    fireEvent.focus(message)
    fireEvent.change(message, { target: { value: 'changed note' } })
    fireEvent.blur(message)
    await waitFor(() =>
      expect(onUpdate).toHaveBeenCalledWith(
        101,
        expect.objectContaining({
          salesAndTradingMessage: 'changed note',
          expectedVersion: 3,
        }),
      ),
    )
    expect(onReconcileCases).toHaveBeenCalledWith([101])
  })

  it('lets the Contact Owner Present an Active quoted RFQ', async () => {
    const onPresent = vi.fn().mockResolvedValue(undefined)
    const quoted = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Quoted',
      quoteRequestReason: null,
      currentQuoteId: '00000000-0000-0000-0000-000000000301',
      currentVersion: 7,
    }
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[quoted]}
        {...withLifecycle({ present: onPresent })}
      />,
    )
    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Present' }))

    await waitFor(() => expect(onPresent).toHaveBeenCalledWith(101, 7))
  })

  it.each([
    ['Hit', 'Hit'],
    ['Away', 'Away'],
  ] as const)(
    'executes paused row %s directly without confirmation',
    async (label, outcome) => {
      const onClose = vi.fn().mockResolvedValue(undefined)
      render(
        <SalesScreen
          {...baseProps}
          refreshMode="paused"
          rfqs={[quotedRow(101)]}
          {...withLifecycle({ close: onClose })}
        />,
      )

      fireEvent.click(screen.getByRole('button', { name: `Row 101 ${label}` }))

      await waitFor(() => expect(onClose).toHaveBeenCalledWith(101, outcome, 7))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    },
  )

  it('executes paused row Cancel directly without confirmation', async () => {
    const onCancel = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
        {...withLifecycle({ cancel: onCancel })}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Row 101 Cancel' }))
    await waitFor(() =>
      expect(onCancel).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 101 }),
      ),
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('keeps row actions disabled in Live and enables them in Paused', () => {
    const { rerender } = render(
      <SalesScreen {...baseProps} refreshMode="live" rfqs={[quotedRow(101)]} />,
    )
    expect(screen.getByRole('button', { name: 'Row 101 Hit' })).toBeDisabled()

    rerender(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
      />,
    )
    expect(screen.getByRole('button', { name: 'Row 101 Hit' })).toBeEnabled()
  })

  it('preserves all eight bulk operations, excludes Bulk Hit, and moves results to the Result Bar', async () => {
    const onBulk = vi.fn().mockResolvedValue([
      { caseId: 101, status: 'Succeeded', code: null, message: null },
      {
        caseId: 102,
        status: 'Failed',
        code: 'VersionConflict',
        message: 'Changed remotely',
      },
    ])
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[
          quotedRow(101),
          quotedRow(102),
          {
            ...quotedRow(103),
            quoteStatus: 'Requested',
            quoteRequestReason: 'Initial',
          },
        ]}
        bulk={{ execute: onBulk }}
      />,
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-101')).getByRole('button', {
        name: /client-101/,
      }),
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-102')).getByRole('button', {
        name: /client-102/,
      }),
      { ctrlKey: true },
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-103')).getByRole('button', {
        name: /client-103/,
      }),
      { ctrlKey: true },
    )
    fireEvent.click(screen.getByRole('tab', { name: 'Bulk (3)' }))

    for (const name of [
      'Away',
      'Cancel',
      'Present',
      'Unpresent',
      'Confirm Drafts',
      'Discard Drafts',
      'Confirm Amendments',
      'Discard Amendments',
    ])
      expect(screen.getByRole('button', { name })).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Hit' }),
    ).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Away' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Apply' }))
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    )
    expect(
      await screen.findByText('Bulk Away: 1 ok / 1 skipped / 1 failed'),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Details' }))
    expect(screen.getByText('Changed remotely')).toBeInTheDocument()
    expect(
      screen.getByText('Not eligible in current state'),
    ).toBeInTheDocument()
  }, 30_000)

  it('toggles Live/Pause, shows pending updates, and refreshes manually without changing mode', () => {
    const onRefreshModeChange = vi.fn()
    const onManualRefresh = vi.fn()
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        updatesPending
        onRefreshModeChange={onRefreshModeChange}
        onManualRefresh={onManualRefresh}
      />,
    )
    expect(screen.getByText('Updates pending')).toBeInTheDocument()
    fireEvent.click(screen.getByText('Live'))
    expect(onRefreshModeChange).toHaveBeenCalledOnce()
    expect(onRefreshModeChange).toHaveBeenCalledWith('live')
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }))
    expect(onManualRefresh).toHaveBeenCalledOnce()
    expect(onRefreshModeChange).toHaveBeenCalledOnce()
  })

  it('keeps only Alt+L, Alt+N, Alt+Enter, and Esc workflow shortcuts', () => {
    const onRefreshModeChange = vi.fn()
    const onClose = vi.fn()
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[quotedRow(101)]}
        onRefreshModeChange={onRefreshModeChange}
        {...withLifecycle({ close: onClose })}
      />,
    )
    fireEvent.click(
      within(screen.getByTestId('grid-row-101')).getByRole('button', {
        name: /client-101/,
      }),
    )

    for (const key of ['p', 'u', 'r', 'h', 'a', 'c'])
      fireEvent.keyDown(window, { key, altKey: true })
    expect(onClose).not.toHaveBeenCalled()
    fireEvent.keyDown(window, { key: 'l', altKey: true })
    expect(onRefreshModeChange).toHaveBeenCalledWith('paused')
    expect(screen.getByText(/Alt\+L Live\/Pause/)).toBeInTheDocument()
    expect(screen.queryByText(/Alt\+H Hit/)).not.toBeInTheDocument()
  })

  it('preserves Contact Owner handoff in the redesigned Work Pane', async () => {
    const onChangeContactOwner = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    const quoted = {
      ...draftRow,
      rfqStatus: 'Active',
      revisionStatus: 'Confirmed',
      quoteStatus: 'Quoted',
      currentVersion: 7,
    }
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quoted]}
        contactOwner={{ change: onChangeContactOwner }}
        onReconcileCases={onReload}
      />,
    )
    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.mouseDown(screen.getByLabelText('Contact Owner'))
    fireEvent.click(await screen.findByText('営業 一郎'))
    fireEvent.click(screen.getByRole('button', { name: 'Change' }))
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(onChangeContactOwner).toHaveBeenCalledWith(101, 'sales-a', 7),
    )
    expect(onReload).toHaveBeenCalledWith([101])
  })

  it('shows the current quote ID in the same abbreviated form as Trader', () => {
    render(
      <SalesScreen
        {...baseProps}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Active',
            revisionStatus: 'Confirmed',
            quoteStatus: 'Quoted',
            quoteRequestReason: null,
            currentQuoteId: '12345678-1234-1234-1234-123456789abc',
          },
        ]}
      />,
    )

    expect(screen.getByTestId('grid-101-currentQuoteId')).toHaveTextContent(
      '12345678',
    )
    expect(screen.getByTestId('grid-101-currentQuoteId')).not.toHaveTextContent(
      '12345678-1234-1234-1234-123456789abc',
    )
  })

  it('lets the Contact Owner close a quoted RFQ without presentation', async () => {
    const onClose = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        {...withLifecycle({ close: onClose })}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Active',
            revisionStatus: 'Confirmed',
            quoteStatus: 'Quoted',
            quoteRequestReason: null,
            currentQuoteId: '12345678-1234-1234-1234-123456789abc',
            currentVersion: 7,
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Hit' }))

    await waitFor(() => expect(onClose).toHaveBeenCalledWith(101, 'Hit', 7))
  })

  it('updates the Sales-only Memo after close', async () => {
    const onUpdateMemo = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    const { rerender } = render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        memo={{ update: onUpdateMemo }}
        onReconcileCases={onReload}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Away',
            revisionStatus: 'Confirmed',
            quoteStatus: null,
            currentQuoteId: null,
            closedQuoteId: '12345678-1234-1234-1234-123456789abc',
            salesMemo: 'existing note',
            salesMemoVersion: 3,
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByText(/client-grid/))
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }))
    fireEvent.change(screen.getByLabelText('Sales-only Memo'), {
      target: { value: 'post-close follow-up' },
    })
    rerender(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        memo={{ update: onUpdateMemo }}
        onReconcileCases={onReload}
        rfqs={[
          {
            ...draftRow,
            rfqStatus: 'Away',
            revisionStatus: 'Confirmed',
            quoteStatus: null,
            currentQuoteId: null,
            closedQuoteId: '12345678-1234-1234-1234-123456789abc',
            salesMemo: 'remote note',
            salesMemoVersion: 4,
          },
        ]}
      />,
    )
    expect(screen.getByLabelText('Sales-only Memo')).toHaveValue(
      'post-close follow-up',
    )
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(onUpdateMemo).toHaveBeenCalledWith(101, 'post-close follow-up', 3),
    )
    expect(onReload).toHaveBeenCalledWith([101])
  })

  it('keeps the paused snapshot after correcting a closed outcome', async () => {
    const onCorrectOutcome = vi.fn().mockResolvedValue(undefined)
    const onReload = vi.fn().mockResolvedValue(undefined)
    const prompt = vi
      .spyOn(window, 'prompt')
      .mockReturnValue('booking correction')
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[{ ...quotedRow(101), rfqStatus: 'Hit', quoteStatus: null }]}
        {...withLifecycle({ correctOutcome: onCorrectOutcome })}
        onReconcileCases={onReload}
      />,
    )

    fireEvent.click(screen.getByText(/client-101/))
    fireEvent.click(screen.getByRole('button', { name: 'Correct outcome' }))

    await waitFor(() =>
      expect(onCorrectOutcome).toHaveBeenCalledWith(
        101,
        'Away',
        7,
        'booking correction',
      ),
    )
    expect(onReload).toHaveBeenCalledWith([101])
    prompt.mockRestore()
  })

  it('keeps the paused snapshot after an inline Amendment edit', async () => {
    const onSaveAmendment = vi.fn().mockResolvedValue({
      caseId: 101,
      currentRevisionId: draftRow.currentRevisionId,
      draftRevisionId: '00000000-0000-0000-0000-000000000102',
      currentVersion: 7,
      draftVersion: 1,
      draftNotional: 99_500_000,
      draftSettlementDate: '2026-09-23',
      draftSalesAndTradingMessage: 'initial note',
      rfqStatus: 'Active',
      quoteStatus: 'Quoted',
      quoteRequestReason: null,
    })
    const onReload = vi.fn().mockResolvedValue(undefined)
    render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
        {...withAmendment({ save: onSaveAmendment })}
        onReconcileCases={onReload}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 101' }))

    await waitFor(() =>
      expect(onSaveAmendment).toHaveBeenCalledWith(
        101,
        99_500_000,
        '2026-09-23',
        'initial note',
        7,
        null,
      ),
    )
    expect(onReload).toHaveBeenCalledWith([101])
  })

  it('starts a real zero-difference Amendment and inhibits confirmation until changed', async () => {
    const onStart = vi.fn().mockResolvedValue({
      caseId: 101,
      currentRevisionId: draftRow.currentRevisionId,
      draftRevisionId: '00000000-0000-0000-0000-000000000102',
      currentVersion: 7,
      draftVersion: 1,
      draftNotional: 100_000_000,
      draftSettlementDate: '2026-09-23',
      draftSalesAndTradingMessage: 'initial note',
      rfqStatus: 'Active',
      quoteStatus: 'Quoted',
      quoteRequestReason: null,
    })
    const onReconcileCases = vi.fn().mockResolvedValue(undefined)
    const { rerender } = render(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[quotedRow(101)]}
        {...withAmendment({ start: onStart })}
        onReconcileCases={onReconcileCases}
      />,
    )
    fireEvent.click(screen.getByText(/client-101/))
    fireEvent.click(screen.getByRole('button', { name: 'Start Amendment' }))
    await waitFor(() => expect(onStart).toHaveBeenCalledOnce())
    expect(onReconcileCases).toHaveBeenCalledWith([101])

    rerender(
      <SalesScreen
        {...baseProps}
        refreshMode="paused"
        rfqs={[
          {
            ...quotedRow(101),
            draftRevisionId: '00000000-0000-0000-0000-000000000102',
            draftVersion: 1,
            draftNotional: 100_000_000,
            draftSettlementDate: '2026-09-23',
            draftSalesAndTradingMessage: 'initial note',
          },
        ]}
      />,
    )
    expect(screen.getByText('No changes yet')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Confirm Amendment' }),
    ).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Discard' })).toBeEnabled()
  })

  it('opens the typed Recent Revisions drawer with changed fields only', async () => {
    render(
      <SalesScreen
        {...baseProps}
        recentRevisions={[
          {
            kind: 'Quote',
            occurredAt: '2026-09-22T01:42:00Z',
            caseId: 101,
            clientId: 'client-grid',
            clientName: 'Client Grid',
            securityId: 'security-grid',
            securityName: 'Security Grid',
            changes: [{ field: 'Price', before: '99.85', after: '99.72' }],
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Recent Revisions' }))

    expect(await screen.findByText('Case 101')).toBeInTheDocument()
    expect(screen.getByText(/99.85/)).toHaveTextContent('99.85 → 99.72')
  })
})
