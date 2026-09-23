import type { ReactElement } from 'react'
import { Button } from 'antd'
import type { CaseOperationResponse } from '@/generated/rfqApi'

export interface BulkResultBarProps {
  label: string
  items: CaseOperationResponse[]
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
  const applied = items.filter((item) => item.status === 'Applied').length
  const noChange = items.filter((item) => item.status === 'NoChange').length
  const failed = items.filter((item) => item.status === 'Failed').length
  const tone = failed ? 'error' : noChange ? 'warning' : 'success'
  const detailRows = items.filter((item) => item.status !== 'Applied')

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
                  <td>{item.failureCode}</td>
                  <td>{item.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="bulk-result-summary" role={summaryRole}>
        <span>
          {label}: {applied} applied / {noChange} no change / {failed} failed
        </span>
        <Button size="small" type={toggleType} onClick={onToggle}>
          {expanded ? 'Collapse' : 'Details'}
        </Button>
      </div>
    </section>
  )
}
