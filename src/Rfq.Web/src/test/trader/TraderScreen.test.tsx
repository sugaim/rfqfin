import '@/test/support/agGridMock'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'
import {
  TraderScreen,
  type TraderScreenProps,
} from '@/features/trader/TraderScreen'
import type { TraderRfq } from '@/services/api'

const traderRow: TraderRfq = {
  caseId: 201,
  clientId: 'client-001',
  clientName: 'Client One',
  securityId: 'sec-jgb-375',
  securityJapaneseName: 'JGB 375',
  securityBbgDisplay: 'JGB 0.5 03/20/2030 #375',
  categoryId: 'JGB',
  rfqStatus: 'Active',
  quoteStatus: 'Requested',
  quoteRequestReason: 'Initial',
  currentRevisionId: '00000000-0000-0000-0000-000000000201',
  currentQuoteId: null,
  closedQuoteId: null,
  confirmedAt: null,
  expiresAt: null,
  quoteSeedRevisionId: null,
  contactOwnerId: 'sales-dev',
  assignedTraderId: 'trader-a',
  owned: false,
  currentVersion: 4,
  settlementDate: '2026-09-23',
  notional: 100000000,
  salesAndTradingMessage: 'Please quote',
  workingQuoteMode: 'Calculated',
  calculated: null,
  manual: null,
  workingQuoteVersion: 1,
  traderMemo: '',
  traderMemoVersion: 1,
  createdAt: '2026-09-21T00:00:00Z',
  stateSince: '2026-09-21T00:00:00Z',
}

const traderProps: TraderScreenProps = {
  rfqs: [traderRow],
  traders: [
    { userId: 'trader-a', name: 'Trader A' },
    { userId: 'trader-b', name: 'Trader B' },
  ],
  users: [
    { userId: 'sales-dev', name: '開発 営業' },
    { userId: 'trader-a', name: '国債 トレーダー' },
    { userId: 'trader-b', name: '社債 トレーダー' },
  ],
  currentUserId: 'trader-a',
  defaultExpiryMinutes: 5,
  isLoading: false,
  isError: false,
  isMutating: false,
  onPickUp: vi.fn().mockResolvedValue(undefined),
  onRelease: vi.fn().mockResolvedValue(undefined),
  onAssign: vi.fn().mockResolvedValue(undefined),
  onTakeOver: vi.fn().mockResolvedValue(undefined),
  onCalculate: vi.fn().mockResolvedValue(undefined),
  onChangeMode: vi.fn().mockResolvedValue(undefined),
  onUpdateManual: vi.fn().mockResolvedValue(undefined),
  onConfirmQuote: vi.fn().mockResolvedValue(undefined),
  onClose: vi.fn().mockResolvedValue(undefined),
  onBulk: vi.fn().mockResolvedValue([]),
  onCorrectOutcome: vi.fn().mockResolvedValue(undefined),
  onChangeContactOwner: vi.fn().mockResolvedValue(undefined),
  onUpdateMemo: vi.fn().mockResolvedValue(undefined),
  onReload: vi.fn(),
}

