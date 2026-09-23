import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import type { ReactNode } from 'react'
import { useState } from 'react'
import { vi } from 'vitest'
import { PostProcessScreen } from '@/pages/post-process/PostProcessScreen'
import type {
  BulkItemResult,
  PostProcessCommitItem,
  PostProcessItem,
  PostProcessPreset,
  PostProcessScope,
} from '@/services/api'

type GridColumn = {
  field?: string
  colId?: string
  headerName?: string
  editable?: boolean | ((params: { data: PostProcessItem }) => boolean)
  cellRenderer?: (params: { data: PostProcessItem }) => ReactNode
  valueGetter?: (params: { data: PostProcessItem }) => unknown
}

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({
    rowData,
    columnDefs,
    onCellEditRequest,
    rowClassRules,
  }: {
    rowData: PostProcessItem[]
    columnDefs: GridColumn[]
    onCellEditRequest: (event: {
      data: PostProcessItem
      newValue: string
      column: { getColId: () => string }
    }) => void
    rowClassRules: Record<
      string,
      (params: { data: PostProcessItem }) => boolean
    >
  }) => (
    <div>
      <div>{columnDefs.map((column) => column.headerName).join(' | ')}</div>
      {rowData.map((row) => (
        <div
          key={row.caseId}
          data-testid={`post-process-row-${row.caseId}`}
          className={Object.entries(rowClassRules)
            .filter(([, rule]) => rule({ data: row }))
            .map(([name]) => name)
            .join(' ')}
        >
          <span>{`${row.clientName} ${row.securityName} ${row.rfqStatus}`}</span>
          {columnDefs.map((column) => {
            const key = column.colId ?? column.field ?? column.headerName
            const editable =
              typeof column.editable === 'function'
                ? column.editable({ data: row })
                : Boolean(column.editable)

            return (
              <span
                key={key}
                data-testid={`column-${row.caseId}-${key}`}
                data-editable={String(editable)}
              >
                {column.valueGetter
                  ? String(column.valueGetter({ data: row }))
                  : ''}
                {column.cellRenderer?.({ data: row })}
                {key === 'myMemo' && (
                  <button
                    onClick={() =>
                      onCellEditRequest({
                        data: row,
                        newValue: 'follow tomorrow',
                        column: { getColId: () => 'myMemo' },
                      })
                    }
                  >
                    Edit My Memo {row.caseId}
                  </button>
                )}
                {key === 'correctionReason' && editable && (
                  <button
                    onClick={() =>
                      onCellEditRequest({
                        data: row,
                        newValue: 'booking correction',
                        column: { getColId: () => 'correctionReason' },
                      })
                    }
                  >
                    Edit Correction Reason {row.caseId}
                  </button>
                )}
              </span>
            )
          })}
        </div>
      ))}
    </div>
  ),
}))

const baseItem: PostProcessItem = {
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
  rfqStatus: 'Active',
  currentVersion: 3,
  salesAndTradingMessage: 'shared message',
  myMemo: 'private memo',
  myMemoVersion: 2,
  price: 99.25,
  finalSimpleYield: 1.5,
  yield: 1.4,
  ysc: 1.3,
  gSpread: 20,
  closedBusinessDate: null,
  lastCorrectionReason: null,
  lastChangedBy: 'sales-dev',
  lastChangedAt: '2026-09-22T01:00:00Z',
}

function renderScreen(
  overrides: Partial<React.ComponentProps<typeof PostProcessScreen>> = {},
) {
  const props: React.ComponentProps<typeof PostProcessScreen> = {
    items: [baseItem],
    currentUserId: 'sales-dev',
    preset: 'Today',
    scope: 'Mine',
    isLoading: false,
    isError: false,
    isCommitting: false,
    onPresetChange: vi.fn(),
    onScopeChange: vi.fn(),
    onRefresh: vi.fn().mockResolvedValue(undefined),
    onCommit: vi.fn().mockResolvedValue([]),
    ...overrides,
  }

  return { ...render(<PostProcessScreen {...props} />), props }
}

