import { useEffect, useRef, useState } from 'react'
import type { CellEditRequestEvent } from 'ag-grid-community'
import type {
  ApiProblemDetails,
  CalculatedQuoteResponse2,
  TraderRfqResponse,
  WorkingQuoteResponse,
} from '@/generated/rfqApi'
import {
  calculatedValue,
  canEditQuote,
  type CalcState,
} from '@/pages/trader/traderModel'

const calculationTimeoutMs = 10_000

const calculationDrivers: Record<string, CalculatedQuoteResponse2['driver']> = {
  price: 'Price',
  bbgYield: 'BbgYield',
  baseSimpleYield: 'SimpleYield',
  ysc: 'Ysc',
  gSpread: 'GSpread',
  asw: 'Asw',
  iSpread: 'ISpread',
  zSpread: 'ZSpread',
}

type CalculateWorkingQuote = (
  row: TraderRfqResponse,
  driver: CalculatedQuoteResponse2['driver'],
  value: number,
  simpleYieldSlide: number,
) => Promise<WorkingQuoteResponse | void>

type UpdateManualQuote = (
  row: TraderRfqResponse,
  price: number | null,
  finalSimpleYield: number | null,
) => Promise<WorkingQuoteResponse | void>

export interface UseWorkingQuoteCalculationOptions {
  currentUserId: string
  refreshGeneration: number
  onCalculate: CalculateWorkingQuote
  onUpdateManual: UpdateManualQuote
  onSuccess: (caseId: number) => Promise<void>
}

export interface WorkingQuoteCalculationController {
  calcStates: Record<number, CalcState>
  calculating: boolean
  editQuote: (event: CellEditRequestEvent<TraderRfqResponse>) => Promise<void>
  invalidate: () => void
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(
      () => reject(new Error('Calculation timed out.')),
      timeoutMs,
    )

    promise.then(
      (value) => {
        window.clearTimeout(timer)
        resolve(value)
      },
      (error) => {
        window.clearTimeout(timer)
        reject(error)
      },
    )
  })
}

function failureFrom(error: unknown, requestId: number): CalcState {
  const problem = error as Partial<ApiProblemDetails>
  const message = error instanceof Error ? error.message : undefined

  return {
    status: 'failed',
    requestId,
    code: problem.calculationErrorCode ?? problem.code ?? undefined,
    message: problem.detail ?? message ?? 'Calculation failed.',
    traceId: problem.traceId ?? undefined,
    failureLogId: problem.failureLogId ?? undefined,
  }
}

export function useWorkingQuoteCalculation({
  currentUserId,
  refreshGeneration,
  onCalculate,
  onUpdateManual,
  onSuccess,
}: UseWorkingQuoteCalculationOptions): WorkingQuoteCalculationController {
  const [calcStates, setCalcStates] = useState<Record<number, CalcState>>({})
  const requestSequence = useRef(0)
  const requestByCase = useRef<Record<number, number>>({})
  const inFlightCases = useRef(new Set<number>())
  const generationRef = useRef(refreshGeneration)

  const invalidate = () => {
    generationRef.current += 1
    requestSequence.current += 1
    requestByCase.current = {}
    inFlightCases.current.clear()
    setCalcStates({})
  }

  useEffect(() => {
    generationRef.current = refreshGeneration
    requestSequence.current += 1
    requestByCase.current = {}
    inFlightCases.current.clear()
    setCalcStates({})
  }, [refreshGeneration])

  const editQuote = async (
    event: CellEditRequestEvent<TraderRfqResponse>,
  ): Promise<void> => {
    const row = event.data
    const column = event.column.getColId()

    if (
      !canEditQuote(row, currentUserId) ||
      inFlightCases.current.has(row.caseId)
    ) {
      event.api.refreshCells({ rowNodes: [event.node], force: true })

      return
    }

    const value = Number(event.newValue)
    if (!Number.isFinite(value)) {
      event.api.refreshCells({ rowNodes: [event.node], force: true })

      return
    }

    const requestId = ++requestSequence.current
    requestByCase.current[row.caseId] = requestId
    inFlightCases.current.add(row.caseId)
    const generation = generationRef.current

    setCalcStates((state) => ({
      ...state,
      [row.caseId]: { status: 'calculating', requestId },
    }))

    try {
      await (row.workingQuoteMode === 'Manual'
        ? updateManualQuote(row, column, value, onUpdateManual)
        : updateCalculatedQuote(row, column, value, onCalculate))

      // A refresh or newer edit now owns this Case, so this response is obsolete.
      if (
        requestByCase.current[row.caseId] !== requestId ||
        generationRef.current !== generation
      )
        return

      inFlightCases.current.delete(row.caseId)
      setCalcStates((state) => {
        const next = { ...state }
        delete next[row.caseId]

        return next
      })
    } catch (error) {
      if (
        requestByCase.current[row.caseId] !== requestId ||
        generationRef.current !== generation
      )
        return

      inFlightCases.current.delete(row.caseId)
      setCalcStates((state) => ({
        ...state,
        [row.caseId]: failureFrom(error, requestId),
      }))
      event.api.refreshCells({ rowNodes: [event.node], force: true })

      return
    }
    await Promise.resolve(onSuccess(row.caseId)).catch(() => undefined)
  }

  return {
    calcStates,
    calculating: Object.values(calcStates).some(
      (state) => state.status === 'calculating',
    ),
    editQuote,
    invalidate,
  }
}

async function updateManualQuote(
  row: TraderRfqResponse,
  column: string,
  value: number,
  onUpdateManual: UpdateManualQuote,
): Promise<WorkingQuoteResponse | void> {
  return withTimeout(
    onUpdateManual(
      row,
      column === 'price' ? value : (row.manual?.price ?? null),
      column === 'finalSimpleYield'
        ? value
        : (row.manual?.finalSimpleYield ?? null),
    ),
    calculationTimeoutMs,
  )
}

async function updateCalculatedQuote(
  row: TraderRfqResponse,
  column: string,
  value: number,
  onCalculate: CalculateWorkingQuote,
): Promise<WorkingQuoteResponse | void> {
  const driver =
    column === 'simpleYieldSlide'
      ? row.calculated?.driver
      : calculationDrivers[column]
  const driverValue =
    column === 'simpleYieldSlide'
      ? row.calculated && calculatedValue(row.calculated, row.calculated.driver)
      : value

  if (!driver || driverValue == null)
    throw new Error('A calculation driver is required.')

  return withTimeout(
    onCalculate(
      row,
      driver,
      driverValue,
      column === 'simpleYieldSlide'
        ? value
        : (row.calculated?.simpleYieldSlide ?? 0),
    ),
    calculationTimeoutMs,
  )
}
