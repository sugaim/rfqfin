import type {
  DefaultMenuItem,
  GetContextMenuItemsParams,
  MenuItemDef,
} from 'ag-grid-community'
import type { SalesRfq } from '@/services/api'
import {
  commandEligible,
  requiresConfirmation,
  type SalesCommand,
} from '@/features/sales/salesModel'
import {
  gridLayoutMenu,
  type GridColumnGroupState,
} from '@/features/grid/gridLayout'
import type { SalesGridLayoutActions } from '@/features/sales/salesContracts'

interface BuildSalesContextMenuInput {
  params: GetContextMenuItemsParams<SalesRfq>
  currentUserId: string
  gridLayout: SalesGridLayoutActions
  defaultColumnGroupState: GridColumnGroupState
  onRequestCommand: (
    command: SalesCommand,
    row: SalesRfq,
    confirm: boolean,
  ) => void
  onExecuteCommand: (command: SalesCommand, row: SalesRfq) => void
}

export function buildSalesContextMenu({
  params,
  currentUserId,
  gridLayout,
  defaultColumnGroupState,
  onRequestCommand,
  onExecuteCommand,
}: BuildSalesContextMenuInput): (DefaultMenuItem | MenuItemDef<SalesRfq>)[] {
  const row = params.node?.data
  const items: (DefaultMenuItem | MenuItemDef<SalesRfq>)[] = []
  if (row) {
    const add = (command: SalesCommand, label: string) => {
      if (commandEligible(command, row, currentUserId))
        items.push({
          name: label,
          action: () =>
            onRequestCommand(
              command,
              row,
              requiresConfirmation('context-menu', command),
            ),
        })
    }
    add('present', 'Present')
    add('unpresent', 'Unpresent')
    add('hit', 'Hit')
    add('away', 'Away')
    add('cancel', 'Cancel')
    add('reopen', 'Reopen')
    add('confirm-amendment', 'Confirm Amendment')
    add('discard-amendment', 'Discard Amendment')
    if (items.length) items.push('separator')
    items.push({
      name: 'Create New from Existing',
      action: () => onExecuteCommand('create-from-existing', row),
    })
    items.push({
      name: 'Copy',
      subMenu: [
        {
          name: 'Case ID',
          action: () => void navigator.clipboard.writeText(String(row.caseId)),
        },
        {
          name: 'Security ID',
          action: () => void navigator.clipboard.writeText(row.securityId),
        },
        {
          name: 'Client ID',
          action: () => void navigator.clipboard.writeText(row.clientId),
        },
      ],
    })
  }
  if (items.length) items.push('separator')
  items.push({
    name: 'Select All Filtered',
    action: () =>
      params.api.forEachNodeAfterFilter((node) => node.setSelected(true)),
  })
  items.push({
    name: 'Clear Selection',
    action: () => params.api.deselectAll(),
  })
  items.push('separator')
  items.push(
    gridLayoutMenu({
      api: params.api,
      configJson: gridLayout.configJson,
      defaultColumnGroupState,
      onSave: gridLayout.save,
    }),
  )

  return items
}
