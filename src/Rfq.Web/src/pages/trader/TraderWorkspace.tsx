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
  type CaseOperationResponse,
  type QuoteExpiryResponse,
  type SearchRfqsApiArg,
  type TraderRfqResponse,
} from '@/generated/rfqApi'
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
import { unwrapApiResult } from '@/services/apiProblem'

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
    keyOf: (row: TraderRfqResponse) => row.caseId,
    onAuthoritativeRefresh: () => setRefreshGeneration((value) => value + 1),
  })

  const lifecycleItems = (rows: TraderRfqResponse[]) =>
    rows.map((row) => ({
      caseId: row.caseId,
      expectedCurrentVersion: row.currentVersion,
    }))
  const ownershipItems = (rows: TraderRfqResponse[]) =>
    rows.map((row) => ({
      caseId: row.caseId,
      expectedCurrentVersion: row.currentVersion,
    }))
  const expiryFor = (minutes: number | null): QuoteExpiryResponse =>
    minutes === null
      ? { type: 'None', minutes: null }
      : { type: 'After', minutes }

  const runBulk = async (
    command: TraderBulkCommand,
    rows: TraderRfqResponse[],
    expiryMinutes: number | null,
    targetTraderId?: string,
  ): Promise<CaseOperationResponse[]> => {
    switch (command) {
      case 'pick':
        return unwrapApiResult(
          bulkPick({
            pickUpRfqsRequest: {
              confirmed: rows.some((row) =>
                requiresPickUpConfirmation(row, currentUserId),
              ),
              items: ownershipItems(rows),
            },
          }),
        )
      case 'release':
        return unwrapApiResult(
          bulkRelease({
            releaseRfqsRequest: { items: ownershipItems(rows) },
          }),
        )
      case 'assign':
        return unwrapApiResult(
          bulkAssign({
            assignTradersRequest: {
              targetTraderId: targetTraderId!,
              items: ownershipItems(rows),
            },
          }),
        )
      case 'confirm':
        return unwrapApiResult(
          bulkConfirm({
            confirmQuotesRequest: {
              items: rows.map((row) => ({
                caseId: row.caseId,
                expiry: expiryFor(expiryMinutes),
                expectedCurrentVersion: row.currentVersion,
                expectedWorkingQuoteVersion: row.workingQuoteVersion,
              })),
            },
          }),
        )
      case 'withdraw':
        return unwrapApiResult(
          bulkWithdraw({
            withdrawQuotesRequest: { items: lifecycleItems(rows) },
          }),
        )
      case 'away':
        return unwrapApiResult(
          bulkAway({
            closeAwayRfqsRequest: { items: lifecycleItems(rows) },
          }),
        )
      case 'cancel':
        return unwrapApiResult(
          bulkCancel({ cancelRfqsRequest: { items: lifecycleItems(rows) } }),
        )
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

    return unwrapApiResult(
      saveGridConfig({
        screenId: 'trader',
        configKey: key,
        gridConfigRequest: {
          version: (query.data?.version ?? 0) + 1,
          config: JSON.parse(json),
        },
      }),
    ).then(async () => {
      await query
        .refetch()
        .unwrap()
        .catch(() => undefined)
    })
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
      unwrapApiResult(
        pickUp({
          pickUpRfqsRequest: {
            confirmed,
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    release: (row) =>
      unwrapApiResult(
        release({
          releaseRfqsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    assign: (row, targetTraderId) =>
      unwrapApiResult(
        assign({
          assignTradersRequest: {
            targetTraderId,
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    takeOver: (row) =>
      unwrapApiResult(
        takeOver({
          takeOverRfqsRequest: {
            confirmed: true,
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
  }
  const workingQuote: TraderWorkingQuoteActions = {
    calculate: (row, driver, value, slide) =>
      unwrapApiResult(
        calculate({
          caseId: row.caseId,
          calculateWorkingQuoteRequest: {
            driver,
            value,
            simpleYieldSlide: slide,
            expectedCurrentVersion: row.currentVersion,
            expectedWorkingQuoteVersion: row.workingQuoteVersion,
          },
        }),
      ),
    changeMode: (row, mode) =>
      unwrapApiResult(
        changeMode({
          caseId: row.caseId,
          changeWorkingQuoteModeRequest: {
            mode,
            expectedCurrentVersion: row.currentVersion,
            expectedWorkingQuoteVersion: row.workingQuoteVersion,
          },
        }),
      ),
    updateManual: (row, price, finalSimpleYield) =>
      unwrapApiResult(
        updateManual({
          caseId: row.caseId,
          updateManualWorkingQuoteRequest: {
            price,
            finalSimpleYield,
            expectedCurrentVersion: row.currentVersion,
            expectedWorkingQuoteVersion: row.workingQuoteVersion,
          },
        }),
      ),
    confirm: (row, expiryMinutes) =>
      unwrapApiResult(
        confirmQuote({
          confirmQuotesRequest: {
            items: [
              {
                caseId: row.caseId,
                expiry: expiryFor(expiryMinutes),
                expectedCurrentVersion: row.currentVersion,
                expectedWorkingQuoteVersion: row.workingQuoteVersion,
              },
            ],
          },
        }),
      ),
  }
  const lifecycle: TraderLifecycleActions = {
    present: (row) =>
      unwrapApiResult(
        present({
          presentRfqsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    unpresent: (row) =>
      unwrapApiResult(
        unpresent({
          unpresentRfqsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    withdraw: (row) =>
      unwrapApiResult(
        withdraw({
          withdrawQuotesRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    close: (row, outcome) =>
      (outcome === 'Hit'
        ? unwrapApiResult(
            closeHit({
              caseId: row.caseId,
              closeHitRfqRequest: {
                expectedCurrentVersion: row.currentVersion,
              },
            }),
          )
        : unwrapApiResult(
            closeAway({
              closeAwayRfqsRequest: {
                items: [
                  {
                    caseId: row.caseId,
                    expectedCurrentVersion: row.currentVersion,
                  },
                ],
              },
            }),
          )
      ).then(() => undefined),
    cancel: (row) =>
      unwrapApiResult(
        cancel({
          cancelRfqsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    reopen: (row) =>
      unwrapApiResult(
        reopen({
          reopenRfqsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
    correctOutcome: (row, outcome, reason) =>
      unwrapApiResult(
        outcome === 'Hit'
          ? correctHit({
              caseId: row.caseId,
              correctOutcomeToHitRequest: {
                expectedCurrentVersion: row.currentVersion,
                reason,
              },
            })
          : correctAway({
              caseId: row.caseId,
              correctOutcomeToAwayRequest: {
                expectedCurrentVersion: row.currentVersion,
                reason,
              },
            }),
      ),
  }
  const contactOwner: TraderContactOwnerActions = {
    change: (row, targetUserId) =>
      unwrapApiResult(
        changeOwner({
          changeContactOwnersRequest: {
            targetContactOwnerId: targetUserId,
            confirmed: true,
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
              },
            ],
          },
        }),
      ).then(() => undefined),
  }
  const memo: TraderMemoActions = {
    update: (row, value) =>
      unwrapApiResult(
        updateMemo({
          caseId: row.caseId,
          updateMemoRequest: {
            memo: value,
            expectedMemoVersion: row.traderMemoVersion,
          },
        }),
      ),
  }
  const bulk: TraderBulkActions = { execute: runBulk }
  const search: TraderSearchActions = {
    execute: (params: SearchRfqsApiArg) => unwrapApiResult(searchRfqs(params)),
  }
  const pricer: TraderPricerActions = {
    calculate: (input) => unwrapApiResult(scratch({ pricerRequest: input })),
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
