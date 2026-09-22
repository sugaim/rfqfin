import type { ReactElement } from 'react'
import { Button, Empty, List, Space, Tag, Tooltip, Typography } from 'antd'
import type { BulkItemResult, SalesRfq } from '@/services/api'
import { BulkResultBar } from '@/features/bulk/BulkResultBar'
import {
  bulkEligibility,
  displayState,
  rowActionCommands,
  type SalesBulkCommand,
  type SalesRowCommand,
} from '@/features/sales/salesModel'

export type SalesBulkResult = {
  command: SalesBulkCommand
  items: BulkItemResult[]
}

interface BulkPaneProps {
  rows: SalesRfq[]
  userId: string
  onOpen: (command: SalesBulkCommand) => void
}

export function BulkPane({
  rows,
  userId,
  onOpen,
}: BulkPaneProps): ReactElement {
  const actions: { command: SalesBulkCommand; label: string }[] = [
    { command: 'away', label: 'Away' },
    { command: 'cancel', label: 'Cancel' },
    { command: 'present', label: 'Present' },
    { command: 'unpresent', label: 'Unpresent' },
    { command: 'confirm-drafts', label: 'Confirm Drafts' },
    { command: 'discard-drafts', label: 'Discard Drafts' },
    { command: 'confirm-amendments', label: 'Confirm Amendments' },
    { command: 'discard-amendments', label: 'Discard Amendments' },
  ]
  if (rows.length < 2)
    return (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description="Select multiple RFQs for bulk operations"
      />
    )

  return (
    <div className="bulk-pane">
      <Typography.Paragraph type="secondary">
        The confirmation snapshot includes all {rows.length} selected Cases.
        Bulk Hit is intentionally unavailable.
      </Typography.Paragraph>
      <Space wrap>
        {actions.map(({ command, label }) => (
          <Button
            key={command}
            size="small"
            disabled={
              !rows.some((row) => bulkEligibility(command, row, userId))
            }
            onClick={() => onOpen(command)}
          >
            {label}
          </Button>
        ))}
      </Space>
      <List
        size="small"
        dataSource={rows.slice(0, 8)}
        renderItem={(row) => (
          <List.Item>
            Case {row.caseId} · {row.securityJapaneseName}{' '}
            <Tag>{displayState(row)}</Tag>
          </List.Item>
        )}
      />
      {rows.length > 8 && (
        <Typography.Text type="secondary">
          +{rows.length - 8} more
        </Typography.Text>
      )}
    </div>
  )
}

const rowActionLabels: Record<
  SalesRowCommand,
  { short: string; full: string }
> = {
  'confirm-draft': { short: '✓', full: 'Confirm Draft' },
  'discard-draft': { short: '×', full: 'Discard Draft' },
  present: { short: 'P', full: 'Present' },
  unpresent: { short: 'U', full: 'Unpresent' },
  hit: { short: 'H', full: 'Hit' },
  away: { short: 'A', full: 'Away' },
  cancel: { short: 'X', full: 'Cancel' },
  reopen: { short: 'R', full: 'Reopen' },
  'confirm-amendment': { short: 'A✓', full: 'Confirm Amendment' },
  'discard-amendment': { short: 'A×', full: 'Discard Amendment' },
  'create-from-existing': { short: '+', full: 'Create New from Existing' },
}

interface RowActionsProps {
  row: SalesRfq
  userId: string
  disabled: boolean
  onCommand: (command: SalesRowCommand) => void
}

export function RowActions({
  row,
  userId,
  disabled,
  onCommand,
}: RowActionsProps): ReactElement {
  return (
    <Space.Compact className="row-action-buttons">
      {rowActionCommands(row, userId).map((command) => (
        <Tooltip
          key={command}
          title={
            disabled
              ? `${rowActionLabels[command].full} (Pause to enable)`
              : rowActionLabels[command].full
          }
        >
          <Button
            size="small"
            aria-label={`Row ${row.caseId} ${rowActionLabels[command].full}`}
            disabled={disabled}
            onClick={(event) => {
              event.stopPropagation()
              onCommand(command)
            }}
          >
            {rowActionLabels[command].short}
          </Button>
        </Tooltip>
      ))}
    </Space.Compact>
  )
}

interface SalesBulkResultBarProps {
  result: SalesBulkResult
  expanded: boolean
  onToggle: () => void
}

export function SalesBulkResultBar({
  result,
  expanded,
  onToggle,
}: SalesBulkResultBarProps): ReactElement {
  return (
    <BulkResultBar
      label={`Bulk ${bulkCommandLabel(result.command)}`}
      items={result.items}
      expanded={expanded}
      onToggle={onToggle}
      toggleType="link"
      aria-label="Bulk result"
      summaryRole="status"
    />
  )
}

function bulkCommandLabel(command: SalesBulkCommand) {
  return command
    .split('-')
    .map((part) => part[0].toUpperCase() + part.slice(1))
    .join(' ')
}
