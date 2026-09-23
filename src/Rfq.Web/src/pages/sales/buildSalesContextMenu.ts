import type {
  DefaultMenuItem,
  GetContextMenuItemsParams,
  MenuItemDef,
} from 'ag-grid-community'
import type { SalesRfqResponse } from '@/generated/rfqApi'
import {
  commandEligible,
  requiresConfirmation,
  type SalesCommand,
} from '@/pages/sales/salesModel'
import {
  gridLayoutMenu,
  type GridColumnGroupState,
} from '@/shared/grid/gridLayout'
import type { SalesGridLayoutActions } from '@/pages/sales/salesContracts'

interface BuildSalesContextMenuInput {
  params: GetContextMenuItemsParams<SalesRfqResponse>
  currentUserId: string
  gridLayout: SalesGridLayoutActions
  defaultColumnGroupState: GridColumnGroupState
  onRequestCommand: (
    command: SalesCommand,
    row: SalesRfqResponse,
    confirm: boolean,
  ) => void
  onExecuteCommand: (command: SalesCommand, row: SalesRfqResponse) => void
}

export function buildSalesContextMenu({
  params,
  currentUserId,
  gridLayout,
  defaultColumnGroupState,
  onRequestCommand,
  onExecuteCommand,
}: BuildSalesContextMenuInput): (
  DefaultMenuItem | MenuItemDef<SalesRfqResponse>
)[] {
  const row = params.node?.data
  const items: (DefaultMenuItem | MenuItemDef<SalesRfqResponse>)[] = []
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
