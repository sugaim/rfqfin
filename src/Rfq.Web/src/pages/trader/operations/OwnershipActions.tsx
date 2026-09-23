import type { ReactElement } from 'react'
import { Button, Popconfirm, Select, Space, Typography } from 'antd'
import type { TraderRfq } from '@/services/api'
import { requiresPickUpConfirmation } from '@/pages/trader/traderModel'
import type { UserOption } from '@/pages/trader/traderContracts'
import type { OwnershipOperationIntents } from '@/pages/trader/operations/operationTypes'

interface OwnershipActionsProps {
  row: TraderRfq
  currentUserId: string
  traders: UserOption[]
  isMutating: boolean
  targetTraderId?: string
  onTargetTraderChange: (value?: string) => void
  intents: OwnershipOperationIntents
}

export function OwnershipActions({
  row,
  currentUserId,
  traders,
  isMutating,
  targetTraderId,
  onTargetTraderChange,
  intents,
}: OwnershipActionsProps): ReactElement {
  const open = ['Active', 'Presented'].includes(row.rfqStatus)
  const mine = row.assignedTraderId === currentUserId
  const needsConfirmation = requiresPickUpConfirmation(row, currentUserId)

  return (
    <>
      <Typography.Text type="secondary">Ownership / routing</Typography.Text>
      <Space wrap>
        <Popconfirm
          title={`Pick up Case ${row.caseId} assigned to another trader?`}
          disabled={!needsConfirmation}
          onConfirm={intents.pickUp}
        >
          <Button
            size="small"
            disabled={!open || row.owned || isMutating}
            onClick={() => {
              if (!needsConfirmation) intents.pickUp()
            }}
          >
            Pick Up
          </Button>
        </Popconfirm>
        <Button
          size="small"
          disabled={!open || !row.owned || !mine || isMutating}
          onClick={intents.release}
        >
          Release
        </Button>
        <Select
          size="small"
          aria-label="Assign to trader"
          placeholder="Assign"
          value={targetTraderId}
          onChange={onTargetTraderChange}
          options={traders.map((user) => ({
            value: user.userId,
            label: user.name,
          }))}
          disabled={!open || row.owned || isMutating}
        />
        <Button
          size="small"
          disabled={!targetTraderId || !open || row.owned || isMutating}
          onClick={() => targetTraderId && intents.assign(targetTraderId)}
        >
          Assign
        </Button>
        <Popconfirm
          title={`Take over Case ${row.caseId}?`}
          onConfirm={intents.takeOver}
        >
          <Button
            size="small"
            danger
            disabled={!open || !row.owned || mine || isMutating}
          >
            Take Over
          </Button>
        </Popconfirm>
      </Space>
    </>
  )
}
