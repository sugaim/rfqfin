import type { ReactElement } from 'react'
import { Button, Select, Space, Typography } from 'antd'
import type { TraderRfq } from '@/services/api'
import { canEditQuote } from '@/pages/trader/traderModel'
import type { QuoteOperationIntents } from '@/pages/trader/operations/operationTypes'

interface QuoteActionsProps {
  row: TraderRfq
  currentUserId: string
  isMutating: boolean
  intents: QuoteOperationIntents
}

export function QuoteActions({
  row,
  currentUserId,
  isMutating,
  intents,
}: QuoteActionsProps): ReactElement {
  const mine = row.assignedTraderId === currentUserId
  const quoted = row.quoteStatus === 'Quoted'

  return (
    <>
      <Typography.Text type="secondary">Quote</Typography.Text>
      <Space wrap>
        <Select
          size="small"
          aria-label="Quote mode"
          value={row.workingQuoteMode}
          disabled={!canEditQuote(row, currentUserId) || isMutating}
          options={['Calculated', 'Manual'].map((value) => ({
            value,
            label: value,
          }))}
          onChange={intents.changeMode}
        />
        <Button
          size="small"
          disabled={
            !intents.withdraw ||
            row.rfqStatus === 'Presented' ||
            !quoted ||
            !mine ||
            !row.owned ||
            isMutating
          }
          onClick={intents.withdraw}
        >
          Withdraw
        </Button>
      </Space>
    </>
  )
}
