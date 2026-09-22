import { useRef, useState } from 'react'
import type { InputRef } from 'antd'
import type { RfqSearchParams, RfqSearchResult } from '@/services/api'
import {
  searchDateRange,
  type SearchDatePreset,
} from '@/features/trader/traderModel'
import type { TraderSearchActions } from '@/features/trader/traderContracts'

interface UseTraderSearchInput {
  businessDate: string
  actions: TraderSearchActions
  onError: (message: string | null) => void
}

export interface TraderSearchController {
  open: boolean
  filtersOpen: boolean
  preset: SearchDatePreset
  filters: Record<string, string>
  result: RfqSearchResult
  searching: boolean
  caseInputRef: React.RefObject<InputRef | null>
  toggle: () => void
  openAndFocus: () => void
  toggleFilters: () => void
  setPreset: (preset: SearchDatePreset) => void
  setFilters: (filters: Record<string, string>) => void
  execute: () => Promise<void>
}

export function useTraderSearch({
  businessDate,
  actions,
  onError,
}: UseTraderSearchInput): TraderSearchController {
  const [open, setOpen] = useState(true)
  const [filtersOpen, setFiltersOpen] = useState(true)
  const [preset, setPreset] = useState<SearchDatePreset>('1Y')
  const [filters, setFilters] = useState<Record<string, string>>({})
  const [result, setResult] = useState<RfqSearchResult>({
    items: [],
    requiresNarrowing: false,
  })
  const [searching, setSearching] = useState(false)
  const caseInputRef = useRef<InputRef>(null)

  const execute = async () => {
    setSearching(true)
    onError(null)
    try {
      const params: RfqSearchParams = searchDateRange(businessDate, preset)
      for (const [key, value] of Object.entries(filters)) {
        if (!value.trim()) continue
        if (key === 'caseId') params.caseId = Number(value)
        else (params as Record<string, unknown>)[key] = value.trim()
      }
      setResult(await actions.execute(params))
    } catch {
      onError('RFQ Search could not be completed.')
    } finally {
      setSearching(false)
    }
  }

  const openAndFocus = () => {
    setOpen(true)
    window.setTimeout(() => caseInputRef.current?.focus(), 0)
  }

  return {
    open,
    filtersOpen,
    preset,
    filters,
    result,
    searching,
    caseInputRef,
    toggle: () => setOpen((value) => !value),
    openAndFocus,
    toggleFilters: () => setFiltersOpen((value) => !value),
    setPreset,
    setFilters,
    execute,
  }
}
