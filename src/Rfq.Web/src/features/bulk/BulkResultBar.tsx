import type { ReactElement } from 'react'
import { Button } from 'antd'
import type { BulkItemResult } from '@/services/api'

export interface BulkResultBarProps {
  label: string
  items: BulkItemResult[]
  expanded: boolean
  onToggle: () => void
  toggleType: 'link' | 'text'
  ariaLabel?: string
  summaryRole?: 'status'
}

export function BulkResultBar({
  label,
  items,
  expanded,
  onToggle,
  toggleType,
  ariaLabel,
  summaryRole,
}: BulkResultBarProps): ReactElement {
  const succeeded = items.filter((item) => item.status === 'Succeeded').length
  const skipped = items.filter((item) => item.status === 'Skipped').length
  const failed = items.filter((item) => item.status === 'Failed').length
  const tone = failed ? 'error' : skipped ? 'warning' : 'success'
  const detailRows = items.filter((item) => item.status !== 'Succeeded')

  return (
    <section
      className={`bulk-result-bar bulk-result-${tone}`}
      aria-label={ariaLabel}
    >
      {expanded && (
        <div className="bulk-result-details">
          <table>
            <thead>
              <tr>
                <th>Case</th>
                <th>Result</th>
                <th>Code</th>
                <th>Message</th>
              </tr>
            </thead>
            <tbody>
              {(detailRows.length ? detailRows : items).map((item) => (
                <tr key={item.caseId}>
                  <td>{item.caseId}</td>
                  <td>{item.status}</td>
                  <td>{item.code}</td>
                  <td>{item.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="bulk-result-summary" role={summaryRole}>
        <span>
          {label}: {succeeded} ok / {skipped} skipped / {failed} failed
        </span>
        <Button size="small" type={toggleType} onClick={onToggle}>
          {expanded ? 'Collapse' : 'Details'}
        </Button>
      </div>
    </section>
  )
}
