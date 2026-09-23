import { useState } from 'react'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'
import {
  useAssignTraderMutation,
  useBulkAssignTraderMutation,
  useBulkCancelRfqsMutation,
  useBulkCloseAwayRfqsMutation,
  useBulkConfirmQuotesMutation,
  useBulkPickUpRfqsMutation,
  useBulkReleaseRfqsMutation,
  useBulkWithdrawQuotesMutation,
  useCalculateWorkingQuoteMutation,
  useCancelRfqMutation,
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
  useGetGridConfigQuery,
  useGetQuoteExpiryQuery,
  useLazySearchRfqsQuery,
  usePickUpRfqMutation,
  usePresentQuoteMutation,
  useReleaseRfqMutation,
  useReopenRfqMutation,
  useSaveGridConfigMutation,
  useScratchPriceMutation,
  useTakeOverRfqMutation,
  useUnpresentQuoteMutation,
  useUpdateManualWorkingQuoteMutation,
  useUpdateTraderMemoMutation,
  useWithdrawQuoteMutation,
  type BulkItemResult,
  type QuoteExpiry,
  type RfqSearchParams,
  type TraderRfq,
} from '@/services/api'
import { TraderScreen } from '@/pages/trader/TraderScreen'
import type { TraderBulkCommand } from '@/pages/trader/traderModel'
import { requiresPickUpConfirmation } from '@/pages/trader/traderModel'
import type {
  TraderBulkActions,
  TraderContactOwnerActions,
  TraderGridLayoutActions,
  TraderLifecycleActions,
  TraderMemoActions,
  TraderOwnershipActions,
  TraderPricerActions,
  TraderSearchActions,
  TraderWorkingQuoteActions,
} from '@/pages/trader/traderContracts'
import { useLivePausedRows } from '@/shared/state/useLivePausedRows'

