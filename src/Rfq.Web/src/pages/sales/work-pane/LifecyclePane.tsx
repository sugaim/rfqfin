import type { ReactElement } from 'react'
import type { SalesRfqResponse } from '@/generated/rfqApi'
import type { SalesCommand } from '@/pages/sales/salesModel'
import type { ContactOwnerControl } from '@/pages/sales/work-pane/SalesContactOwnerSection'
import type { MemoEditorControl } from '@/pages/sales/work-pane/SalesMemoSection'
import { SalesRfqSummary } from '@/pages/sales/work-pane/SalesRfqSummary'
import { SalesAmendmentSection } from '@/pages/sales/work-pane/SalesAmendmentSection'
import type { AmendmentEditorControl } from '@/pages/sales/work-pane/SalesAmendmentSection'
import { SalesLifecycleActions } from '@/pages/sales/work-pane/SalesLifecycleActions'
import { SalesContactOwnerSection } from '@/pages/sales/work-pane/SalesContactOwnerSection'
import { SalesMemoSection } from '@/pages/sales/work-pane/SalesMemoSection'

interface LifecyclePaneProps {
  row: SalesRfqResponse
  mode: string
  now: number
  isMutating: boolean
  contactOwner: ContactOwnerControl
  memo: MemoEditorControl
  amendment: AmendmentEditorControl
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
  amendment,
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
      <SalesAmendmentSection
        row={row}
        control={amendment}
        onCommand={onCommand}
      />
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
