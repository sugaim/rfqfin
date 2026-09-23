import type { ReactElement } from 'react'
import { Button, Input, Space, Tooltip, Typography } from 'antd'
import type { SalesRfqResponse } from '@/generated/rfqApi'
import { compactText } from '@/pages/sales/work-pane/workPaneFormatters'

export interface MemoEditorControl {
  memoEditing: boolean
  memoDraft: string
  onMemoEdit: () => void
  onMemoChange: (value: string) => void
  onMemoCancel: () => void
  onMemoSave: () => void
}

interface SalesMemoSectionProps {
  row: SalesRfqResponse
  isMutating: boolean
  control: MemoEditorControl
}

export function SalesMemoSection({
  row,
  isMutating,
  control,
}: SalesMemoSectionProps): ReactElement {
  return (
    <div className="memo-line">
      <Typography.Text type="secondary">Memo</Typography.Text>
      {control.memoEditing ? (
        <>
          <Input.TextArea
            aria-label="Sales-only Memo"
            size="small"
            autoSize={{ minRows: 2, maxRows: 4 }}
            value={control.memoDraft}
            onChange={(event) => control.onMemoChange(event.target.value)}
          />
          <Space>
            <Button
              size="small"
              type="primary"
              loading={isMutating}
              onClick={control.onMemoSave}
            >
              Save
            </Button>
            <Button size="small" onClick={control.onMemoCancel}>
              Cancel
            </Button>
          </Space>
        </>
      ) : (
        <>
          <Tooltip title={row.salesMemo}>
            <span className="memo-preview">
              {compactText(row.salesMemo, 'No memo')}
            </span>
          </Tooltip>
          <Button size="small" type="link" onClick={control.onMemoEdit}>
            Edit
          </Button>
        </>
      )}
    </div>
  )
}
