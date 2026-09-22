import type { ReactElement } from 'react'
import type { SalesRfq } from '@/services/api'
import type { SalesCommand } from '@/features/sales/salesModel'
import type { ContactOwnerControl } from '@/features/sales/work-pane/SalesContactOwnerSection'
import type { MemoEditorControl } from '@/features/sales/work-pane/SalesMemoSection'
import { SalesRfqSummary } from '@/features/sales/work-pane/SalesRfqSummary'
import { SalesAmendmentSection } from '@/features/sales/work-pane/SalesAmendmentSection'
import { SalesLifecycleActions } from '@/features/sales/work-pane/SalesLifecycleActions'
import { SalesContactOwnerSection } from '@/features/sales/work-pane/SalesContactOwnerSection'
import { SalesMemoSection } from '@/features/sales/work-pane/SalesMemoSection'

interface LifecyclePaneProps {
  row: SalesRfq
  mode: string
  now: number
  isMutating: boolean
  contactOwner: ContactOwnerControl
  memo: MemoEditorControl
  onCorrectOutcome: () => void
  onCommand: (command: SalesCommand) => void
}

export function LifecyclePane({
  row,
  mode,
  now,
  isMutating,
  contactOwner,
  memo,
  onCorrectOutcome,
  onCommand,
}: LifecyclePaneProps): ReactElement {
  return (
    <>
      <SalesRfqSummary
        row={row}
        mode={mode}
        now={now}
        users={contactOwner.users}
      />
      <SalesAmendmentSection row={row} onCommand={onCommand} />
      <SalesLifecycleActions
        mode={mode}
        onCorrectOutcome={onCorrectOutcome}
        onCommand={onCommand}
      />
      <SalesContactOwnerSection
        row={row}
        isMutating={isMutating}
        control={contactOwner}
      />
      <SalesMemoSection row={row} isMutating={isMutating} control={memo} />
    </>
  )
}
