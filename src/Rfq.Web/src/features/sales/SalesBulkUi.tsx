import { Button, Empty, List, Space, Tag, Tooltip, Typography } from 'antd'
import type { BulkItemResult, SalesRfq } from '@/services/api'
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

export function BulkPane({
  rows,
  userId,
  onOpen,
}: {
  rows: SalesRfq[]
  userId: string
  onOpen: (command: SalesBulkCommand) => void
}) {
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

export function RowActions({
  row,
  userId,
  disabled,
  onCommand,
}: {
  row: SalesRfq
  userId: string
  disabled: boolean
  onCommand: (command: SalesRowCommand) => void
}) {
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

export function BulkResultBar({
  result,
  expanded,
  onToggle,
}: {
  result: SalesBulkResult
  expanded: boolean
  onToggle: () => void
}) {
  const succeeded = result.items.filter(
    (item) => item.status === 'Succeeded',
  ).length
  const skipped = result.items.filter(
    (item) => item.status === 'Skipped',
  ).length
  const failed = result.items.filter((item) => item.status === 'Failed').length
  const tone = failed ? 'error' : skipped ? 'warning' : 'success'
  const details = result.items.filter((item) => item.status !== 'Succeeded')
  return (
    <section
      className={`bulk-result-bar bulk-result-${tone}`}
      aria-label="Bulk result"
    >
      {expanded && (
        <div className="bulk-result-details">
          <table>
            <thead>
              <tr>
                <th>Case</th>
                <th>Result</th>
                <th>Code</th>
                <th>Message</th>
              </tr>
            </thead>
            <tbody>
              {(details.length ? details : result.items).map((item) => (
                <tr key={item.caseId}>
                  <td>{item.caseId}</td>
                  <td>{item.status}</td>
                  <td>{item.code}</td>
                  <td>{item.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="bulk-result-summary" role="status">
        <span>
          Bulk {bulkCommandLabel(result.command)}: {succeeded} ok / {skipped}{' '}
          skipped / {failed} failed
        </span>
        <Button size="small" type="link" onClick={onToggle}>
          {expanded ? 'Collapse' : 'Details'}
        </Button>
      </div>
    </section>
  )
}

function bulkCommandLabel(command: SalesBulkCommand) {
  return command
    .split('-')
    .map((part) => part[0].toUpperCase() + part.slice(1))
    .join(' ')
}
