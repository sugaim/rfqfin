import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'
import {
  useBulkCancelRfqsMutation,
  useBulkCloseAwayRfqsMutation,
  useBulkConfirmAmendmentsMutation,
  useBulkConfirmInitialDraftsMutation,
  useBulkDiscardAmendmentsMutation,
  useBulkDiscardInitialDraftsMutation,
  useBulkPresentRfqsMutation,
  useBulkUnpresentRfqsMutation,
  useCancelRfqMutation,
  useChangeContactOwnerMutation,
  useCloseAwayRfqMutation,
  useCloseHitRfqMutation,
  useConfirmAmendmentMutation,
  useConfirmDraftMutation,
  useConfirmNewRfqMutation,
  useCorrectOutcomeToAwayMutation,
  useCorrectOutcomeToHitMutation,
  useCreateDraftMutation,
  useCreateFromExistingMutation,
  useDiscardAmendmentMutation,
  useDiscardDraftMutation,
  useGetActiveSalesRfqsQuery,
  useGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useGetGridConfigQuery,
  useLazyGetSalesRecentRevisionsQuery,
  useLazyResolveRfqCreationContextQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
  usePresentQuoteMutation,
  useReopenRfqMutation,
  useSaveAmendmentMutation,
  useSaveGridConfigMutation,
  useUnpresentQuoteMutation,
  useUpdateDraftMutation,
  useUpdateSalesMemoMutation,
  useStartAmendmentMutation,
  type BulkItemResult,
  type ClientSearchResult,
  type SalesRfq,
  type SecuritySearchResult,
} from '@/services/api'
import { SalesScreen } from '@/pages/sales/SalesScreen'
import type { SalesBulkCommand } from '@/pages/sales/salesModel'
import type {
  SalesAmendmentActions,
  SalesBulkActions,
  SalesContactOwnerActions,
  SalesDraftActions,
  SalesGridLayoutActions,
  SalesLifecycleActions,
  SalesLookupActions,
  SalesMemoActions,
} from '@/pages/sales/salesContracts'
import { useLivePausedRows } from '@/shared/state/useLivePausedRows'
import { shouldLoadRecentRevisions } from '@/pages/sales/recentRevisions'

