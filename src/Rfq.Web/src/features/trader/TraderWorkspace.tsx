import { useCallback, useEffect, useRef, useState } from 'react'
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
  useGetDefaultQuoteModeQuery,
  useGetGridConfigQuery,
  useGetQuoteExpiryQuery,
  useLazySearchRfqsQuery,
  usePickUpRfqMutation,
  usePresentQuoteMutation,
  useReleaseRfqMutation,
  useReopenRfqMutation,
  useSaveDefaultQuoteModeMutation,
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
import { TraderScreen } from '@/features/trader/TraderScreen'
import type {
  TraderBulkCommand,
  TraderRefreshMode,
} from '@/features/trader/traderModel'
import { requiresPickUpConfirmation } from '@/features/trader/traderModel'

export function TraderWorkspace() {
  const {
    currentUserId,
    businessDate,
    remoteChangeVersion,
    acknowledgeRemoteChanges,
  } = useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveTraderRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const quoteExpiryQuery = useGetQuoteExpiryQuery()
  const defaultModeQuery = useGetDefaultQuoteModeQuery()
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
  const [saveDefaultMode] = useSaveDefaultQuoteModeMutation()
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

  const [visibleRfqs, setVisibleRfqs] = useState<TraderRfq[]>([])
  const [refreshMode, setRefreshMode] = useState<TraderRefreshMode>('live')
  const [pendingUpdateCount, setPendingUpdateCount] = useState(0)
  const [protectedState, setProtectedState] = useState(false)
  const [deferredUpdate, setDeferredUpdate] = useState(false)
  const [refreshGeneration, setRefreshGeneration] = useState(0)
  const observedRemoteVersion = useRef(remoteChangeVersion)

  const catchUp = useCallback(async () => {
    const result = await rfqsQuery.refetch()
    if (result.data) setVisibleRfqs(result.data)
    await acknowledgeRemoteChanges()
    setRefreshGeneration((value) => value + 1)
    setDeferredUpdate(false)
    setPendingUpdateCount(0)
  }, [acknowledgeRemoteChanges, rfqsQuery.refetch])

  useEffect(() => {
    if (refreshMode === 'live' && !protectedState && rfqsQuery.data)
      setVisibleRfqs(rfqsQuery.data)
  }, [protectedState, refreshMode, rfqsQuery.data])

  useEffect(() => {
    if (remoteChangeVersion === observedRemoteVersion.current) return
    const count = Math.max(
      1,
      remoteChangeVersion - observedRemoteVersion.current,
    )
    observedRemoteVersion.current = remoteChangeVersion
    if (refreshMode === 'paused')
      setPendingUpdateCount((value) => value + count)
    else if (protectedState) setDeferredUpdate(true)
    else void catchUp()
  }, [catchUp, protectedState, refreshMode, remoteChangeVersion])

  useEffect(() => {
    if (refreshMode === 'live' && !protectedState && deferredUpdate)
      void catchUp()
  }, [catchUp, deferredUpdate, protectedState, refreshMode])

  const changeRefreshMode = async (mode: TraderRefreshMode) => {
    if (mode === refreshMode) return
    setRefreshMode(mode)
    if (mode === 'live') {
      if (protectedState) setDeferredUpdate(true)
      else await catchUp()
    }
  }

  const patchRow = (caseId: number, update: (row: TraderRfq) => TraderRfq) =>
    setVisibleRfqs((rows) =>
      rows.map((row) => (row.caseId === caseId ? update(row) : row)),
    )

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

  return (
    <TraderScreen
      rfqs={visibleRfqs}
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
      defaultQuoteMode={defaultModeQuery.data?.mode ?? 'Calculated'}
      isLoading={
        rfqsQuery.isLoading || (refreshMode === 'live' && rfqsQuery.isFetching)
      }
      isError={rfqsQuery.isError}
      isMutating={mutationStates.some((state) => state.isLoading)}
      refreshMode={refreshMode}
      pendingUpdateCount={pendingUpdateCount}
      remoteUpdatePending={deferredUpdate}
      refreshGeneration={refreshGeneration}
      onRefreshModeChange={changeRefreshMode}
      onManualRefresh={catchUp}
      onTransientStateChange={setProtectedState}
      onPatchRow={patchRow}
      onPickUp={(row, confirmed) =>
        pickUp({
          caseId: row.caseId,
          expectedVersion: row.currentVersion,
          confirmed,
        }).unwrap()
      }
      onRelease={(row) =>
        release({
          caseId: row.caseId,
          expectedVersion: row.currentVersion,
        }).unwrap()
      }
      onAssign={(row, targetTraderId) =>
        assign({
          caseId: row.caseId,
          targetTraderId,
          expectedVersion: row.currentVersion,
        }).unwrap()
      }
      onTakeOver={(row) =>
        takeOver({
          caseId: row.caseId,
          expectedVersion: row.currentVersion,
          confirmed: true,
        }).unwrap()
      }
      onCalculate={(row, driver, value, slide) =>
        calculate({
          caseId: row.caseId,
          driver,
          value,
          simpleYieldSlide: slide,
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap()
      }
      onChangeMode={(row, mode) =>
        changeMode({
          caseId: row.caseId,
          mode,
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap()
      }
      onUpdateManual={(row, price, finalSimpleYield) =>
        updateManual({
          caseId: row.caseId,
          price,
          finalSimpleYield,
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap()
      }
      onConfirmQuote={(row, expiryMinutes) =>
        confirmQuote({
          caseId: row.caseId,
          expiry: expiryFor(expiryMinutes),
          expectedCurrentVersion: row.currentVersion,
          expectedWorkingQuoteVersion: row.workingQuoteVersion,
        }).unwrap()
      }
      onPresent={(row) =>
        present({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        }).unwrap()
      }
      onUnpresent={(row) =>
        unpresent({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        }).unwrap()
      }
      onWithdraw={(row) =>
        withdraw({
          caseId: row.caseId,
          expectedVersion: row.currentVersion,
        }).unwrap()
      }
      onClose={(row, outcome) =>
        (outcome === 'Hit' ? closeHit : closeAway)({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        }).unwrap()
      }
      onCancel={(row) =>
        cancel({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        }).unwrap()
      }
      onReopen={(row) =>
        reopen({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        }).unwrap()
      }
      onCorrectOutcome={(row, outcome, reason) =>
        (outcome === 'Hit' ? correctHit : correctAway)({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
          reason,
        }).unwrap()
      }
      onChangeContactOwner={(row, targetUserId) =>
        changeOwner({
          caseId: row.caseId,
          targetUserId,
          expectedCurrentVersion: row.currentVersion,
          confirmed: true,
        }).unwrap()
      }
      onUpdateMemo={(row, memo) =>
        updateMemo({
          caseId: row.caseId,
          memo,
          expectedVersion: row.traderMemoVersion,
        }).unwrap()
      }
      onBulk={runBulk}
      onSearch={(params: RfqSearchParams) => searchRfqs(params).unwrap()}
      onScratchPrice={(input) => scratch(input).unwrap()}
      onSaveDefaultQuoteMode={(mode) =>
        saveDefaultMode({ mode })
          .unwrap()
          .then(() => undefined)
      }
      mainGridConfigJson={configJson(mainConfigQuery.data?.config)}
      searchGridConfigJson={configJson(searchConfigQuery.data?.config)}
      confirmGridConfigJson={configJson(confirmConfigQuery.data?.config)}
      onSaveGridConfig={saveConfig}
      onReload={catchUp}
    />
  )
}