describe('Post Process staging', () => {
  it('stages and replaces outcomes while pending changes survive view switches', () => {
    const onPresetChange = vi.fn()
    const onScopeChange = vi.fn()
    const onCommit = vi.fn()
    renderScreen({
      items: [baseItem, { ...baseItem, caseId: 102 }],
      onPresetChange,
      onScopeChange,
      onCommit,
    })

    const firstRow = screen.getByTestId('post-process-row-101')
    fireEvent.click(within(firstRow).getByRole('button', { name: 'Hit' }))
    expect(onCommit).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Confirm Changes (1)' }))
    expect(screen.getByTestId('post-process-row-101')).toHaveClass(
      'post-process-row-unclosed',
      'post-process-row-pending',
    )

    fireEvent.click(within(firstRow).getByRole('button', { name: 'Away' }))
    fireEvent.click(screen.getByText('Unclosed'))
    fireEvent.click(screen.getByText('All permitted'))
    expect(onPresetChange).toHaveBeenCalledWith('Unclosed')
    expect(onScopeChange).toHaveBeenCalledWith('AllPermitted')
    expect(screen.getByRole('button', { name: 'Confirm Changes (1)' }))

    fireEvent.click(screen.getByRole('button', { name: 'Confirm Changes (1)' }))
    expect(screen.getAllByText('Active -> Away')).not.toHaveLength(0)
    expect(screen.queryByText('Active -> Hit')).not.toBeInTheDocument()
    expect(screen.getByText('#101')).toBeInTheDocument()
    expect(screen.queryByText('#102')).not.toBeInTheDocument()
  })

  it('stages lifecycle and own memo together and requires a correction reason', async () => {
    const onCommit = vi
      .fn<(items: PostProcessCommitItem[]) => Promise<BulkItemResult[]>>()
      .mockResolvedValue([
        { caseId: 101, status: 'Succeeded', code: null, message: null },
      ])
    const hit = {
      ...baseItem,
      rfqStatus: 'Hit' as const,
      lastCorrectionReason: 'previous audit reason',
    }
    renderScreen({ items: [hit], onCommit })

    expect(screen.queryByText(/Sales Memo|Trader Memo/)).not.toBeInTheDocument()
    expect(screen.getByText(/Px/)).toBeInTheDocument()
    expect(screen.getByTestId('column-101-price')).not.toHaveAttribute(
      'data-editable',
      'true',
    )

    const reasonCell = screen.getByTestId('column-101-correctionReason')
    expect(reasonCell).toHaveTextContent('previous audit reason')

    fireEvent.click(screen.getByRole('button', { name: 'Correct to Away' }))
    expect(reasonCell).not.toHaveTextContent('previous audit reason')
    expect(
      screen.getByText(/Correction Reason is required/),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Confirm Changes (1)' }),
    ).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: 'Edit My Memo 101' }))
    fireEvent.click(
      screen.getByRole('button', { name: 'Edit Correction Reason 101' }),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Changes (1)' }))
    expect(screen.getByText('Changed')).toBeInTheDocument()
    expect(screen.getAllByText('booking correction')).not.toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: 'Commit' }))

    await waitFor(() => expect(onCommit).toHaveBeenCalledTimes(1))
    expect(onCommit.mock.calls[0][0]).toEqual([
      {
        caseId: 101,
        expectedCurrentVersion: 3,
        lifecycleChange: {
          type: 'CorrectToAway',
          correctionReason: 'booking correction',
        },
        memoChange: { expectedVersion: 2, value: 'follow tomorrow' },
      },
    ])
  })

  it('warns before Refresh and keeps failed pending changes after commit', async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined)
    const onCommit = vi.fn().mockResolvedValue([
      {
        caseId: 101,
        status: 'Failed',
        code: 'VersionConflict',
        message: 'changed',
      },
    ])
    renderScreen({ onRefresh, onCommit })

    fireEvent.click(screen.getByRole('button', { name: 'Away' }))
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }))
    expect(
      await screen.findByText('Discard all uncommitted Post Process changes?'),
    ).toBeInTheDocument()
    expect(onRefresh).not.toHaveBeenCalled()
    fireEvent.click(screen.getAllByRole('button', { name: 'Cancel' }).at(-1)!)
    expect(screen.getByRole('button', { name: 'Confirm Changes (1)' }))

    fireEvent.click(screen.getByRole('button', { name: 'Confirm Changes (1)' }))
    fireEvent.click(screen.getByRole('button', { name: 'Commit' }))
    await waitFor(() => expect(onCommit).toHaveBeenCalledTimes(1))
    expect(onRefresh).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Confirm Changes (1)' }))
    fireEvent.click(screen.getByText(/0 succeeded/))
    expect(screen.getByText(/Case 101: Failed/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }))
    await screen.findByText('Discard all uncommitted Post Process changes?')
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1))
    expect(
      screen.getByRole('button', { name: 'Confirm Changes (0)' }),
    ).toBeDisabled()
  })

  it('keeps outcome and pending styling as independent visual channels', () => {
    renderScreen({
      items: [
        { ...baseItem, rfqStatus: 'Hit' },
        { ...baseItem, caseId: 102, rfqStatus: 'Away' },
        { ...baseItem, caseId: 103, rfqStatus: 'Cancelled' },
      ],
    })

    const hit = screen.getByTestId('post-process-row-101')
    const away = screen.getByTestId('post-process-row-102')
    const cancelled = screen.getByTestId('post-process-row-103')
    expect(within(hit).getByText('HIT')).toHaveClass('ant-tag-success')
    expect(within(away).getByText('AWAY')).toHaveClass('ant-tag-error')
    expect(cancelled).toHaveClass('post-process-row-cancelled')

    fireEvent.click(
      within(hit).getByRole('button', { name: 'Correct to Away' }),
    )
    expect(hit).toHaveClass('post-process-row-pending')
    expect(within(hit).getByText('HIT')).toHaveClass('ant-tag-success')
  })
})

