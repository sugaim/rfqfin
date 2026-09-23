import type { ReactElement } from 'react'
import { Tag, Typography } from 'antd'
import type { SalesRfq } from '@/services/api'

interface WorkPaneHeaderProps {
  mode: string
  row?: SalesRfq
}

export function WorkPaneHeader({
  mode,
  row,
}: WorkPaneHeaderProps): ReactElement {
  const title =
    mode === 'new' ? 'New RFQ' : row ? `Case ${row.caseId}` : 'Work Pane'

  return (
    <div className="work-pane-header">
      <div>
        <Typography.Text strong>{title}</Typography.Text>
        {row && (
          <div className="work-pane-security">
            {row.clientName} · {row.securityJapaneseName}
          </div>
        )}
      </div>
      <Tag>{mode.toUpperCase()}</Tag>
    </div>
  )
}
