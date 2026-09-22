import type { SalesRfq } from '@/services/api'

export type SalesFilterPreset =
  'all' | 'owner' | 'sales' | 'owner-and-sales' | 'owner-or-sales'
export type SalesPaneMode =
  | 'neutral'
  | 'new'
  | 'draft'
  | 'waiting'
  | 'quoted'
  | 'presented'
  | 'cancelled'
  | 'hit'
  | 'away'
export type SalesCommand =
  | 'present'
  | 'unpresent'
  | 'hit'
  | 'away'
  | 'cancel'
  | 'reopen'
  | 'confirm-amendment'
  | 'discard-amendment'
  | 'create-from-existing'
export type SalesRowCommand = SalesCommand | 'confirm-draft' | 'discard-draft'
export type SalesBulkCommand =
  | 'away'
  | 'cancel'
  | 'present'
  | 'unpresent'
  | 'confirm-drafts'
  | 'discard-drafts'
  | 'confirm-amendments'
  | 'discard-amendments'
export type SalesInvocationSurface = 'work-pane' | 'row-action' | 'context-menu'

export function reflectConfirmedAmendment(row: SalesRfq): SalesRfq {
  return {
    ...row,
    notional: row.draftNotional ?? row.notional,
    settlementDate: row.draftSettlementDate ?? row.settlementDate,
    salesAndTradingMessage:
      row.draftSalesAndTradingMessage ?? row.salesAndTradingMessage,
    draftRevisionId: null,
    draftVersion: null,
    draftNotional: null,
    draftSettlementDate: null,
    draftSalesAndTradingMessage: null,
    rfqStatus: 'Active',
    quoteStatus: 'Requested',
    quoteRequestReason: 'Revised',
    currentVersion: row.currentVersion + 1,
  }
}

export function derivePaneMode(
  rows: SalesRfq[],
  activeCaseId: number | undefined,
  newIntent: boolean,
): SalesPaneMode {
  if (newIntent) return 'new'
  const selected = rows.find((row) => row.caseId === activeCaseId)
  if (!selected) return 'neutral'
  if (selected.revisionStatus === 'Draft') return 'draft'
  if (selected.rfqStatus === 'Presented') return 'presented'
  if (selected.rfqStatus === 'Cancelled') return 'cancelled'
  if (selected.rfqStatus === 'Hit') return 'hit'
  if (selected.rfqStatus === 'Away') return 'away'
  return selected.quoteStatus === 'Quoted' ? 'quoted' : 'waiting'
}

export function rowActionCommands(
  row: SalesRfq,
  userId: string,
): SalesRowCommand[] {
  if (row.contactOwnerId !== userId) return []
  if (row.revisionStatus === 'Draft') return ['confirm-draft', 'discard-draft']
  const commands: SalesRowCommand[] = []
  if (row.draftRevisionId)
    commands.push('confirm-amendment', 'discard-amendment')
  for (const command of [
    'present',
    'unpresent',
    'hit',
    'away',
    'cancel',
    'reopen',
  ] as const) {
    if (commandEligible(command, row, userId)) commands.push(command)
  }
  return commands
}

export function reconcileSelection(caseIds: number[], rows: SalesRfq[]) {
  const existing = new Set(rows.map((row) => row.caseId))
  return caseIds.filter((caseId) => existing.has(caseId))
}

export function matchesSalesPreset(
  row: SalesRfq,
  preset: SalesFilterPreset,
  currentUserId: string,
) {
  const owner = row.contactOwnerId === currentUserId
  const sales = row.salesId === currentUserId
  switch (preset) {
    case 'owner':
      return owner
    case 'sales':
      return sales
    case 'owner-and-sales':
      return owner && sales
    case 'owner-or-sales':
      return owner || sales
    default:
      return true
  }
}

export function displayState(row: SalesRfq) {
  if (row.rfqStatus === 'Draft') return 'DRAFT'
  if (row.rfqStatus === 'Presented') return 'PRESENTED'
  if (row.rfqStatus === 'Cancelled') return 'CANCELLED'
  if (row.rfqStatus === 'Hit') return 'HIT'
  if (row.rfqStatus === 'Away') return 'AWAY'
  return row.quoteStatus === 'Quoted' ? 'QUOTED' : 'WAITING'
}

export function elapsedLabel(stateSince: string, now = Date.now()) {
  const elapsed = Math.max(0, now - new Date(stateSince).getTime())
  const minutes = Math.floor(elapsed / 60_000)
  if (minutes < 60) return `${minutes}m`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ${minutes % 60}m`
  return `${Math.floor(hours / 24)}d ${hours % 24}h`
}

export function commandEligible(
  command: SalesCommand,
  row: SalesRfq,
  userId: string,
) {
  const owner = row.contactOwnerId === userId
  switch (command) {
    case 'present':
      return owner && row.rfqStatus === 'Active' && row.quoteStatus === 'Quoted'
    case 'unpresent':
      return owner && row.rfqStatus === 'Presented'
    case 'hit':
    case 'away':
      return (
        owner &&
        ['Active', 'Presented'].includes(row.rfqStatus) &&
        row.quoteStatus === 'Quoted'
      )
    case 'cancel':
      return owner && ['Active', 'Presented'].includes(row.rfqStatus)
    case 'reopen':
      return owner && row.rfqStatus === 'Cancelled'
    case 'confirm-amendment':
    case 'discard-amendment':
      return owner && Boolean(row.draftRevisionId)
    case 'create-from-existing':
      return true
  }
}

export function bulkEligibility(
  command: SalesBulkCommand,
  row: SalesRfq,
  userId: string,
) {
  switch (command) {
    case 'away':
      return commandEligible('away', row, userId)
    case 'cancel':
      return commandEligible('cancel', row, userId)
    case 'present':
      return commandEligible('present', row, userId)
    case 'unpresent':
      return commandEligible('unpresent', row, userId)
    case 'confirm-drafts':
      return (
        row.revisionStatus === 'Draft' &&
        row.notional !== null &&
        row.settlementDate !== null
      )
    case 'discard-drafts':
      return row.revisionStatus === 'Draft'
    case 'confirm-amendments':
    case 'discard-amendments':
      return Boolean(row.draftRevisionId && row.draftVersion)
  }
}

export function isTextEditingTarget(target: EventTarget | null) {
  const element = target instanceof HTMLElement ? target : null
  return Boolean(
    element &&
    (element.isContentEditable ||
      element.contentEditable === 'true' ||
      element.closest(
        'input, textarea, select, [contenteditable="true"], .ant-select',
      )),
  )
}

export function requiresConfirmation(
  surface: SalesInvocationSurface,
  command: SalesCommand,
) {
  return (
    surface === 'context-menu' && ['hit', 'away', 'cancel'].includes(command)
  )
}