export function SalesWorkspace() {
  const { currentUserId, salesChangeVersion, recentRevisionsChangeVersion } =
    useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveSalesRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const gridConfigQuery = useGetGridConfigQuery({
    screenId: 'sales',
    configKey: 'main',
  })
  const [loadRecent, recentQuery] = useLazyGetSalesRecentRevisionsQuery()
  const [createDraft, createState] = useCreateDraftMutation()
  const [updateDraft, updateState] = useUpdateDraftMutation()
  const [confirmNewRfq, confirmNewState] = useConfirmNewRfqMutation()
  const [confirmDraft, confirmState] = useConfirmDraftMutation()
  const [discardDraft, discardState] = useDiscardDraftMutation()
  const [presentQuote, presentState] = usePresentQuoteMutation()
  const [unpresentQuote, unpresentState] = useUnpresentQuoteMutation()
  const [closeHitRfq, closeHitState] = useCloseHitRfqMutation()
  const [closeAwayRfq, closeAwayState] = useCloseAwayRfqMutation()
  const [correctToHit, correctToHitState] = useCorrectOutcomeToHitMutation()
  const [correctToAway, correctToAwayState] = useCorrectOutcomeToAwayMutation()
  const [cancelRfq, cancelState] = useCancelRfqMutation()
  const [changeContactOwner, changeContactOwnerState] =
    useChangeContactOwnerMutation()
  const [reopenRfq, reopenState] = useReopenRfqMutation()
  const [createFromExisting, createFromExistingState] =
    useCreateFromExistingMutation()
  const [updateSalesMemo, updateSalesMemoState] = useUpdateSalesMemoMutation()
  const [saveAmendment, saveAmendmentState] = useSaveAmendmentMutation()
  const [startAmendment, startAmendmentState] = useStartAmendmentMutation()
  const [confirmAmendment, confirmAmendmentState] =
    useConfirmAmendmentMutation()
  const [discardAmendment, discardAmendmentState] =
    useDiscardAmendmentMutation()
  const [bulkAway, bulkAwayState] = useBulkCloseAwayRfqsMutation()
  const [bulkCancel, bulkCancelState] = useBulkCancelRfqsMutation()
  const [bulkPresent, bulkPresentState] = useBulkPresentRfqsMutation()
  const [bulkUnpresent, bulkUnpresentState] = useBulkUnpresentRfqsMutation()
  const [bulkConfirmDrafts, bulkConfirmDraftsState] =
    useBulkConfirmInitialDraftsMutation()
  const [bulkDiscardDrafts, bulkDiscardDraftsState] =
    useBulkDiscardInitialDraftsMutation()
  const [bulkConfirmAmendments, bulkConfirmAmendmentsState] =
    useBulkConfirmAmendmentsMutation()
  const [bulkDiscardAmendments, bulkDiscardAmendmentsState] =
    useBulkDiscardAmendmentsMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqCreationContextQuery()
  const [clients, setClients] = useState<ClientSearchResult[]>([])
  const [securities, setSecurities] = useState<SecuritySearchResult[]>([])
  const [protectedState, setProtectedState] = useState(false)
  const [recentOpen, setRecentOpen] = useState(false)
  const [recentGeneration, setRecentGeneration] = useState(1)
  const [loadedRecentGeneration, setLoadedRecentGeneration] = useState(0)
  const refresh = useLivePausedRows({
    authoritativeRows: rfqsQuery.data,
    remoteChangeVersion: salesChangeVersion,
    protectedState,
    refetch: rfqsQuery.refetch,
    keyOf: (row: SalesRfq) => row.caseId,
  })

  useEffect(() => {
    setRecentGeneration((value) => value + 1)
  }, [recentRevisionsChangeVersion])

  useEffect(() => {
    if (
      !shouldLoadRecentRevisions(
        recentOpen,
        recentQuery.isFetching,
        recentGeneration,
        loadedRecentGeneration,
      )
    )
      return
    const requestedGeneration = recentGeneration
    void loadRecent(undefined)
      .unwrap()
      .then(() => setLoadedRecentGeneration(requestedGeneration))
  }, [
    loadRecent,
    loadedRecentGeneration,
    recentGeneration,
    recentOpen,
    recentQuery.isFetching,
  ])

  const lifecycleItems = (rows: SalesRfq[]) => ({
    items: rows.map((row) => ({
      caseId: row.caseId,
      expectedCurrentVersion: row.currentVersion,
    })),
  })

  const runBulk = async (
    command: SalesBulkCommand,
    rows: SalesRfq[],
  ): Promise<BulkItemResult[]> => {
    switch (command) {
      case 'away':
        return bulkAway(lifecycleItems(rows)).unwrap()
      case 'cancel':
        return bulkCancel(lifecycleItems(rows)).unwrap()
      case 'present':
        return bulkPresent(lifecycleItems(rows)).unwrap()
      case 'unpresent':
        return bulkUnpresent(lifecycleItems(rows)).unwrap()
      case 'confirm-drafts':
        return bulkConfirmDrafts({
          items: rows.map((row) => ({
            caseId: row.caseId,
            notional: row.notional,
            settlementDate: row.settlementDate!,
            standardSettlementDate: row.standardSettlementDate,
            salesAndTradingMessage: row.salesAndTradingMessage,
            assignedTraderId: row.assignedTraderId,
            expectedVersion: row.version,
          })),
        }).unwrap()
      case 'discard-drafts':
        return bulkDiscardDrafts({
          items: rows.map((row) => ({
            caseId: row.caseId,
            expectedVersion: row.version,
          })),
        }).unwrap()
      case 'confirm-amendments':
        return bulkConfirmAmendments({
          items: rows.map((row) => ({
            caseId: row.caseId,
            expectedCurrentVersion: row.currentVersion,
            expectedDraftVersion: row.draftVersion!,
          })),
        }).unwrap()
      case 'discard-amendments':
        return bulkDiscardAmendments({
          items: rows.map((row) => ({
            caseId: row.caseId,
            expectedCurrentVersion: row.currentVersion,
            expectedDraftVersion: row.draftVersion!,
          })),
        }).unwrap()
    }
  }

  const mutationStates = [
    createState,
    updateState,
    confirmNewState,
    confirmState,
    discardState,
    presentState,
    unpresentState,
    closeHitState,
    closeAwayState,
    correctToHitState,
    correctToAwayState,
    cancelState,
    changeContactOwnerState,
    reopenState,
    createFromExistingState,
    updateSalesMemoState,
    startAmendmentState,
    saveAmendmentState,
    confirmAmendmentState,
    discardAmendmentState,
    bulkAwayState,
    bulkCancelState,
    bulkPresentState,
    bulkUnpresentState,
    bulkConfirmDraftsState,
    bulkDiscardDraftsState,
    bulkConfirmAmendmentsState,
    bulkDiscardAmendmentsState,
  ]
  const lookup: SalesLookupActions = {
    searchClients: async (query) =>
      setClients(query.trim() ? await searchClients(query).unwrap() : []),
    searchSecurities: async (query) =>
      setSecurities(query.trim() ? await searchSecurities(query).unwrap() : []),
    resolveDefaults: (securityId) => resolveDefaults({ securityId }).unwrap(),
  }
  const draft: SalesDraftActions = {
    create: (request) => createDraft(request).unwrap(),
    update: (caseId, body) => updateDraft({ caseId, body }).unwrap(),
    confirmNew: (request) => confirmNewRfq(request).unwrap(),
    confirm: (caseId, body) => confirmDraft({ caseId, body }).unwrap(),
    discard: (caseId, expectedVersion) =>
      discardDraft({ caseId, expectedVersion }).unwrap(),
  }
  const lifecycle: SalesLifecycleActions = {
    present: (caseId, expectedCurrentVersion) =>
      presentQuote({ caseId, expectedCurrentVersion })
        .unwrap()
        .then(() => undefined),
    unpresent: (caseId, expectedCurrentVersion) =>
      unpresentQuote({ caseId, expectedCurrentVersion })
        .unwrap()
        .then(() => undefined),
    close: (caseId, outcome, expectedCurrentVersion) =>
      (outcome === 'Hit' ? closeHitRfq : closeAwayRfq)({
        caseId,
        expectedCurrentVersion,
      })
        .unwrap()
        .then(() => undefined),
    correctOutcome: (caseId, outcome, expectedCurrentVersion, reason) =>
      (outcome === 'Hit' ? correctToHit : correctToAway)({
        caseId,
        expectedCurrentVersion,
        reason,
      })
        .unwrap()
        .then(() => undefined),
    cancel: (row) =>
      cancelRfq({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      })
        .unwrap()
        .then(() => undefined),
    reopen: (row) =>
      reopenRfq({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      })
        .unwrap()
        .then(() => undefined),
    createFromExisting: (caseId) => createFromExisting(caseId).unwrap(),
  }
  const contactOwner: SalesContactOwnerActions = {
    change: (caseId, targetUserId, expectedCurrentVersion) =>
      changeContactOwner({
        caseId,
        targetUserId,
        expectedCurrentVersion,
        confirmed: true,
      })
        .unwrap()
        .then(() => undefined),
  }
  const memo: SalesMemoActions = {
    update: (caseId, value, expectedVersion) =>
      updateSalesMemo({ caseId, memo: value, expectedVersion })
        .unwrap()
        .then(() => undefined),
  }
  const amendment: SalesAmendmentActions = {
    start: (row) =>
      startAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap(),
    save: (
      caseId,
      notional,
      settlementDate,
      text,
      expectedCurrentVersion,
      expectedDraftVersion,
    ) =>
      saveAmendment({
        caseId,
        notional,
        settlementDate,
        salesAndTradingMessage: text,
        expectedCurrentVersion,
        expectedDraftVersion,
      }).unwrap(),
    confirm: (row) =>
      confirmAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      }).unwrap(),
    discard: (row) =>
      discardAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      }).unwrap(),
  }
  const bulk: SalesBulkActions = { execute: runBulk }
  const gridLayout: SalesGridLayoutActions = {
    configJson: gridConfigQuery.data
      ? JSON.stringify(gridConfigQuery.data.config)
      : undefined,
    save: (configJson) =>
      saveGridConfig({
        screenId: 'sales',
        configKey: 'main',
        version: (gridConfigQuery.data?.version ?? 0) + 1,
        config: JSON.parse(configJson),
      })
        .unwrap()
        .then(() => undefined),
  }

  return (
    <SalesScreen
      rfqs={refresh.rows}
      clients={clients}
      securities={securities}
      traders={(tradersQuery.data ?? []).map((user) => ({
        userId: user.userId,
        name: user.name,
      }))}
      users={(usersQuery.data ?? []).map((user) => ({
        userId: user.userId,
        name: user.name,
      }))}
      currentUserId={currentUserId}
      isLoading={
        rfqsQuery.isLoading || (refresh.mode === 'live' && rfqsQuery.isFetching)
      }
      isError={rfqsQuery.isError}
      isMutating={mutationStates.some((state) => state.isLoading)}
      recentRevisions={recentQuery.data ?? []}
      recentRevisionsLoading={recentQuery.isLoading || recentQuery.isFetching}
      onRecentRevisionsOpenChange={setRecentOpen}
      remoteUpdatePending={refresh.liveUpdateDeferred}
      refreshMode={refresh.mode}
      updatesPending={refresh.updatesPending}
      refreshError={refresh.refreshError}
      refreshBlocked={refresh.refreshBlocked}
      onRefreshModeChange={refresh.changeMode}
      onManualRefresh={refresh.refresh}
      onReconcileCases={refresh.reconcileCases}
      onTransientStateChange={setProtectedState}
      lookup={lookup}
      draft={draft}
      lifecycle={lifecycle}
      amendment={amendment}
      contactOwner={contactOwner}
      memo={memo}
      bulk={bulk}
      gridLayout={gridLayout}
    />
  )
}