describe('Post Process reconciliation', () => {
  function Harness({ initialPreset }: { initialPreset: PostProcessPreset }) {
    const [preset, setPreset] = useState(initialPreset)
    const [scope, setScope] = useState<PostProcessScope>('Mine')
    const [items, setItems] = useState([baseItem])

    return (
      <PostProcessScreen
        items={items}
        currentUserId="sales-dev"
        preset={preset}
        scope={scope}
        isLoading={false}
        isError={false}
        isCommitting={false}
        onPresetChange={setPreset}
        onScopeChange={setScope}
        onCommit={() => {
          setItems((current) =>
            preset === 'Unclosed'
              ? []
              : current.map((item) => ({
                  ...item,
                  rfqStatus: 'Away' as const,
                  currentVersion: item.currentVersion + 1,
                })),
          )

          return Promise.resolve([
            {
              caseId: 101,
              status: 'Succeeded' as const,
              code: null,
              message: null,
            },
          ])
        }}
        onRefresh={() => Promise.resolve()}
      />
    )
  }

  it.each([
    ['Unclosed', false],
    ['Today', true],
  ] as const)(
    'reconciles successful close in %s from the authoritative refresh',
    async (preset, remainsVisible) => {
      render(<Harness initialPreset={preset} />)
      fireEvent.click(screen.getByRole('button', { name: 'Away' }))
      fireEvent.click(
        screen.getByRole('button', { name: 'Confirm Changes (1)' }),
      )
      fireEvent.click(screen.getByRole('button', { name: 'Commit' }))

      await waitFor(() => {
        const row = screen.queryByTestId('post-process-row-101')
        if (remainsVisible) {
          expect(row).toHaveTextContent('Away')
          expect(row).not.toHaveClass('post-process-row-pending')
        } else {
          expect(row).not.toBeInTheDocument()
        }
      })
    },
  )
})
