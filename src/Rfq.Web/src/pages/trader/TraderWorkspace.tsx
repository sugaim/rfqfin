import { useState } from 'react'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'
import {
  useAssignTradersMutation,
  useCancelRfqsMutation,
  useCloseAwayRfqsMutation,
  useConfirmQuotesMutation,
  usePickUpRfqsMutation,
  useReleaseRfqsMutation,
  useWithdrawQuotesMutation,
  useCalculateWorkingQuoteMutation,
  useChangeContactOwnersMutation,
  useChangeWorkingQuoteModeMutation,
  useCloseHitRfqMutation,
  useCorrectOutcomeToAwayMutation,
  useCorrectOutcomeToHitMutation,
  useGetActiveTraderRfqsQuery,
  useGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useGetGridConfigQuery,
  useGetQuoteExpiryQuery,
  useLazySearchRfqsQuery,
  usePresentRfqsMutation,
  useReopenRfqsMutation,
  useSaveGridConfigMutation,
  useScratchPriceMutation,
  useTakeOverRfqsMutation,
  useUnpresentRfqsMutation,
  useUpdateManualWorkingQuoteMutation,
  useUpdateTraderMemoMutation,
  type CaseOperationResult,
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
  const [pickUp, pickUpState] = usePickUpRfqsMutation()
  const [release, releaseState] = useReleaseRfqsMutation()
  const [assign, assignState] = useAssignTradersMutation()
  const [takeOver, takeOverState] = useTakeOverRfqsMutation()
  const [calculate] = useCalculateWorkingQuoteMutation()
  const [changeMode, changeModeState] = useChangeWorkingQuoteModeMutation()
  const [updateManual] = useUpdateManualWorkingQuoteMutation()
  const [confirmQuote, confirmState] = useConfirmQuotesMutation()
  const [present, presentState] = usePresentRfqsMutation()
  const [unpresent, unpresentState] = useUnpresentRfqsMutation()
  const [withdraw, withdrawState] = useWithdrawQuotesMutation()
  const [closeHit, closeHitState] = useCloseHitRfqMutation()
  const [closeAway, closeAwayState] = useCloseAwayRfqsMutation()
  const [cancel, cancelState] = useCancelRfqsMutation()
  const [reopen, reopenState] = useReopenRfqsMutation()
  const [correctHit, correctHitState] = useCorrectOutcomeToHitMutation()
  const [correctAway, correctAwayState] = useCorrectOutcomeToAwayMutation()
  const [changeOwner, changeOwnerState] = useChangeContactOwnersMutation()
  const [updateMemo, updateMemoState] = useUpdateTraderMemoMutation()
  const [scratch] = useScratchPriceMutation()
  const [bulkPick, bulkPickState] = usePickUpRfqsMutation()
  const [bulkRelease, bulkReleaseState] = useReleaseRfqsMutation()
  const [bulkAssign, bulkAssignState] = useAssignTradersMutation()
  const [bulkConfirm, bulkConfirmState] = useConfirmQuotesMutation()
  const [bulkWithdraw, bulkWithdrawState] = useWithdrawQuotesMutation()
  const [bulkAway, bulkAwayState] = useCloseAwayRfqsMutation()
  const [bulkCancel, bulkCancelState] = useCancelRfqsMutation()

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
      expectedCurrentVersion: row.currentVersion,
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
  ): Promise<CaseOperationResult[]> => {
    switch (command) {
      case 'pick':
        return bulkPick({
          confirmed: rows.some((row) =>
            requiresPickUpConfirmation(row, currentUserId),
          ),
          items: rows.map((row) => ({
            caseId: row.caseId,
            expectedCurrentVersion: row.currentVersion,
          })),
        }).unwrap()
      case 'release':
        return bulkRelease({ items: ownershipItems(rows) }).unwrap()
      case 'assign':
        return bulkAssign({
          targetTraderId: targetTraderId!,
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
        confirmed,
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    release: (row) =>
      release({
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    assign: (row, targetTraderId) =>
      assign({
        targetTraderId,
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    takeOver: (row) =>
      takeOver({
        confirmed: true,
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
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
        items: [
          {
            caseId: row.caseId,
            expiry: expiryFor(expiryMinutes),
            expectedCurrentVersion: row.currentVersion,
            expectedWorkingQuoteVersion: row.workingQuoteVersion,
          },
        ],
      })
        .unwrap()
        .then(() => undefined),
  }
  const lifecycle: TraderLifecycleActions = {
    present: (row) =>
      present({
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    unpresent: (row) =>
      unpresent({
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    withdraw: (row) =>
      withdraw({
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    close: (row, outcome) =>
      (outcome === 'Hit'
        ? closeHit({
            caseId: row.caseId,
            expectedCurrentVersion: row.currentVersion,
          })
        : closeAway({
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          })
      )
        .unwrap()
        .then(() => undefined),
    cancel: (row) =>
      cancel({
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
    reopen: (row) =>
      reopen({
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
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
        targetContactOwnerId: targetUserId,
        confirmed: true,
        items: [
          { caseId: row.caseId, expectedCurrentVersion: row.currentVersion },
        ],
      })
        .unwrap()
        .then(() => undefined),
  }
  const memo: TraderMemoActions = {
    update: (row, value) =>
      updateMemo({
        caseId: row.caseId,
        memo: value,
        expectedMemoVersion: row.traderMemoVersion,
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
