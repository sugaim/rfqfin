import type { ReactElement } from 'react'
import type { CaseOperationResponse } from '@/generated/rfqApi'
import { BulkResultBar } from '@/shared/ui/BulkResultBar'

export type TraderResultState = {
  label: string
  items: CaseOperationResponse[]
}

interface TraderResultBarProps {
  result: TraderResultState
  expanded: boolean
  onToggle: () => void
}

export function TraderResultBar({
  result,
  expanded,
  onToggle,
}: TraderResultBarProps): ReactElement {
  return (
    <BulkResultBar
      label={result.label}
      items={result.items}
      expanded={expanded}
      onToggle={onToggle}
      toggleType="text"
    />
  )
}
