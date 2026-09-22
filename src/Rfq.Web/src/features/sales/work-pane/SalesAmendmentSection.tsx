import type { ReactElement } from 'react'
import { Button, Space, Typography } from 'antd'
import type { SalesRfq } from '@/services/api'
import type { SalesCommand } from '@/features/sales/salesModel'
import {
  million,
  quoteValue,
} from '@/features/sales/work-pane/workPaneFormatters'

interface SalesAmendmentSectionProps {
  row: SalesRfq
  onCommand: (command: SalesCommand) => void
}

export function SalesAmendmentSection({
  row,
  onCommand,
}: SalesAmendmentSectionProps): ReactElement | null {
  const changes = row.draftRevisionId
    ? [
        row.draftNotional !== null &&
        row.draftNotional !== undefined &&
        row.draftNotional !== row.notional
          ? `Notl ${quoteValue(row.notional && row.notional / million, ' MM')} → ${quoteValue(row.draftNotional / million, ' MM')}`
          : null,
        row.draftSettlementDate &&
        row.draftSettlementDate !== row.settlementDate
          ? `Settle ${row.settlementDate} → ${row.draftSettlementDate}`
          : null,
        row.draftSalesAndTradingMessage !== null &&
        row.draftSalesAndTradingMessage !== undefined &&
        row.draftSalesAndTradingMessage !== row.salesAndTradingMessage
          ? 'Message changed'
          : null,
      ].filter((value): value is string => Boolean(value))
    : []
  if (!changes.length) return null

  return (
    <div className="amendment-diff">
      <Typography.Text strong>Pending Amendment</Typography.Text>
      {changes.map((value) => (
        <div key={value}>{value}</div>
      ))}
      <Space>
        <Button
          size="small"
          type="primary"
          onClick={() => onCommand('confirm-amendment')}
        >
          Confirm Amendment
        </Button>
        <Button size="small" onClick={() => onCommand('discard-amendment')}>
          Discard
        </Button>
      </Space>
    </div>
  )
}
