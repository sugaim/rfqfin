import { Button } from 'antd'
import type { BulkItemResult } from '@/services/api'

export type TraderResultState = {
  label: string
  items: BulkItemResult[]
}

export function TraderResultBar({
  result,
  expanded,
  onToggle,
}: {
  result: TraderResultState
  expanded: boolean
  onToggle: () => void
}) {
  const ok = result.items.filter((item) => item.status === 'Succeeded').length
  const skipped = result.items.filter(
    (item) => item.status === 'Skipped',
  ).length
  const failed = result.items.filter((item) => item.status === 'Failed').length
  const detailRows = result.items.filter((item) => item.status !== 'Succeeded')
  return (
    <section
      className={`bulk-result-bar ${failed ? 'bulk-result-error' : skipped ? 'bulk-result-warning' : 'bulk-result-success'}`}
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
              {(detailRows.length ? detailRows : result.items).map((item) => (
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
      <div className="bulk-result-summary">
        <span>
          {result.label}: {ok} ok / {skipped} skipped / {failed} failed
        </span>
        <Button type="text" size="small" onClick={onToggle}>
          {expanded ? 'Collapse' : 'Details'}
        </Button>
      </div>
    </section>
  )
}
