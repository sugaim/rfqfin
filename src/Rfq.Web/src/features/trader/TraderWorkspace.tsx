import { useEffect } from 'react'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '../../app/App'
import {
  useAssignTraderMutation,
  useBulkCloseAwayRfqsMutation,
  useCalculateWorkingQuoteMutation,
  useChangeContactOwnerMutation,
  useChangeWorkingQuoteModeMutation,
  useCloseAwayRfqMutation,
  useCloseHitRfqMutation,
  useConfirmQuoteMutation,
  useCorrectOutcomeToAwayMutation,
  useCorrectOutcomeToHitMutation,
  useGetActiveTraderRfqsQuery,
  useGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useGetQuoteExpiryQuery,
  usePickUpRfqMutation,
  useReleaseRfqMutation,
  useScratchPriceMutation,
  useTakeOverRfqMutation,
  useUpdateManualWorkingQuoteMutation,
  useUpdateTraderMemoMutation,
  useWithdrawQuoteMutation,
} from '../../services/api'
import { TraderScreen } from './TraderScreen'

type RfqOutcome = 'Hit' | 'Away'

export function TraderWorkspace() {
  const { currentUserId, refreshToken } = useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveTraderRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const quoteExpiryQuery = useGetQuoteExpiryQuery()
  const [pickUpRfq, pickUpState] = usePickUpRfqMutation()
  const [releaseRfq, releaseState] = useReleaseRfqMutation()
  const [assignTrader, assignState] = useAssignTraderMutation()
  const [takeOverRfq, takeOverState] = useTakeOverRfqMutation()
  const [calculateWorkingQuote, calculateState] = useCalculateWorkingQuoteMutation()
  const [changeWorkingQuoteMode, changeModeState] = useChangeWorkingQuoteModeMutation()
  const [updateManualWorkingQuote, updateManualState] = useUpdateManualWorkingQuoteMutation()
  const [confirmQuote, confirmQuoteState] = useConfirmQuoteMutation()
  const [closeHitRfq, closeHitState] = useCloseHitRfqMutation()
  const [closeAwayRfq, closeAwayState] = useCloseAwayRfqMutation()
  const [bulkCloseAwayRfqs, bulkCloseState] = useBulkCloseAwayRfqsMutation()
  const [correctOutcomeToHit, correctToHitState] = useCorrectOutcomeToHitMutation()
  const [correctOutcomeToAway, correctToAwayState] = useCorrectOutcomeToAwayMutation()
  const [changeContactOwner, changeContactOwnerState] = useChangeContactOwnerMutation()
  const [updateTraderMemo, updateTraderMemoState] = useUpdateTraderMemoMutation()
  const [withdrawQuote, withdrawState] = useWithdrawQuoteMutation()
  const [scratchPrice] = useScratchPriceMutation()

  useEffect(() => {
    if (refreshToken > 0) void rfqsQuery.refetch()
  }, [refreshToken, rfqsQuery.refetch])

  const mutationStates = [
    pickUpState, releaseState, assignState, takeOverState, calculateState,
    changeModeState, updateManualState, confirmQuoteState, closeHitState,
    closeAwayState, bulkCloseState, correctToHitState, correctToAwayState,
    changeContactOwnerState, updateTraderMemoState, withdrawState,
  ]
  const users = (usersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))
  const traders = (tradersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))
  const closeCase = (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) =>
    (outcome === 'Hit' ? closeHitRfq : closeAwayRfq)(
      { caseId, expectedCurrentVersion }).unwrap().then(() => undefined)
  const correctOutcome = (
    caseId: number,
    outcome: RfqOutcome,
    expectedCurrentVersion: number,
  ) => (outcome === 'Hit' ? correctOutcomeToHit : correctOutcomeToAway)(
    { caseId, expectedCurrentVersion }).unwrap().then(() => undefined)
  const handOffContactOwner = (
    caseId: number,
    targetUserId: string,
    expectedCurrentVersion: number,
  ) => changeContactOwner({
    caseId,
    targetUserId,
    expectedCurrentVersion,
    confirmed: true,
  }).unwrap().then(() => undefined)

  return (
    <TraderScreen
      rfqs={rfqsQuery.data ?? []}
      traders={traders}
      users={users}
      currentUserId={currentUserId}
      defaultExpiryMinutes={quoteExpiryQuery.data?.type === 'After'
        ? quoteExpiryQuery.data.minutes
        : null}
      isLoading={rfqsQuery.isLoading || rfqsQuery.isFetching}
      isError={rfqsQuery.isError}
      isMutating={mutationStates.some((state) => state.isLoading)}
      onPickUp={(caseId, expectedVersion, confirmed) =>
        pickUpRfq({ caseId, expectedVersion, confirmed }).unwrap().then(() => undefined)}
      onRelease={(caseId, expectedVersion) =>
        releaseRfq({ caseId, expectedVersion }).unwrap().then(() => undefined)}
      onAssign={(caseId, targetTraderId, expectedVersion) =>
        assignTrader({ caseId, targetTraderId, expectedVersion }).unwrap().then(() => undefined)}
      onTakeOver={(caseId, expectedVersion, confirmed) =>
        takeOverRfq({ caseId, expectedVersion, confirmed }).unwrap().then(() => undefined)}
      onCalculate={(row, driver, value, simpleYieldSlide) =>
        calculateWorkingQuote({
          caseId: row.caseId,
          driver,
          value,
          simpleYieldSlide,
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap().then(() => undefined)}
      onChangeMode={(row, mode) =>
        changeWorkingQuoteMode({
          caseId: row.caseId,
          mode,
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap().then(() => undefined)}
      onUpdateManual={(row, price, finalSimpleYield) =>
        updateManualWorkingQuote({
          caseId: row.caseId,
          price,
          finalSimpleYield,
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap().then(() => undefined)}
      onConfirmQuote={(row, expiryMinutes) =>
        confirmQuote({
          caseId: row.caseId,
          expiry: expiryMinutes === null
            ? { type: 'None', minutes: null }
            : { type: 'After', minutes: expiryMinutes },
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap().then(() => undefined)}
      onClose={closeCase}
      onBulkClose={(items) => bulkCloseAwayRfqs({ items }).unwrap()}
      onCorrectOutcome={correctOutcome}
      onChangeContactOwner={handOffContactOwner}
      onUpdateMemo={(caseId, memo, expectedVersion) =>
        updateTraderMemo({ caseId, memo, expectedVersion }).unwrap().then(() => undefined)}
      onWithdraw={(row) => withdrawQuote({
        caseId: row.caseId,
        expectedVersion: row.currentVersion,
      }).unwrap().then(() => undefined)}
      onScratchPrice={(input) => scratchPrice(input).unwrap()}
      onReload={rfqsQuery.refetch}
    />
  )
}
