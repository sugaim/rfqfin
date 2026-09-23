import type { ReactElement } from 'react'
import { Button, Popconfirm, Select, Space, Typography } from 'antd'
import type { SalesRfqResponse } from '@/generated/rfqApi'
import type { UserOption } from '@/pages/sales/salesContracts'

export interface ContactOwnerControl {
  users: UserOption[]
  currentUserId: string
  targetContactOwnerId?: string
  onTargetContactOwnerChange: (value: string) => void
  onChangeContactOwner: () => void
}

interface SalesContactOwnerSectionProps {
  row: SalesRfqResponse
  isMutating: boolean
  control: ContactOwnerControl
}

export function SalesContactOwnerSection({
  row,
  isMutating,
  control,
}: SalesContactOwnerSectionProps): ReactElement | null {
  if (row.contactOwnerId !== control.currentUserId) return null

  return (
    <div className="owner-handoff">
      <Typography.Text type="secondary">Contact Owner handoff</Typography.Text>
      <Space.Compact block>
        <Select
          size="small"
          aria-label="Contact Owner"
          value={control.targetContactOwnerId}
          onChange={control.onTargetContactOwnerChange}
          options={control.users
            .filter((user) => user.userId !== row.contactOwnerId)
            .map((user) => ({ value: user.userId, label: user.name }))}
        />
        <Popconfirm
          title={
            control.targetContactOwnerId
              ? `Hand off Case ${row.caseId} to ${control.targetContactOwnerId}?`
              : 'Select a Contact Owner.'
          }
          disabled={!control.targetContactOwnerId}
          onConfirm={control.onChangeContactOwner}
        >
          <Button
            size="small"
            disabled={!control.targetContactOwnerId || isMutating}
          >
            Change
          </Button>
        </Popconfirm>
      </Space.Compact>
    </div>
  )
}
