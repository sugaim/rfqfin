import type { GridApi, MenuItemDef } from 'ag-grid-community'

export type GridColumnGroupState = ReturnType<GridApi['getColumnGroupState']>

export interface GridLayoutConfig {
  columnState: ReturnType<GridApi['getColumnState']>
  columnGroupState: GridColumnGroupState
}

export function captureGridLayout(api: GridApi): GridLayoutConfig {
  return {
    columnState: api.getColumnState(),
    columnGroupState: api.getColumnGroupState(),
  }
}

export function resetGridLayout(
  api: GridApi,
  defaultColumnGroupState: GridColumnGroupState,
) {
  api.resetColumnState()
  api.setColumnGroupState(defaultColumnGroupState)
}

export function applyGridLayout(
  api: GridApi,
  configJson: string | undefined,
  defaultColumnGroupState: GridColumnGroupState,
) {
  if (!configJson) return false
  try {
    const config = JSON.parse(configJson) as GridLayoutConfig
    if (!Array.isArray(config.columnState)) throw new Error('Invalid layout')
    api.applyColumnState({ state: config.columnState, applyOrder: true })
    api.setColumnGroupState(
      Array.isArray(config.columnGroupState) ? config.columnGroupState : [],
    )
    return true
  } catch {
    resetGridLayout(api, defaultColumnGroupState)
    return false
  }
}

export function initializeGridLayout(
  api: GridApi,
  configJson: string | undefined,
) {
  const defaultColumnGroupState = api.getColumnGroupState()
  applyGridLayout(api, configJson, defaultColumnGroupState)
  return defaultColumnGroupState
}

export function gridLayoutMenu<TData>({
  api,
  configJson,
  defaultColumnGroupState,
  onSave,
}: {
  api: GridApi<TData>
  configJson?: string
  defaultColumnGroupState: GridColumnGroupState
  onSave?: (configJson: string) => Promise<void>
}): MenuItemDef<TData> {
  return {
    name: 'Grid Layout',
    subMenu: [
      {
        name: 'Save',
        disabled: !onSave,
        action: () =>
          onSave && void onSave(JSON.stringify(captureGridLayout(api))),
      },
      {
        name: 'Load',
        disabled: !configJson,
        action: () => applyGridLayout(api, configJson, defaultColumnGroupState),
      },
      {
        name: 'Reset',
        action: () => resetGridLayout(api, defaultColumnGroupState),
      },
    ],
  }
}