export function TraderWorkspace() {
  const { currentUserId, businessDate, traderChangeVersion } =
    useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveTraderRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const quoteExpiryQuery = useGetQuoteExpiryQuery()
  const mainConfigQuery = useGetGridConfigQuery({
    screenId: 'trader',
    configKey: 'main',
  })
  const searchConfigQuery = useGetGridConfigQuery({
    screenId: 'trader',
    configKey: 'search',
  })
  const confirmConfigQuery = useGetGridConfigQuery({
    screenId: 'trader',
    configKey: 'confirm',
  })
  const [searchRfqs] = useLazySearchRfqsQuery()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const [pickUp, pickUpState] = usePickUpRfqMutation()
  const [release, releaseState] = useReleaseRfqMutation()
  const [assign, assignState] = useAssignTraderMutation()
  const [takeOver, takeOverState] = useTakeOverRfqMutation()
  const [calculate] = useCalculateWorkingQuoteMutation()
  const [changeMode, changeModeState] = useChangeWorkingQuoteModeMutation()
  const [updateManual] = useUpdateManualWorkingQuoteMutation()
  const [confirmQuote, confirmState] = useConfirmQuoteMutation()
  const [present, presentState] = usePresentQuoteMutation()
  const [unpresent, unpresentState] = useUnpresentQuoteMutation()
  const [withdraw, withdrawState] = useWithdrawQuoteMutation()
  const [closeHit, closeHitState] = useCloseHitRfqMutation()
  const [closeAway, closeAwayState] = useCloseAwayRfqMutation()
  const [cancel, cancelState] = useCancelRfqMutation()
  const [reopen, reopenState] = useReopenRfqMutation()
  const [correctHit, correctHitState] = useCorrectOutcomeToHitMutation()
  const [correctAway, correctAwayState] = useCorrectOutcomeToAwayMutation()
  const [changeOwner, changeOwnerState] = useChangeContactOwnerMutation()
  const [updateMemo, updateMemoState] = useUpdateTraderMemoMutation()
  const [scratch] = useScratchPriceMutation()
  const [bulkPick, bulkPickState] = useBulkPickUpRfqsMutation()
  const [bulkRelease, bulkReleaseState] = useBulkReleaseRfqsMutation()
  const [bulkAssign, bulkAssignState] = useBulkAssignTraderMutation()
  const [bulkConfirm, bulkConfirmState] = useBulkConfirmQuotesMutation()
  const [bulkWithdraw, bulkWithdrawState] = useBulkWithdrawQuotesMutation()
  const [bulkAway, bulkAwayState] = useBulkCloseAwayRfqsMutation()
  const [bulkCancel, bulkCancelState] = useBulkCancelRfqsMutation()

  const [protectedState, setProtectedState] = useState(false)
  const [refreshGeneration, setRefreshGeneration] = useState(0)
  const refresh = useLivePausedRows({
    authoritativeRows: rfqsQuery.data,
    remoteChangeVersion: traderChangeVersion,
    protectedState,
    refetch: rfqsQuery.refetch,
    keyOf: (row: TraderRfq) => row.caseId,
    onAuthoritativeRefresh: () => setRefreshGeneration((value) => value + 1),
  })

  const lifecycleItems = (rows: TraderRfq[]) =>
    rows.map((row) => ({
      caseId: row.caseId,
      expectedCurrentVersion: row.currentVersion,
    }))
  const ownershipItems = (rows: TraderRfq[]) =>
    rows.map((row) => ({
      caseId: row.caseId,
      expectedVersion: row.currentVersion,
    }))
  const expiryFor = (minutes: number | null): QuoteExpiry =>
    minutes === null
      ? { type: 'None', minutes: null }
      : { type: 'After', minutes }

  const runBulk = async (
    command: TraderBulkCommand,
    rows: TraderRfq[],
    expiryMinutes: number | null,
    targetTraderId?: string,
  ): Promise<BulkItemResult[]> => {
    switch (command) {
      case 'pick':
        return bulkPick({
          items: rows.map((row) => ({
            caseId: row.caseId,
            expectedVersion: row.currentVersion,
            confirmed: requiresPickUpConfirmation(row, currentUserId),
          })),
        }).unwrap()
      case 'release':
        return bulkRelease({ items: ownershipItems(rows) }).unwrap()
      case 'assign':
        return bulkAssign({
          targetAssignedTraderId: targetTraderId!,
          items: ownershipItems(rows),
        }).unwrap()
      case 'confirm':
        return bulkConfirm({
          items: rows.map((row) => ({
            caseId: row.caseId,
            expiry: expiryFor(expiryMinutes),
            expectedCurrentVersion: row.currentVersion,
            expectedWorkingQuoteVersion: row.workingQuoteVersion,
          })),
        }).unwrap()
      case 'withdraw':
        return bulkWithdraw({ items: lifecycleItems(rows) }).unwrap()
      case 'away':
        return bulkAway({ items: lifecycleItems(rows) }).unwrap()
      case 'cancel':
        return bulkCancel({ items: lifecycleItems(rows) }).unwrap()
    }
  }

  const configJson = (value: unknown) =>
    value ? JSON.stringify(value) : undefined
  const saveConfig = (key: 'main' | 'search' | 'confirm', json: string) => {
    const query =
      key === 'main'
        ? mainConfigQuery
        : key === 'search'
          ? searchConfigQuery
          : confirmConfigQuery

    return saveGridConfig({
      screenId: 'trader',
      configKey: key,
      version: (query.data?.version ?? 0) + 1,
      config: JSON.parse(json),
    })
      .unwrap()
      .then(() => undefined)
  }

  const mutationStates = [
    pickUpState,
    releaseState,
    assignState,
    takeOverState,
    changeModeState,
    confirmState,
    presentState,
    unpresentState,
    withdrawState,
    closeHitState,
    closeAwayState,
    cancelState,
    reopenState,
    correctHitState,
    correctAwayState,
    changeOwnerState,
    updateMemoState,
    bulkPickState,
    bulkReleaseState,
    bulkAssignState,
    bulkConfirmState,
    bulkWithdrawState,
    bulkAwayState,
    bulkCancelState,
  ]
  const ownership: TraderOwnershipActions = {
    pickUp: (row, confirmed) =>
      pickUp({
        caseId: row.caseId,
        expectedVersion: row.currentVersion,
        confirmed,
      }).unwrap(),
    release: (row) =>
      release({
        caseId: row.caseId,
        expectedVersion: row.currentVersion,
      }).unwrap(),
    assign: (row, targetTraderId) =>
      assign({
        caseId: row.caseId,
        targetTraderId,
        expectedVersion: row.currentVersion,
      }).unwrap(),
    takeOver: (row) =>
      takeOver({
        caseId: row.caseId,
        expectedVersion: row.currentVersion,
        confirmed: true,
      }).unwrap(),
  }
  const workingQuote: TraderWorkingQuoteActions = {
    calculate: (row, driver, value, slide) =>
      calculate({
        caseId: row.caseId,
        driver,
        value,
        simpleYieldSlide: slide,
        expectedCurrentVersion: row.currentVersion,
        expectedWorkingQuoteVersion: row.workingQuoteVersion,
      }).unwrap(),
    changeMode: (row, mode) =>
      changeMode({
        caseId: row.caseId,
        mode,
        expectedCurrentVersion: row.currentVersion,
        expectedWorkingQuoteVersion: row.workingQuoteVersion,
      }).unwrap(),
    updateManual: (row, price, finalSimpleYield) =>
      updateManual({
        caseId: row.caseId,
        price,
        finalSimpleYield,
        expectedCurrentVersion: row.currentVersion,
        expectedWorkingQuoteVersion: row.workingQuoteVersion,
      }).unwrap(),
    confirm: (row, expiryMinutes) =>
      confirmQuote({
        caseId: row.caseId,
        expiry: expiryFor(expiryMinutes),
        expectedCurrentVersion: row.currentVersion,
        expectedWorkingQuoteVersion: row.workingQuoteVersion,
      }).unwrap(),
  }
  const lifecycle: TraderLifecycleActions = {
    present: (row) =>
      present({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap(),
    unpresent: (row) =>
      unpresent({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap(),
    withdraw: (row) =>
      withdraw({
        caseId: row.caseId,
        expectedVersion: row.currentVersion,
      }).unwrap(),
    close: (row, outcome) =>
      (outcome === 'Hit' ? closeHit : closeAway)({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap(),
    cancel: (row) =>
      cancel({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap(),
    reopen: (row) =>
      reopen({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap(),
    correctOutcome: (row, outcome, reason) =>
      (outcome === 'Hit' ? correctHit : correctAway)({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        reason,
      }).unwrap(),
  }
  const contactOwner: TraderContactOwnerActions = {
    change: (row, targetUserId) =>
      changeOwner({
        caseId: row.caseId,
        targetUserId,
        expectedCurrentVersion: row.currentVersion,
        confirmed: true,
      }).unwrap(),
  }
  const memo: TraderMemoActions = {
    update: (row, value) =>
      updateMemo({
        caseId: row.caseId,
        memo: value,
        expectedVersion: row.traderMemoVersion,
      }).unwrap(),
  }
  const bulk: TraderBulkActions = { execute: runBulk }
  const search: TraderSearchActions = {
    execute: (params: RfqSearchParams) => searchRfqs(params).unwrap(),
  }
  const pricer: TraderPricerActions = {
    calculate: (input) => scratch(input).unwrap(),
  }
  const gridLayout: TraderGridLayoutActions = {
    configs: {
      main: configJson(mainConfigQuery.data?.config),
      search: configJson(searchConfigQuery.data?.config),
      confirm: configJson(confirmConfigQuery.data?.config),
    },
    save: saveConfig,
  }

  return (
    <TraderScreen
      rfqs={refresh.rows}
      traders={(tradersQuery.data ?? []).map((user) => ({
        userId: user.userId,
        name: user.name,
      }))}
      users={(usersQuery.data ?? []).map((user) => ({
        userId: user.userId,
        name: user.name,
      }))}
      currentUserId={currentUserId}
      businessDate={businessDate}
      defaultExpiryMinutes={
        quoteExpiryQuery.data?.type === 'After'
          ? quoteExpiryQuery.data.minutes
          : null
      }
      isLoading={
        rfqsQuery.isLoading || (refresh.mode === 'live' && rfqsQuery.isFetching)
      }
      isError={rfqsQuery.isError}
      isMutating={mutationStates.some((state) => state.isLoading)}
      refreshMode={refresh.mode}
      updatesPending={refresh.updatesPending}
      remoteUpdatePending={refresh.liveUpdateDeferred}
      refreshError={refresh.refreshError}
      refreshBlocked={refresh.refreshBlocked}
      refreshGeneration={refreshGeneration}
      onRefreshModeChange={refresh.changeMode}
      onManualRefresh={refresh.refresh}
      onTransientStateChange={setProtectedState}
      onReconcileCases={refresh.reconcileCases}
      ownership={ownership}
      workingQuote={workingQuote}
      lifecycle={lifecycle}
      contactOwner={contactOwner}
      memo={memo}
      bulk={bulk}
      search={search}
      pricer={pricer}
      gridLayout={gridLayout}
    />
  )
}
