import { useEffect, useRef } from 'react'
import type { GridApi, MenuItemDef } from 'ag-grid-community'
import type { RfqSearchItem, TraderRfq } from '@/services/api'
import {
  applyGridLayout,
  gridLayoutMenu,
  initializeGridLayout,
  type GridColumnGroupState,
} from '@/features/grid/gridLayout'
import type {
  TraderGridConfigKey,
  TraderGridLayoutActions,
} from '@/features/trader/traderContracts'

export interface TraderGridLayoutsController {
  initialize: <T>(key: TraderGridConfigKey, api: GridApi<T>) => void
  menu: <T>(key: TraderGridConfigKey, api: GridApi<T>) => MenuItemDef<T>
}

export function useTraderGridLayouts(
  actions: TraderGridLayoutActions,
): TraderGridLayoutsController {
  const mainGrid = useRef<GridApi<TraderRfq> | null>(null)
  const searchGrid = useRef<GridApi<RfqSearchItem> | null>(null)
  const confirmGrid = useRef<GridApi<TraderRfq> | null>(null)
  const defaults = useRef<Record<TraderGridConfigKey, GridColumnGroupState>>({
    main: [],
    search: [],
    confirm: [],
  })

  const gridFor = (key: TraderGridConfigKey): GridApi<unknown> | null => {
    if (key === 'main') return mainGrid.current as GridApi<unknown> | null
    if (key === 'search') return searchGrid.current as GridApi<unknown> | null

    return confirmGrid.current as GridApi<unknown> | null
  }

  useEffect(() => {
    const grid = gridFor('main')
    if (grid) applyGridLayout(grid, actions.configs.main, defaults.current.main)
  }, [actions.configs.main])

  useEffect(() => {
    const grid = gridFor('search')
    if (grid)
      applyGridLayout(grid, actions.configs.search, defaults.current.search)
  }, [actions.configs.search])

  useEffect(() => {
    const grid = gridFor('confirm')
    if (grid)
      applyGridLayout(grid, actions.configs.confirm, defaults.current.confirm)
  }, [actions.configs.confirm])

  const initialize = <T>(key: TraderGridConfigKey, api: GridApi<T>) => {
    if (key === 'main') mainGrid.current = api as GridApi<TraderRfq>
    else if (key === 'search')
      searchGrid.current = api as GridApi<RfqSearchItem>
    else confirmGrid.current = api as GridApi<TraderRfq>
    defaults.current[key] = initializeGridLayout(api, actions.configs[key])
  }

  const menu = <T>(key: TraderGridConfigKey, api: GridApi<T>) =>
    gridLayoutMenu({
      api,
      configJson: actions.configs[key],
      defaultColumnGroupState: defaults.current[key],
      onSave: actions.save
        ? (configJson) => actions.save!(key, configJson)
        : undefined,
    })

  return { initialize, menu }
}
