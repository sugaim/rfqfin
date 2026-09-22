import type { ReactElement, RefObject } from 'react'
import { Alert, Button, Input, Segmented, Select, Space } from 'antd'
import type { InputRef } from 'antd'
import type {
  ColDef,
  DefaultMenuItem,
  GridApi,
  MenuItemDef,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type { RfqSearchItem, RfqSearchResult } from '@/services/api'
import type { SearchDatePreset } from '@/features/trader/traderModel'

const datePresets: SearchDatePreset[] = ['1M', '3M', '6M', '1Y', '2Y', '5Y']
const textFilterKeys = [
  'clientId',
  'securityId',
  'categoryId',
  'contactOwnerId',
  'salesId',
  'assignedTraderId',
] as const
const rfqStatuses = ['Draft', 'Active', 'Presented', 'Cancelled', 'Hit', 'Away']

interface TraderSearchSectionProps {
  open: boolean
  filtersOpen: boolean
  caseInputRef: RefObject<InputRef | null>
  preset: SearchDatePreset
  filters: Record<string, string>
  searching: boolean
  result: RfqSearchResult
  columns: ColDef<RfqSearchItem>[]
  onToggle: () => void
  onToggleFilters: () => void
  onPresetChange: (preset: SearchDatePreset) => void
  onFiltersChange: (filters: Record<string, string>) => void
  onSearch: () => void | Promise<void>
  onGridReady: (api: GridApi<RfqSearchItem>) => void
  getLayoutMenu: (
    api: GridApi<RfqSearchItem>,
  ) => DefaultMenuItem | MenuItemDef<RfqSearchItem>
}

export function TraderSearchSection({
  open,
  filtersOpen,
  caseInputRef,
  preset,
  filters,
  searching,
  result,
  columns,
  onToggle,
  onToggleFilters,
  onPresetChange,
  onFiltersChange,
  onSearch,
  onGridReady,
  getLayoutMenu,
}: TraderSearchSectionProps): ReactElement {
  const updateFilter = (key: string, value: string) =>
    onFiltersChange({ ...filters, [key]: value })

  return (
    <section
      className={`trader-search-section ${open ? '' : 'collapsed'}`}
      aria-label="RFQ Search"
    >
      <header>
        <Button type="text" size="small" onClick={onToggle}>
          {open ? '▾' : '▸'} RFQ Search
        </Button>
        {open && (
          <Space size={4}>
            <Button type="text" size="small" onClick={onToggleFilters}>
              {filtersOpen ? 'Hide filters' : 'Filters'}
            </Button>
          </Space>
        )}
      </header>
      {open && (
        <div
          className={`trader-search-body ${filtersOpen ? '' : 'filters-collapsed'}`}
        >
          {filtersOpen && (
            <div
              className="trader-search-filters"
              onKeyDown={(event) => {
                if (event.key === 'Enter') void onSearch()
              }}
            >
              <Input
                ref={caseInputRef}
                size="small"
                aria-label="Search Case"
                placeholder="Case"
                value={filters.caseId ?? ''}
                onChange={(event) => updateFilter('caseId', event.target.value)}
              />
              <Segmented
                size="small"
                aria-label="Search date preset"
                value={preset}
                options={datePresets}
                onChange={(value) => onPresetChange(value as SearchDatePreset)}
              />
              {textFilterKeys.map((key) => (
                <Input
                  key={key}
                  size="small"
                  aria-label={`Search ${key}`}
                  placeholder={key}
                  value={filters[key] ?? ''}
                  onChange={(event) => updateFilter(key, event.target.value)}
                />
              ))}
              <Select
                size="small"
                aria-label="Search RFQ Status"
                placeholder="RFQ Status"
                allowClear
                value={filters.status || undefined}
                onChange={(status) => updateFilter('status', status ?? '')}
                options={rfqStatuses.map((value) => ({ value, label: value }))}
              />
              <Button
                size="small"
                type="primary"
                loading={searching}
                onClick={() => void onSearch()}
              >
                Search
              </Button>
            </div>
          )}
          <div className="trader-search-results">
            {result.requiresNarrowing && (
              <Alert
                banner
                type="warning"
                message="Result cap reached. Narrow the search."
              />
            )}
            <AgGridReact<RfqSearchItem>
              rowData={result.items}
              columnDefs={columns}
              getRowId={({ data }) => String(data.caseId)}
              defaultColDef={{ sortable: true, filter: true, resizable: true }}
              rowSelection={undefined}
              rowHeight={27}
              headerHeight={29}
              onGridReady={({ api }) => onGridReady(api)}
              getContextMenuItems={({ api }) => [getLayoutMenu(api)]}
            />
          </div>
        </div>
      )}
    </section>
  )
}
