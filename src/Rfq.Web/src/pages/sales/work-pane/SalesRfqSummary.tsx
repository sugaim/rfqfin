import type { ReactElement } from 'react'
import { Descriptions, Space, Tag } from 'antd'
import type { SalesRfq } from '@/services/api'
import { displayState, elapsedLabel } from '@/pages/sales/salesModel'
import type { UserOption } from '@/pages/sales/salesContracts'
import {
  compactText,
  million,
  quoteValue,
} from '@/pages/sales/work-pane/workPaneFormatters'

interface SalesRfqSummaryProps {
  row: SalesRfq
  mode: string
  now: number
  users: UserOption[]
}

export function SalesRfqSummary({
  row,
  mode,
  now,
  users,
}: SalesRfqSummaryProps): ReactElement {
  const quote = row.confirmedQuote
  const showQuote = [
    'quoted',
    'presented',
    'hit',
    'away',
    'cancelled',
  ].includes(mode)

  return (
    <>
      <Descriptions
        size="small"
        column={1}
        colon={false}
        className="work-pane-facts"
        items={[
          {
            key: 'state',
            label: 'State',
            children: (
              <Space>
                <Tag>{displayState(row)}</Tag>
                {row.draftRevisionId && <Tag color="purple">AMEND</Tag>}
              </Space>
            ),
          },
          {
            key: 'notl',
            label: 'Notl',
            children:
              row.notional == null
                ? '—'
                : `${(row.notional / million).toLocaleString()} MM`,
          },
          {
            key: 'settle',
            label: 'Settle',
            children: row.settlementDate ?? '—',
          },
          { key: 'trader', label: 'Trader', children: row.assignedTraderId },
          {
            key: 'owner',
            label: 'Owner',
            children:
              users.find((user) => user.userId === row.contactOwnerId)?.name ??
              row.contactOwnerId,
          },
          {
            key: 'elapsed',
            label: 'Elapsed',
            children: `${elapsedLabel(row.stateSince, now)} · ${row.quoteRequestReason ?? 'current state'}`,
          },
          {
            key: 'message',
            label: 'Message',
            children: compactText(row.salesAndTradingMessage),
          },
        ]}
      />
      {showQuote && quote && (
        <div className="quote-summary">
          <div>
            <span>Price</span>
            <strong>{quoteValue(quote.price)}</strong>
          </div>
          <div>
            <span>Yield</span>
            <strong>{quoteValue(quote.bbgYield, '%')}</strong>
          </div>
          <div>
            <span>Simple</span>
            <strong>{quoteValue(quote.finalSimpleYield, '%')}</strong>
          </div>
          <div>
            <span>G-Spread</span>
            <strong>{quoteValue(quote.gSpread, ' bp')}</strong>
          </div>
        </div>
      )}
    </>
  )
}
