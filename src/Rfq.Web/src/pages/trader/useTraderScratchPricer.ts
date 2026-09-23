import { useState } from 'react'
import type {
  CalculatedQuoteResponse2,
  TraderRfqResponse,
} from '@/generated/rfqApi'
import {
  calculatedValue,
  sameSourceTerms,
  type PricerProvenance,
} from '@/pages/trader/traderModel'
import type { ScratchState } from '@/pages/trader/TraderPricerPane'
import type { TraderPricerActions } from '@/pages/trader/traderContracts'

interface UseTraderScratchPricerInput {
  rfqs: TraderRfqResponse[]
  selected?: TraderRfqResponse
  actions?: TraderPricerActions
  onApply: (
    row: TraderRfqResponse,
    price: number | null,
    finalSimpleYield: number | null,
  ) => Promise<void>
  onError: (message: string | null) => void
}

export interface TraderScratchPricerController {
  scratch: ScratchState
  result: CalculatedQuoteResponse2 | null
  provenance: PricerProvenance | null
  sourceRow?: TraderRfqResponse
  canApply: boolean
  busy: boolean
  setScratch: React.Dispatch<React.SetStateAction<ScratchState>>
  setIdentity: (
    key: 'securityId' | 'notional' | 'settlementDate',
    value: string | number | null,
  ) => void
  loadSelected: () => void
  calculate: () => Promise<void>
  apply: () => Promise<void>
  clear: () => void
}

const emptyScratch = (): ScratchState => ({
  securityId: '',
  notional: null,
  settlementDate: '',
  driver: 'Price',
  value: 100,
  slide: 0,
})

export function useTraderScratchPricer({
  rfqs,
  selected,
  actions,
  onApply,
  onError,
}: UseTraderScratchPricerInput): TraderScratchPricerController {
  const [scratch, setScratch] = useState<ScratchState>(emptyScratch)
  const [result, setResult] = useState<CalculatedQuoteResponse2 | null>(null)
  const [provenance, setProvenance] = useState<PricerProvenance | null>(null)
  const [busy, setBusy] = useState(false)
  const sourceRow = provenance
    ? rfqs.find((row) => row.caseId === provenance.sourceCaseId)
    : undefined
  const canApply = Boolean(
    sourceRow &&
    sourceRow.workingQuoteMode === 'Manual' &&
    sameSourceTerms(sourceRow, provenance) &&
    result,
  )

  const setIdentity: TraderScratchPricerController['setIdentity'] = (
    key,
    value,
  ) => {
    setScratch((current) => ({ ...current, [key]: value }))
    setProvenance(null)
  }

  const loadSelected = () => {
    if (!selected) return
    const driver = selected.calculated?.driver ?? 'Price'
    setScratch({
      securityId: selected.securityId,
      notional: selected.notional,
      settlementDate: selected.settlementDate ?? '',
      driver,
      value:
        calculatedValue(selected.calculated, driver) ??
        selected.manual?.price ??
        100,
      slide: selected.calculated?.simpleYieldSlide ?? 0,
    })
    setResult(null)
    setProvenance({
      sourceCaseId: selected.caseId,
      securityId: selected.securityId,
      notional: selected.notional,
      settlementDate: selected.settlementDate,
    })
  }

  const calculate = async () => {
    if (!actions) return
    setBusy(true)
    try {
      setResult(
        await actions.calculate({
          securityId: scratch.securityId,
          settlementDate: scratch.settlementDate,
          driver: scratch.driver,
          value: scratch.value,
          simpleYieldSlide: scratch.slide,
        }),
      )
    } catch {
      onError('Scratch calculation failed.')
    } finally {
      setBusy(false)
    }
  }

  const apply = async () => {
    if (!sourceRow || !result || !canApply) return
    await onApply(sourceRow, result.price, result.finalSimpleYield)
  }

  const clear = () => {
    setScratch(emptyScratch())
    setResult(null)
    setProvenance(null)
  }

  return {
    scratch,
    result,
    provenance,
    sourceRow,
    canApply,
    busy,
    setScratch,
    setIdentity,
    loadSelected,
    calculate,
    apply,
    clear,
  }
}
