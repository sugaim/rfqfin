import { useCallback, useEffect, useRef, useState } from 'react'
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
  useGetSalesRecentRevisionsQuery,
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
  type BulkItemResult,
  type ClientSearchResult,
  type SalesRfq,
  type SecuritySearchResult,
} from '@/services/api'
import { SalesScreen } from '@/features/sales/SalesScreen'
import {
  reflectConfirmedAmendment,
  type SalesBulkCommand,
  type SalesRowCommand,
} from '@/features/sales/salesModel'
import type { SalesRefreshMode } from '@/features/sales/salesModel'
import type {
  SalesAmendmentActions,
  SalesBulkActions,
  SalesContactOwnerActions,
  SalesDraftActions,
  SalesGridLayoutActions,
  SalesLifecycleActions,
  SalesLookupActions,
  SalesMemoActions,
} from '@/features/sales/salesContracts'

export function SalesWorkspace() {
  const { currentUserId, remoteChangeVersion, acknowledgeRemoteChanges } =
    useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveSalesRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const gridConfigQuery = useGetGridConfigQuery({
    screenId: 'sales',
    configKey: 'main',
  })
  const recentQuery = useGetSalesRecentRevisionsQuery(50)
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
  const [visibleRfqs, setVisibleRfqs] = useState<SalesRfq[]>([])
  const [refreshMode, setRefreshMode] = useState<SalesRefreshMode>('live')
  const [pendingUpdateCount, setPendingUpdateCount] = useState(0)
  const [protectedState, setProtectedState] = useState(false)
  const [deferredUpdate, setDeferredUpdate] = useState(false)
  const observedRemoteVersion = useRef(remoteChangeVersion)

  const catchUp = useCallback(async () => {
    const [rfqResult] = await Promise.all([
      rfqsQuery.refetch(),
      recentQuery.refetch(),
    ])
    if (rfqResult.data) setVisibleRfqs(rfqResult.data)
    await acknowledgeRemoteChanges()
    setDeferredUpdate(false)
    setPendingUpdateCount(0)
  }, [acknowledgeRemoteChanges, recentQuery.refetch, rfqsQuery.refetch])

  useEffect(() => {
    if (refreshMode === 'live' && !protectedState && rfqsQuery.data)
      setVisibleRfqs(rfqsQuery.data)
  }, [protectedState, refreshMode, rfqsQuery.data])

  useEffect(() => {
    if (remoteChangeVersion === observedRemoteVersion.current) return
    const changeCount = Math.max(
      1,
      remoteChangeVersion - observedRemoteVersion.current,
    )
    observedRemoteVersion.current = remoteChangeVersion
    if (refreshMode === 'paused')
      setPendingUpdateCount((current) => current + changeCount)
    else if (protectedState) setDeferredUpdate(true)
    else void catchUp()
  }, [catchUp, protectedState, refreshMode, remoteChangeVersion])

  useEffect(() => {
    if (refreshMode === 'live' && !protectedState && deferredUpdate)
      void catchUp()
  }, [catchUp, deferredUpdate, protectedState, refreshMode])

  const changeRefreshMode = async (mode: SalesRefreshMode) => {
    if (mode === refreshMode) return
    setRefreshMode(mode)
    if (mode === 'live') {
      if (protectedState) setDeferredUpdate(true)
      else await catchUp()
    }
  }

  const applyRowAction = (command: SalesRowCommand, target: SalesRfq) => {
    setVisibleRfqs((current) => {
      if (command === 'discard-draft')
        return current.filter((row) => row.caseId !== target.caseId)

      return current.map((row) => {
        if (row.caseId !== target.caseId) return row
        const version = row.currentVersion + 1
        switch (command) {
          case 'confirm-draft':
            return {
              ...row,
              revisionStatus: 'Confirmed',
              rfqStatus: 'Active',
              quoteStatus: 'Requested',
              quoteRequestReason: 'Initial',
              currentVersion: version,
              version: row.version + 1,
            }
          case 'present':
            return { ...row, rfqStatus: 'Presented', currentVersion: version }
          case 'unpresent':
            return { ...row, rfqStatus: 'Active', currentVersion: version }
          case 'hit':
          case 'away':
            return {
              ...row,
              rfqStatus: command === 'hit' ? 'Hit' : 'Away',
              quoteStatus: null,
              quoteRequestReason: null,
              currentQuoteId: null,
              closedQuoteId: row.currentQuoteId,
              currentVersion: version,
            }
          case 'cancel':
            return { ...row, rfqStatus: 'Cancelled', currentVersion: version }
          case 'reopen':
            return {
              ...row,
              rfqStatus: 'Active',
              quoteStatus: 'Requested',
              quoteRequestReason: 'Reopened',
              currentVersion: version,
            }
          case 'confirm-amendment':
            return reflectConfirmedAmendment(row)
          case 'discard-amendment':
            return {
              ...row,
              draftRevisionId: null,
              draftVersion: null,
              draftNotional: null,
              draftSettlementDate: null,
              draftSalesAndTradingMessage: null,
              currentVersion: version,
            }
          default:
            return row
        }
      })
    })
  }

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
    create: (request) =>
      createDraft(request)
        .unwrap()
        .then(() => undefined),
    update: (caseId, body) =>
      updateDraft({ caseId, body })
        .unwrap()
        .then(() => undefined),
    confirmNew: (request) =>
      confirmNewRfq(request)
        .unwrap()
        .then(() => undefined),
    confirm: (caseId, body) =>
      confirmDraft({ caseId, body })
        .unwrap()
        .then(() => undefined),
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
    createFromExisting: (caseId) =>
      createFromExisting(caseId)
        .unwrap()
        .then(() => undefined),
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
    save: (row, notional, settlementDate, text) =>
      saveAmendment({
        caseId: row.caseId,
        notional,
        settlementDate,
        salesAndTradingMessage: text,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion ?? null,
      })
        .unwrap()
        .then(() => undefined),
    confirm: (row) =>
      confirmAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      })
        .unwrap()
        .then(() => undefined),
    discard: (row) =>
      discardAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      })
        .unwrap()
        .then(() => undefined),
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
      rfqs={visibleRfqs}
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
        rfqsQuery.isLoading || (refreshMode === 'live' && rfqsQuery.isFetching)
      }
      isError={rfqsQuery.isError}
      isMutating={mutationStates.some((state) => state.isLoading)}
      recentRevisions={recentQuery.data ?? []}
      recentRevisionsLoading={recentQuery.isLoading || recentQuery.isFetching}
      remoteUpdatePending={deferredUpdate}
      refreshMode={refreshMode}
      pendingUpdateCount={pendingUpdateCount}
      onRefreshModeChange={changeRefreshMode}
      onManualRefresh={catchUp}
      onRowActionApplied={applyRowAction}
      onTransientStateChange={setProtectedState}
      lookup={lookup}
      draft={draft}
      lifecycle={lifecycle}
      amendment={amendment}
      contactOwner={contactOwner}
      memo={memo}
      bulk={bulk}
      onReload={catchUp}
      gridLayout={gridLayout}
    />
  )
}
