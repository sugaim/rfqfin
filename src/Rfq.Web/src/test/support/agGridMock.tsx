import type { ReactNode } from 'react'
import { vi } from 'vitest'
import type { RfqSearchItem, SalesRfq, TraderRfq } from '@/services/api'

type GridRow = SalesRfq | TraderRfq | RfqSearchItem
type GridColumn = {
  field?: string
  colId?: string
  children?: GridColumn[]
  valueGetter?: (params: { data: GridRow }) => unknown
  valueFormatter?: (params: { value: unknown }) => unknown
  cellEditor?: string
  editable?: (params: { data: GridRow }) => boolean
  cellRenderer?: (params: { data: GridRow }) => ReactNode
}

vi.mock('ag-grid-react', async () => {
  const React = await import('react')

  return {
    AgGridReact: ({
      rowData,
      columnDefs,
      onRowClicked,
      onSelectionChanged,
      onCellEditRequest,
      readOnlyEdit,
      rowSelection,
      rowClassRules,
      statusBar,
      components,
    }: {
      rowData: GridRow[]
      columnDefs?: GridColumn[]
      onRowClicked?: (event: { data: GridRow }) => void
      onSelectionChanged?: (event: {
        api: { getSelectedRows: () => GridRow[] }
      }) => void
      onCellEditRequest?: (event: {
        data: GridRow
        newValue: string
        colDef: { field: string }
        column: { getColId: () => string }
        api: { refreshCells: ReturnType<typeof vi.fn> }
        node: object
      }) => void
      readOnlyEdit?: boolean
      rowSelection?: {
        mode: string
        checkboxes?: boolean
        headerCheckbox?: boolean
        enableClickSelection?: boolean
        enableSelectionWithoutKeys?: boolean
        selectAll?: string
      }
      rowClassRules?: Record<
        string,
        (params: {
          data: GridRow
          node: { isSelected: () => boolean }
        }) => boolean
      >
      statusBar?: {
        statusPanels: {
          statusPanel: string
          statusPanelParams?: { text?: string }
        }[]
      }
      components?: Record<string, (props: { text: string }) => ReactNode>
    }) => {
      const flatColumns = (columnDefs ?? []).flatMap(
        function flatten(column): GridColumn[] {
          return column.children ? column.children.flatMap(flatten) : [column]
        },
      )
      const [selectedIds, setSelectedIds] = React.useState<number[]>([])
      const anchorIndex = React.useRef<number | undefined>(undefined)
      const select = (row: GridRow, index: number, event: React.MouseEvent) => {
        let next: number[]
        if (event.shiftKey && anchorIndex.current !== undefined) {
          const [start, end] = [anchorIndex.current, index].sort(
            (left, right) => left - right,
          )
          next = rowData.slice(start, end + 1).map((item) => item.caseId)
        } else if (event.ctrlKey || event.metaKey) {
          next = selectedIds.includes(row.caseId)
            ? selectedIds.filter((caseId) => caseId !== row.caseId)
            : [...selectedIds, row.caseId]
          anchorIndex.current = index
        } else {
          next = [row.caseId]
          anchorIndex.current = index
        }
        setSelectedIds(next)
        onRowClicked?.({ data: row })
        onSelectionChanged?.({
          api: {
            getSelectedRows: () =>
              rowData.filter((item) => next.includes(item.caseId)),
          },
        })
      }

      return (
        <div
          data-testid="grid-selection-config"
          data-checkboxes={String(rowSelection?.checkboxes)}
          data-header-checkbox={String(rowSelection?.headerCheckbox)}
          data-click-selection={String(rowSelection?.enableClickSelection)}
          data-selection-without-keys={String(
            rowSelection?.enableSelectionWithoutKeys,
          )}
          data-select-all={rowSelection?.selectAll}
        >
          {rowData.map((row, index) => {
            const selected = selectedIds.includes(row.caseId)
            const classes = Object.entries(rowClassRules ?? {})
              .filter(([, rule]) =>
                rule({ data: row, node: { isSelected: () => selected } }),
              )
              .map(([name]) => name)
              .join(' ')

            return (
              <div
                key={row.caseId}
                data-testid={`grid-row-${row.caseId}`}
                className={classes}
              >
                <button onClick={(event) => select(row, index, event)}>
                  {row.clientId} {row.clientName} {row.securityId}
                  {'securityJapaneseName' in row
                    ? row.securityJapaneseName
                    : row.securityName}{' '}
                  {'securityBbgDisplay' in row ? row.securityBbgDisplay : ''}{' '}
                  {'rfqStatus' in row ? row.rfqStatus : row.status}{' '}
                  {'revisionStatus' in row ? row.revisionStatus : ''}{' '}
                  {row.quoteStatus}{' '}
                  {'quoteRequestReason' in row ? row.quoteRequestReason : ''}
                </button>
                {readOnlyEdit && onCellEditRequest && (
                  <button
                    onClick={() =>
                      onCellEditRequest({
                        data: row,
                        newValue: '99.5',
                        colDef: { field: 'notional' },
                        column: { getColId: () => 'price' },
                        api: { refreshCells: vi.fn() },
                        node: {},
                      })
                    }
                  >
                    Edit Price {row.caseId}
                  </button>
                )}
                {readOnlyEdit &&
                  onCellEditRequest &&
                  flatColumns.some(
                    (column) => column.field === 'traderMemo',
                  ) && (
                    <button
                      onClick={() =>
                        onCellEditRequest({
                          data: row,
                          newValue: 'post-close desk note',
                          colDef: { field: 'traderMemo' },
                          column: { getColId: () => 'traderMemo' },
                          api: { refreshCells: vi.fn() },
                          node: {},
                        })
                      }
                    >
                      Edit Memo {row.caseId}
                    </button>
                  )}
                {flatColumns
                  .filter(
                    (column) =>
                      column.colId || column.field === 'currentQuoteId',
                  )
                  .map((column) => {
                    const columnKey = column.colId ?? column.field!
                    const value = column.valueGetter
                      ? column.valueGetter({ data: row })
                      : (row as unknown as Record<string, unknown>)[columnKey]
                    const display =
                      column.valueFormatter?.({ value }) ?? value ?? ''

                    return (
                      <output
                        key={columnKey}
                        data-testid={`grid-${row.caseId}-${columnKey}`}
                        data-cell-editor={column.cellEditor ?? ''}
                        data-editable={
                          column.editable?.({ data: row }) ? 'true' : 'false'
                        }
                      >
                        {column.cellRenderer
                          ? column.cellRenderer({ data: row })
                          : String(display)}
                      </output>
                    )
                  })}
              </div>
            )
          })}
          {statusBar?.statusPanels.map((panel, index) => {
            const Component = components?.[panel.statusPanel]

            return Component ? (
              <div key={index}>
                {Component({
                  text: panel.statusPanelParams?.text ?? '',
                })}
              </div>
            ) : null
          })}
        </div>
      )
    },
  }
})
