import type { GridApi, MenuItemDef } from 'ag-grid-community'
import { describe, expect, it, vi } from 'vitest'
import {
  applyGridLayout,
  captureGridLayout,
  gridLayoutMenu,
  resetGridLayout,
} from '@/features/grid/gridLayout'

function gridApi() {
  return {
    getColumnState: vi.fn(() => [
      {
        colId: 'caseId',
        hide: false,
        width: 120,
        pinned: 'left',
        sort: 'asc',
        sortIndex: 0,
      },
    ]),
    getColumnGroupState: vi.fn(() => [{ groupId: 'terms', open: false }]),
    applyColumnState: vi.fn(),
    setColumnGroupState: vi.fn(),
    resetColumnState: vi.fn(),
    getFilterModel: vi.fn(() => ({ client: { value: 'ignored' } })),
    getSelectedRows: vi.fn(() => [{ caseId: 101 }]),
  } as unknown as GridApi
}

describe('Grid layout persistence', () => {
  it('captures only column and column-group layout state', () => {
    const api = gridApi()

    expect(captureGridLayout(api)).toEqual({
      columnState: [
        {
          colId: 'caseId',
          hide: false,
          width: 120,
          pinned: 'left',
          sort: 'asc',
          sortIndex: 0,
        },
      ],
      columnGroupState: [{ groupId: 'terms', open: false }],
    })
    expect(api.getFilterModel).not.toHaveBeenCalled()
    expect(api.getSelectedRows).not.toHaveBeenCalled()
  })

  it('provides Save, Load and Reset context actions', async () => {
    const api = gridApi()
    const onSave = vi.fn().mockResolvedValue(undefined)
    const configJson = JSON.stringify({
      columnState: [{ colId: 'client', width: 180 }],
      columnGroupState: [{ groupId: 'terms', open: true }],
    })
    const menu = gridLayoutMenu({
      api,
      configJson,
      defaultColumnGroupState: [{ groupId: 'terms', open: false }],
      onSave,
    })
    const actions = menu.subMenu as MenuItemDef[]

    expect(menu.name).toBe('Grid Layout')
    expect(actions.map((item) => item.name)).toEqual(['Save', 'Load', 'Reset'])
    actions[0].action?.({} as never)
    await vi.waitFor(() => expect(onSave).toHaveBeenCalledTimes(1))
    const saved = JSON.parse(onSave.mock.calls[0][0])
    expect(saved).toEqual(captureGridLayout(api))

    actions[1].action?.({} as never)
    expect(api.applyColumnState).toHaveBeenCalledWith({
      state: [{ colId: 'client', width: 180 }],
      applyOrder: true,
    })
    expect(api.setColumnGroupState).toHaveBeenCalledWith([
      { groupId: 'terms', open: true },
    ])

    actions[2].action?.({} as never)
    expect(api.resetColumnState).toHaveBeenCalled()
    expect(onSave).toHaveBeenCalledTimes(1)
  })

  it('resets safely for invalid layouts and disables Load when none is saved', () => {
    const api = gridApi()
    const defaults = [{ groupId: 'terms', open: false }]

    expect(applyGridLayout(api, '{invalid', defaults)).toBe(false)
    expect(api.resetColumnState).toHaveBeenCalled()
    expect(api.setColumnGroupState).toHaveBeenCalledWith(defaults)

    resetGridLayout(api, defaults)
    const menu = gridLayoutMenu({
      api,
      defaultColumnGroupState: defaults,
    })
    const actions = menu.subMenu as MenuItemDef[]
    expect(actions.find((item) => item.name === 'Load')?.disabled).toBe(true)
  })
})
