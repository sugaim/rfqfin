import type { ReactElement } from 'react'
import { Button } from 'antd'
import type { SalesCommand } from '@/pages/sales/salesModel'

interface SalesLifecycleActionsProps {
  mode: string
  onCorrectOutcome: () => void
  onCommand: (command: SalesCommand) => void
}

export function SalesLifecycleActions({
  mode,
  onCorrectOutcome,
  onCommand,
}: SalesLifecycleActionsProps): ReactElement {
  return (
    <div className="work-pane-actions">
      {mode === 'waiting' && (
        <Button size="small" danger onClick={() => onCommand('cancel')}>
          Cancel
        </Button>
      )}
      {mode === 'quoted' && (
        <>
          <Button
            size="small"
            type="primary"
            onClick={() => onCommand('present')}
          >
            Present
          </Button>
          <Button size="small" onClick={() => onCommand('hit')}>
            Hit
          </Button>
          <Button size="small" onClick={() => onCommand('away')}>
            Away
          </Button>
          <Button size="small" danger onClick={() => onCommand('cancel')}>
            Cancel
          </Button>
        </>
      )}
      {mode === 'presented' && (
        <>
          <Button size="small" type="primary" onClick={() => onCommand('hit')}>
            Hit
          </Button>
          <Button size="small" type="primary" onClick={() => onCommand('away')}>
            Away
          </Button>
          <Button size="small" onClick={() => onCommand('unpresent')}>
            Unpresent
          </Button>
          <Button size="small" danger onClick={() => onCommand('cancel')}>
            Cancel
          </Button>
        </>
      )}
      {mode === 'cancelled' && (
        <Button size="small" type="primary" onClick={() => onCommand('reopen')}>
          Reopen
        </Button>
      )}
      {(mode === 'hit' || mode === 'away') && (
        <Button size="small" type="text" onClick={onCorrectOutcome}>
          Correct outcome
        </Button>
      )}
      <Button
        size="small"
        type="text"
        onClick={() => onCommand('create-from-existing')}
      >
        Create New from Existing
      </Button>
    </div>
  )
}