describe('TraderScreen', () => {
  it('picks all self-assigned unowned RFQs independently of selection', async () => {
    const onBulk = vi.fn().mockResolvedValue([])
    render(<TraderScreen {...traderProps} onBulk={onBulk} />)
    fireEvent.click(screen.getByRole('button', { name: 'Pick (1)' }))

    await waitFor(() =>
      expect(onBulk).toHaveBeenCalledWith(
        'pick',
        [expect.objectContaining({ caseId: 201 })],
        5,
        undefined,
      ),
    )
    const activeGrid = screen.getAllByTestId('grid-selection-config')[0]
    expect(activeGrid).toHaveAttribute('data-checkboxes', 'false')
    expect(screen.getByTestId('grid-row-201')).toHaveClass(
      'trader-row-attention-high',
    )
  })

  it('searches with the default 1Y range and the selected 5Y preset', async () => {
    const onSearch = vi
      .fn()
      .mockResolvedValue({ items: [], requiresNarrowing: false })
    render(
      <TraderScreen
        {...traderProps}
        businessDate="2026-09-22"
        onSearch={onSearch}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() =>
      expect(onSearch).toHaveBeenLastCalledWith(
        expect.objectContaining({
          createdFrom: '2025-09-22',
          createdTo: '2026-09-22',
        }),
      ),
    )

    fireEvent.click(screen.getByText('5Y'))
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() =>
      expect(onSearch).toHaveBeenLastCalledWith(
        expect.objectContaining({
          createdFrom: '2021-09-22',
          createdTo: '2026-09-22',
        }),
      ),
    )
  }, 10_000)

  it('requires confirmation to take over an RFQ owned by another trader', async () => {
    const onTakeOver = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, assignedTraderId: 'trader-b', owned: true }]}
        onTakeOver={onTakeOver}
      />,
    )
    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Take Over' }))
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(onTakeOver).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, currentVersion: 4 }),
      ),
    )
  }, 10_000)

  it('picks up a self-assigned unowned RFQ without confirmation', async () => {
    const onPickUp = vi.fn().mockResolvedValue(undefined)
    render(<TraderScreen {...traderProps} onPickUp={onPickUp} />)

    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Pick Up' }))

    await waitFor(() =>
      expect(onPickUp).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201 }),
        false,
      ),
    )
    expect(
      screen.queryByText(/assigned to another trader/),
    ).not.toBeInTheDocument()
  })

  it('confirms before picking up another trader assigned unowned RFQ', async () => {
    const onPickUp = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, assignedTraderId: 'trader-b' }]}
        onPickUp={onPickUp}
      />,
    )

    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Pick Up' }))

    expect(onPickUp).not.toHaveBeenCalled()
    expect(
      await screen.findByText(/assigned to another trader/),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(onPickUp).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201 }),
        true,
      ),
    )
  }, 10_000)

  it('keeps Pick Up unavailable for another trader owned RFQ', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, assignedTraderId: 'trader-b', owned: true }]}
      />,
    )

    fireEvent.click(screen.getByText(/client-001 Client One/))

    expect(screen.getByRole('button', { name: 'Pick Up' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Take Over' })).toBeEnabled()
  })

  it('confirms a mixed bulk Pick and submits only eligible unowned rows', async () => {
    const onBulk = vi.fn().mockResolvedValue([])
    const otherAssigned = {
      ...traderRow,
      caseId: 202,
      clientId: 'client-002',
      clientName: 'Client Two',
      assignedTraderId: 'trader-b',
    }
    const otherOwned = {
      ...traderRow,
      caseId: 203,
      clientId: 'client-003',
      clientName: 'Client Three',
      assignedTraderId: 'trader-b',
      owned: true,
    }
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[traderRow, otherAssigned, otherOwned]}
        onBulk={onBulk}
      />,
    )

    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByText(/client-002 Client Two/), {
      ctrlKey: true,
    })
    fireEvent.click(screen.getByText(/client-003 Client Three/), {
      ctrlKey: true,
    })
    fireEvent.click(screen.getByRole('button', { name: /^Pick$/ }))

    expect(onBulk).not.toHaveBeenCalled()
    expect(
      await screen.findByText(/including 1 assigned to another trader/),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))

    await waitFor(() =>
      expect(onBulk).toHaveBeenCalledWith(
        'pick',
        [
          expect.objectContaining({ caseId: 201 }),
          expect.objectContaining({ caseId: 202 }),
        ],
        5,
        undefined,
      ),
    )
  }, 10_000)

  it('lets the owner switch the working quote to manual mode', async () => {
    const onChangeMode = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        onChangeMode={onChangeMode}
      />,
    )
    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.mouseDown(screen.getByLabelText('Quote mode'))
    fireEvent.click((await screen.findAllByText('Manual')).at(-1)!)

    await waitFor(() =>
      expect(onChangeMode).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, workingQuoteVersion: 1 }),
        'Manual',
      ),
    )
  })

  it('sends an edited calculated Price as the calculation driver', async () => {
    const onCalculate = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        onCalculate={onCalculate}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    await waitFor(() =>
      expect(onCalculate).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, workingQuoteVersion: 1 }),
        'Price',
        99.5,
        0,
      ),
    )
  })

  it('locks only the calculating Case while unrelated RFQs remain operable', async () => {
    let resolveCalculation!: () => void
    const calculation = new Promise<void>((resolve) => {
      resolveCalculation = resolve
    })
    const onCalculate = vi.fn().mockReturnValue(calculation)
    const onRelease = vi.fn().mockResolvedValue(undefined)
    const caseA = { ...traderRow, owned: true }
    const caseB = {
      ...traderRow,
      caseId: 202,
      clientId: 'client-002',
      clientName: 'Client Two',
      owned: true,
    }
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[caseA, caseB]}
        onCalculate={onCalculate}
        onRelease={onRelease}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))
    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    expect(onCalculate).toHaveBeenCalledTimes(1)
    expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
      'data-editable',
      'false',
    )
    expect(screen.getByTestId('grid-202-price')).toHaveAttribute(
      'data-editable',
      'true',
    )
    expect(screen.getByRole('button', { name: 'Search' })).toBeEnabled()

    fireEvent.click(screen.getByText(/client-002 Client Two/))
    const release = screen.getByRole('button', { name: 'Release' })
    expect(release).toBeEnabled()
    fireEvent.click(release)
    await waitFor(() =>
      expect(onRelease).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 202 }),
      ),
    )

    resolveCalculation()
    await waitFor(() =>
      expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
        'data-editable',
        'true',
      ),
    )
  }, 10_000)

  it('ignores a calculation response from an obsolete refresh generation', async () => {
    let resolveCalculation!: (value: {
      caseId: number
      revisionId: string
      mode: 'Calculated'
      calculated: null
      manual: null
      version: number
      currentVersion: number
    }) => void
    const calculation = new Promise<{
      caseId: number
      revisionId: string
      mode: 'Calculated'
      calculated: null
      manual: null
      version: number
      currentVersion: number
    }>((resolve) => {
      resolveCalculation = resolve
    })
    const onPatchRow = vi.fn()
    const onReload = vi.fn()
    const { rerender } = render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        refreshGeneration={0}
        onCalculate={() => calculation}
        onPatchRow={onPatchRow}
        onReload={onReload}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))
    rerender(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        refreshGeneration={1}
        onCalculate={() => calculation}
        onPatchRow={onPatchRow}
        onReload={onReload}
      />,
    )
    resolveCalculation({
      caseId: 201,
      revisionId: traderRow.currentRevisionId,
      mode: 'Calculated',
      calculated: null,
      manual: null,
      version: 2,
      currentVersion: 4,
    })

    await act(async () => {
      await calculation
    })
    expect(onPatchRow).not.toHaveBeenCalled()
    expect(onReload).not.toHaveBeenCalled()
  })

  it('unlocks the Case after a calculation timeout', async () => {
    vi.useFakeTimers()
    try {
      render(
        <TraderScreen
          {...traderProps}
          rfqs={[{ ...traderRow, owned: true }]}
          onCalculate={() => new Promise(() => undefined)}
        />,
      )

      fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))
      await act(async () => {
        await vi.advanceTimersByTimeAsync(10_001)
      })

      expect(screen.getByTestId('grid-201-calcStatus')).toHaveTextContent('!')
      expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
        'data-editable',
        'true',
      )
    } finally {
      vi.useRealTimers()
    }
  })

  it('patches an authoritative calculation response without refreshing while Paused', async () => {
    const onPatchRow = vi.fn()
    const onReload = vi.fn()
    const payload = {
      driver: 'Price' as const,
      driverValue: 99.5,
      price: 99.5,
      bbgYield: 0.8,
      baseSimpleYield: 0.81,
      simpleYieldSlide: 0.03,
      finalSimpleYield: 0.84,
      internalYield: 0.83,
      gSpread: 5,
      asw: 8,
      ysc: 6,
      iSpread: 7,
      zSpread: 6,
    }
    const onCalculate = vi.fn().mockResolvedValue({
      caseId: 201,
      revisionId: traderRow.currentRevisionId,
      mode: 'Calculated',
      calculated: payload,
      manual: null,
      version: 2,
      currentVersion: 4,
    })
    render(
      <TraderScreen
        {...traderProps}
        refreshMode="paused"
        rfqs={[{ ...traderRow, owned: true }]}
        onCalculate={onCalculate}
        onPatchRow={onPatchRow}
        onReload={onReload}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    await waitFor(() =>
      expect(onPatchRow).toHaveBeenCalledWith(201, expect.any(Function)),
    )
    expect(onReload).not.toHaveBeenCalled()
  })

  it('unlocks the row and exposes a compact calculation failure state', async () => {
    const onCalculate = vi.fn().mockRejectedValue({
      data: {
        code: 'CalculationFailure',
        calculationErrorCode: 'PRICER_DOWN',
        detail: 'Pricing is unavailable.',
        traceId: 'trace-1',
        failureLogId: 'log-1',
      },
    })
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[{ ...traderRow, owned: true }]}
        onCalculate={onCalculate}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Edit Price 201' }))

    await waitFor(() =>
      expect(screen.getByTestId('grid-201-calcStatus')).toHaveTextContent('!'),
    )
    expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
      'data-editable',
      'true',
    )
  })

  it('shows yields as percent and spreads as basis points with numeric editors', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[
          {
            ...traderRow,
            owned: true,
            calculated: {
              driver: 'Price',
              driverValue: 99.5,
              price: 99.5,
              bbgYield: 0.8,
              baseSimpleYield: 0.81,
              simpleYieldSlide: 0.03,
              finalSimpleYield: 0.84,
              internalYield: 0.83,
              gSpread: 5,
              asw: 8,
              ysc: 6,
              iSpread: 7,
              zSpread: 6,
            },
          },
        ]}
      />,
    )

    expect(screen.getByTestId('grid-201-bbgYield')).toHaveTextContent('0.8%')
    expect(screen.getByTestId('grid-201-baseSimpleYield')).toHaveTextContent(
      '0.81%',
    )
    expect(screen.getByTestId('grid-201-simpleYieldSlide')).toHaveTextContent(
      '0.03%',
    )
    expect(screen.getByTestId('grid-201-finalSimpleYield')).toHaveTextContent(
      '0.84%',
    )
    expect(screen.getByTestId('grid-201-gSpread')).toHaveTextContent('5 bp')
    expect(screen.getByTestId('grid-201-bbgYield')).toHaveAttribute(
      'data-cell-editor',
      'agNumberCellEditor',
    )
    expect(screen.getByTestId('grid-201-gSpread')).toHaveAttribute(
      'data-cell-editor',
      'agNumberCellEditor',
    )
  })

  it('hides every calculated numeric result when switched to empty Manual mode', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[
          {
            ...traderRow,
            owned: true,
            workingQuoteMode: 'Manual',
            manual: { price: null, finalSimpleYield: null },
            calculated: {
              driver: 'Price',
              driverValue: 99.5,
              price: 99.5,
              bbgYield: 0.8,
              baseSimpleYield: 0.81,
              simpleYieldSlide: 0.03,
              finalSimpleYield: 0.84,
              internalYield: 0.83,
              gSpread: 5,
              asw: 8,
              ysc: 6,
              iSpread: 7,
              zSpread: 6,
            },
          },
        ]}
      />,
    )

    for (const column of [
      'price',
      'bbgYield',
      'baseSimpleYield',
      'gSpread',
      'simpleYieldSlide',
      'finalSimpleYield',
    ]) {
      expect(screen.getByTestId(`grid-201-${column}`)).toBeEmptyDOMElement()
    }
  })

  it('confirms a populated quote using the trader default expiry', async () => {
    const onConfirmQuote = vi.fn().mockResolvedValue(undefined)
    const populated = {
      ...traderRow,
      owned: true,
      calculated: {
        driver: 'Price' as const,
        driverValue: 99.5,
        price: 99.5,
        bbgYield: 0.8,
        baseSimpleYield: 0.81,
        simpleYieldSlide: 0.03,
        finalSimpleYield: 0.84,
        internalYield: 0.83,
        gSpread: 5,
        asw: 8,
        ysc: 6,
        iSpread: 7,
        zSpread: 6,
      },
    }
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[populated]}
        onConfirmQuote={onConfirmQuote}
      />,
    )
    fireEvent.click(screen.getByText(/client-001 Client One/))
    const confirmButton = screen.getByRole('button', { name: 'Confirm' })
    await waitFor(() => expect(confirmButton).toBeEnabled())
    fireEvent.click(confirmButton)
    fireEvent.click(await screen.findByRole('button', { name: 'Apply' }))

    await waitFor(() =>
      expect(onConfirmQuote).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201 }),
        5,
      ),
    )
  }, 10_000)

  it('locks quote editing after QuoteStatus becomes Quoted', () => {
    render(
      <TraderScreen
        {...traderProps}
        rfqs={[
          {
            ...traderRow,
            owned: true,
            quoteStatus: 'Quoted',
            quoteRequestReason: null,
            currentQuoteId: '00000000-0000-0000-0000-000000000301',
          },
        ]}
      />,
    )

    expect(screen.getByTestId('grid-201-price')).toHaveAttribute(
      'data-editable',
      'false',
    )
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled()
  })

  it('updates the Trader-only Memo after close', async () => {
    const onUpdateMemo = vi.fn().mockResolvedValue(undefined)
    render(
      <TraderScreen
        {...traderProps}
        onUpdateMemo={onUpdateMemo}
        rfqs={[
          {
            ...traderRow,
            rfqStatus: 'Hit',
            quoteStatus: null,
            quoteRequestReason: null,
            currentQuoteId: null,
            closedQuoteId: '00000000-0000-0000-0000-000000000301',
            owned: false,
            traderMemo: 'existing desk note',
            traderMemoVersion: 4,
          },
        ]}
      />,
    )

    fireEvent.click(screen.getByText(/client-001 Client One/))
    fireEvent.click(screen.getByRole('button', { name: 'Edit Memo 201' }))

    await waitFor(() =>
      expect(onUpdateMemo).toHaveBeenCalledWith(
        expect.objectContaining({ caseId: 201, traderMemoVersion: 4 }),
        'post-close desk note',
      ),
    )
  })
})
