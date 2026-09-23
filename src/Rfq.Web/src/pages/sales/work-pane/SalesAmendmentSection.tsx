import type { ReactElement } from 'react'
import { Button, Input, InputNumber, Space, Typography } from 'antd'
import type { SalesRfqResponse } from '@/generated/rfqApi'
import {
  hasAmendmentChanges,
  type SalesCommand,
} from '@/pages/sales/salesModel'
import { million, quoteValue } from '@/pages/sales/work-pane/workPaneFormatters'

interface SalesAmendmentSectionProps {
  row: SalesRfqResponse
  control: AmendmentEditorControl
  onCommand: (command: SalesCommand) => void
}

export interface AmendmentEditorControl {
  canStart: boolean
  onStart: () => void
  onEditStart: () => void
  onEditComplete: (
    field: 'notional' | 'settlementDate' | 'salesAndTradingMessage',
    value: number | string | null,
  ) => void
}

export function SalesAmendmentSection({
  row,
  control,
  onCommand,
}: SalesAmendmentSectionProps): ReactElement {
  const changes = row.draftRevisionId
    ? [
        row.draftNotional !== row.notional
          ? `Notl ${quoteValue(row.notional && row.notional / million, ' MM')} → ${quoteValue(row.draftNotional && row.draftNotional / million, ' MM')}`
          : null,
        row.draftSettlementDate !== row.settlementDate
          ? `Settle ${row.settlementDate} → ${row.draftSettlementDate}`
          : null,
        row.draftSalesAndTradingMessage !== row.salesAndTradingMessage
          ? 'Message changed'
          : null,
      ].filter((value): value is string => Boolean(value))
    : []
  if (!row.draftRevisionId)
    return (
      <div className="amendment-diff">
        <Button
          size="small"
          disabled={!control.canStart}
          onClick={control.onStart}
        >
          Start Amendment
        </Button>
      </div>
    )

  return (
    <div className="amendment-diff">
      <Typography.Text strong>Pending Amendment</Typography.Text>
      {changes.length ? (
        changes.map((value) => <div key={value}>{value}</div>)
      ) : (
        <Typography.Text type="secondary">No changes yet</Typography.Text>
      )}
      <label>
        Notl (MM)
        <InputNumber
          key={`notional-${row.draftVersion}`}
          defaultValue={(row.draftNotional ?? 0) / million}
          min={0}
          precision={2}
          onFocus={control.onEditStart}
          onBlur={(event) =>
            control.onEditComplete(
              'notional',
              event.target.value.trim() === ''
                ? null
                : Number(event.target.value) * million,
            )
          }
        />
      </label>
      <label>
        Settle
        <Input
          key={`settlement-${row.draftVersion}`}
          type="date"
          defaultValue={row.draftSettlementDate ?? ''}
          onFocus={control.onEditStart}
          onBlur={(event) =>
            control.onEditComplete('settlementDate', event.target.value)
          }
        />
      </label>
      <label>
        Message
        <Input
          key={`message-${row.draftVersion}`}
          defaultValue={row.draftSalesAndTradingMessage ?? ''}
          onFocus={control.onEditStart}
          onBlur={(event) =>
            control.onEditComplete('salesAndTradingMessage', event.target.value)
          }
        />
      </label>
      <Space>
        <Button
          size="small"
          type="primary"
          disabled={!hasAmendmentChanges(row)}
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
