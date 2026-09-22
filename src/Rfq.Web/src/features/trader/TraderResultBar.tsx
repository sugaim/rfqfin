import type { ReactElement } from 'react'
import type { BulkItemResult } from '@/services/api'
import { BulkResultBar } from '@/features/bulk/BulkResultBar'

export type TraderResultState = {
  label: string
  items: BulkItemResult[]
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
