import type { ReactElement } from 'react'
import { Button, Popconfirm, Select, Space, Typography } from 'antd'
import type { TraderRfqResponse } from '@/generated/rfqApi'
import type { UserOption } from '@/pages/trader/traderContracts'
import type {
  ContactOwnerOperationIntents,
  LifecycleOperationIntents,
} from '@/pages/trader/operations/operationTypes'

interface ContactOwnerActionsProps {
  row: TraderRfqResponse
  users: UserOption[]
  targetContactOwnerId?: string
  onTargetContactOwnerChange: (value?: string) => void
  lifecycle: LifecycleOperationIntents
  contactOwner: ContactOwnerOperationIntents
}

export function ContactOwnerActions({
  row,
  users,
  targetContactOwnerId,
  onTargetContactOwnerChange,
  lifecycle,
  contactOwner,
}: ContactOwnerActionsProps): ReactElement {
  const open = ['Active', 'Presented'].includes(row.rfqStatus)
  const quoted = row.quoteStatus === 'Quoted'

  return (
    <>
      <Typography.Text type="secondary">
        Contact Owner lifecycle
      </Typography.Text>
      <Space wrap>
        <Button
          size="small"
          disabled={!lifecycle.present || row.rfqStatus !== 'Active' || !quoted}
          onClick={lifecycle.present}
        >
          Present
        </Button>
        <Button
          size="small"
          disabled={!lifecycle.unpresent || row.rfqStatus !== 'Presented'}
          onClick={lifecycle.unpresent}
        >
          Unpresent
        </Button>
        {(['Hit', 'Away'] as const).map((outcome) => (
          <Popconfirm
            key={outcome}
            title={`${outcome} Case ${row.caseId}?`}
            onConfirm={() => lifecycle.close(outcome)}
          >
            <Button size="small" disabled={!open || !quoted}>
              {outcome}
            </Button>
          </Popconfirm>
        ))}
        <Popconfirm
          title={`Cancel Case ${row.caseId}?`}
          onConfirm={lifecycle.cancel}
        >
          <Button size="small" danger disabled={!lifecycle.cancel || !open}>
            Cancel
          </Button>
        </Popconfirm>
        <Button
          size="small"
          disabled={!lifecycle.reopen || row.rfqStatus !== 'Cancelled'}
          onClick={lifecycle.reopen}
        >
          Reopen
        </Button>
        <Popconfirm
          title="Correct closed outcome?"
          onConfirm={() => {
            const reason = window.prompt('Correction Reason')
            if (!reason?.trim()) return
            lifecycle.correctOutcome(
              row.rfqStatus === 'Hit' ? 'Away' : 'Hit',
              reason.trim(),
            )
          }}
        >
          <Button
            size="small"
            disabled={!['Hit', 'Away'].includes(row.rfqStatus)}
          >
            Correct
          </Button>
        </Popconfirm>
      </Space>
      <Space.Compact block>
        <Select
          size="small"
          aria-label="Contact Owner"
          placeholder="Contact Owner"
          value={targetContactOwnerId}
          onChange={onTargetContactOwnerChange}
          options={users
            .filter((user) => user.userId !== row.contactOwnerId)
            .map((user) => ({ value: user.userId, label: user.name }))}
        />
        <Button
          size="small"
          disabled={!targetContactOwnerId}
          onClick={() =>
            targetContactOwnerId && contactOwner.change(targetContactOwnerId)
          }
        >
          Change
        </Button>
      </Space.Compact>
    </>
  )
}
