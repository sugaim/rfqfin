import type { ReactElement } from 'react'
import { Button, Popconfirm, Space, Typography } from 'antd'
import type { TraderRfq } from '@/services/api'
import {
  isPickUpEligible,
  requiresPickUpConfirmation,
} from '@/pages/trader/traderModel'
import type { BulkOperationIntents } from '@/pages/trader/operations/operationTypes'

interface BulkActionsProps {
  rows: TraderRfq[]
  currentUserId: string
  isMutating: boolean
  targetTraderId?: string
  intents: BulkOperationIntents
}

export function BulkActions({
  rows,
  currentUserId,
  isMutating,
  targetTraderId,
  intents,
}: BulkActionsProps): ReactElement {
  const pickRows = rows.filter(isPickUpEligible)
  const otherAssignedPickCount = pickRows.filter((row) =>
    requiresPickUpConfirmation(row, currentUserId),
  ).length

  return (
    <>
      <Typography.Text type="secondary">
        Selected ({rows.length})
      </Typography.Text>
      <Space wrap>
        <Popconfirm
          title={`Pick up ${pickRows.length} selected RFQs, including ${otherAssignedPickCount} assigned to another trader?`}
          disabled={otherAssignedPickCount === 0}
          onConfirm={intents.pick}
        >
          <Button
            size="small"
            disabled={!pickRows.length || isMutating}
            onClick={() => {
              if (otherAssignedPickCount === 0) intents.pick()
            }}
          >
            Pick
          </Button>
        </Popconfirm>
        <Button size="small" onClick={intents.release}>
          Release
        </Button>
        <Button
          size="small"
          disabled={!targetTraderId}
          onClick={() => targetTraderId && intents.assign(targetTraderId)}
        >
          Assign
        </Button>
        <Button size="small" onClick={intents.withdraw}>
          Withdraw
        </Button>
        <Popconfirm
          title={`Away ${rows.length} selected Cases?`}
          onConfirm={intents.closeAway}
        >
          <Button size="small">Away</Button>
        </Popconfirm>
        <Popconfirm
          title={`Cancel ${rows.length} selected Cases?`}
          onConfirm={intents.cancel}
        >
          <Button size="small" danger>
            Cancel
          </Button>
        </Popconfirm>
      </Space>
    </>
  )
}
