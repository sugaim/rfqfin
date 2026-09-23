import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'
import {
  useCancelRfqsMutation,
  useCloseAwayRfqsMutation,
  useConfirmAmendmentsMutation,
  useConfirmInitialDraftsMutation,
  useDiscardAmendmentsMutation,
  useDiscardInitialDraftsMutation,
  usePresentRfqsMutation,
  useUnpresentRfqsMutation,
  useChangeContactOwnersMutation,
  useCloseHitRfqMutation,
  useConfirmNewRfqMutation,
  useCorrectOutcomeToAwayMutation,
  useCorrectOutcomeToHitMutation,
  useCreateDraftMutation,
  useCreateFromExistingMutation,
  useGetActiveSalesRfqsQuery,
  useGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useGetGridConfigQuery,
  useLazyGetSalesRecentRevisionsQuery,
  useLazyResolveRfqCreationContextQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
  useReopenRfqsMutation,
  useSaveAmendmentMutation,
  useSaveGridConfigMutation,
  useUpdateInitialDraftMutation,
  useUpdateSalesMemoMutation,
  useStartAmendmentMutation,
  type CaseOperationResponse,
  type ClientCandidateResponse,
  type SalesRfqResponse,
  type SecurityCandidateResponse,
} from '@/generated/rfqApi'
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
import { unwrapApiResult } from '@/services/apiProblem'

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
  const [updateDraft, updateState] = useUpdateInitialDraftMutation()
  const [confirmNewRfq, confirmNewState] = useConfirmNewRfqMutation()
  const [confirmDraft, confirmState] = useConfirmInitialDraftsMutation()
  const [discardDraft, discardState] = useDiscardInitialDraftsMutation()
  const [presentQuote, presentState] = usePresentRfqsMutation()
  const [unpresentQuote, unpresentState] = useUnpresentRfqsMutation()
  const [closeHitRfq, closeHitState] = useCloseHitRfqMutation()
  const [closeAwayRfq, closeAwayState] = useCloseAwayRfqsMutation()
  const [correctToHit, correctToHitState] = useCorrectOutcomeToHitMutation()
  const [correctToAway, correctToAwayState] = useCorrectOutcomeToAwayMutation()
  const [cancelRfq, cancelState] = useCancelRfqsMutation()
  const [changeContactOwner, changeContactOwnerState] =
    useChangeContactOwnersMutation()
  const [reopenRfq, reopenState] = useReopenRfqsMutation()
  const [createFromExisting, createFromExistingState] =
    useCreateFromExistingMutation()
  const [updateSalesMemo, updateSalesMemoState] = useUpdateSalesMemoMutation()
  const [saveAmendment, saveAmendmentState] = useSaveAmendmentMutation()
  const [startAmendment, startAmendmentState] = useStartAmendmentMutation()
  const [confirmAmendment, confirmAmendmentState] =
    useConfirmAmendmentsMutation()
  const [discardAmendment, discardAmendmentState] =
    useDiscardAmendmentsMutation()
  const [bulkAway, bulkAwayState] = useCloseAwayRfqsMutation()
  const [bulkCancel, bulkCancelState] = useCancelRfqsMutation()
  const [bulkPresent, bulkPresentState] = usePresentRfqsMutation()
  const [bulkUnpresent, bulkUnpresentState] = useUnpresentRfqsMutation()
  const [bulkConfirmDrafts, bulkConfirmDraftsState] =
    useConfirmInitialDraftsMutation()
  const [bulkDiscardDrafts, bulkDiscardDraftsState] =
    useDiscardInitialDraftsMutation()
  const [bulkConfirmAmendments, bulkConfirmAmendmentsState] =
    useConfirmAmendmentsMutation()
  const [bulkDiscardAmendments, bulkDiscardAmendmentsState] =
    useDiscardAmendmentsMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqCreationContextQuery()
  const [clients, setClients] = useState<ClientCandidateResponse[]>([])
  const [securities, setSecurities] = useState<SecurityCandidateResponse[]>([])
  const [protectedState, setProtectedState] = useState(false)
  const [recentOpen, setRecentOpen] = useState(false)
  const [recentGeneration, setRecentGeneration] = useState(1)
  const [loadedRecentGeneration, setLoadedRecentGeneration] = useState(0)
  const refresh = useLivePausedRows({
    authoritativeRows: rfqsQuery.data,
    remoteChangeVersion: salesChangeVersion,
    protectedState,
    refetch: rfqsQuery.refetch,
    keyOf: (row: SalesRfqResponse) => row.caseId,
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
    void loadRecent({})
      .unwrap()
      .then(() => setLoadedRecentGeneration(requestedGeneration))
  }, [
    loadRecent,
    loadedRecentGeneration,
    recentGeneration,
    recentOpen,
    recentQuery.isFetching,
  ])

  const lifecycleItems = (rows: SalesRfqResponse[]) => ({
    items: rows.map((row) => ({
      caseId: row.caseId,
      expectedCurrentVersion: row.currentVersion,
    })),
  })

  const runBulk = async (
    command: SalesBulkCommand,
    rows: SalesRfqResponse[],
  ): Promise<CaseOperationResponse[]> => {
    switch (command) {
      case 'away':
        return unwrapApiResult(
          bulkAway({ closeAwayRfqsRequest: lifecycleItems(rows) }),
        )
      case 'cancel':
        return unwrapApiResult(
          bulkCancel({ cancelRfqsRequest: lifecycleItems(rows) }),
        )
      case 'present':
        return unwrapApiResult(
          bulkPresent({ presentRfqsRequest: lifecycleItems(rows) }),
        )
      case 'unpresent':
        return unwrapApiResult(
          bulkUnpresent({ unpresentRfqsRequest: lifecycleItems(rows) }),
        )
      case 'confirm-drafts':
        return unwrapApiResult(
          bulkConfirmDrafts({
            confirmInitialDraftsRequest: {
              items: rows.map((row) => ({
                caseId: row.caseId,
                notional: row.notional,
                settlementDate: row.settlementDate,
                standardSettlementDate: row.standardSettlementDate,
                salesAndTradingMessage: row.salesAndTradingMessage,
                assignedTraderId: row.assignedTraderId,
                expectedCurrentVersion: row.version,
              })),
            },
          }),
        )
      case 'discard-drafts':
        return unwrapApiResult(
          bulkDiscardDrafts({
            discardInitialDraftsRequest: {
              items: rows.map((row) => ({
                caseId: row.caseId,
                expectedCurrentVersion: row.version,
              })),
            },
          }),
        )
      case 'confirm-amendments':
        return unwrapApiResult(
          bulkConfirmAmendments({
            confirmAmendmentsRequest: {
              items: rows.map((row) => ({
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
                expectedDraftVersion: row.draftVersion!,
              })),
            },
          }),
        )
      case 'discard-amendments':
        return unwrapApiResult(
          bulkDiscardAmendments({
            discardAmendmentsRequest: {
              items: rows.map((row) => ({
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
                expectedDraftVersion: row.draftVersion!,
              })),
            },
          }),
        )
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
      setClients(
        query.trim() ? await unwrapApiResult(searchClients({ q: query })) : [],
      ),
    searchSecurities: async (query) =>
      setSecurities(
        query.trim()
          ? await unwrapApiResult(searchSecurities({ q: query }))
          : [],
      ),
    resolveDefaults: (securityId) =>
      unwrapApiResult(resolveDefaults({ securityId })),
  }
  const draft: SalesDraftActions = {
    create: (request) =>
      unwrapApiResult(createDraft({ createDraftRequest: request })),
    update: (caseId, body) =>
      unwrapApiResult(updateDraft({ caseId, updateDraftRequest: body })),
    confirmNew: (request) =>
      unwrapApiResult(confirmNewRfq({ createDraftRequest: request })),
    confirm: (caseId, body) =>
      unwrapApiResult(
        confirmDraft({
          confirmInitialDraftsRequest: { items: [{ caseId, ...body }] },
        }),
      ).then(() => undefined),
    discard: (caseId, expectedVersion) =>
      unwrapApiResult(
        discardDraft({
          discardInitialDraftsRequest: {
            items: [{ caseId, expectedCurrentVersion: expectedVersion }],
          },
        }),
      ).then(() => undefined),
  }
  const lifecycle: SalesLifecycleActions = {
    present: (caseId, expectedCurrentVersion) =>
      unwrapApiResult(
        presentQuote({
          presentRfqsRequest: { items: [{ caseId, expectedCurrentVersion }] },
        }),
      ).then(() => undefined),
    unpresent: (caseId, expectedCurrentVersion) =>
      unwrapApiResult(
        unpresentQuote({
          unpresentRfqsRequest: { items: [{ caseId, expectedCurrentVersion }] },
        }),
      ).then(() => undefined),
    close: (caseId, outcome, expectedCurrentVersion) =>
      (outcome === 'Hit'
        ? unwrapApiResult(
            closeHitRfq({
              caseId,
              closeHitRfqRequest: { expectedCurrentVersion },
            }),
          )
        : unwrapApiResult(
            closeAwayRfq({
              closeAwayRfqsRequest: {
                items: [{ caseId, expectedCurrentVersion }],
              },
            }),
          )
      ).then(() => undefined),
    correctOutcome: (caseId, outcome, expectedCurrentVersion, reason) =>
      unwrapApiResult(
        outcome === 'Hit'
          ? correctToHit({
              caseId,
              correctOutcomeToHitRequest: { expectedCurrentVersion, reason },
            })
          : correctToAway({
              caseId,
              correctOutcomeToAwayRequest: { expectedCurrentVersion, reason },
            }),
      ).then(() => undefined),
    cancel: (row) =>
      unwrapApiResult(
        cancelRfq({
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
        reopenRfq({
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
    createFromExisting: (caseId) =>
      unwrapApiResult(createFromExisting({ caseId })),
  }
  const contactOwner: SalesContactOwnerActions = {
    change: (caseId, targetUserId, expectedCurrentVersion) =>
      unwrapApiResult(
        changeContactOwner({
          changeContactOwnersRequest: {
            targetContactOwnerId: targetUserId,
            confirmed: true,
            items: [{ caseId, expectedCurrentVersion }],
          },
        }),
      ).then(() => undefined),
  }
  const memo: SalesMemoActions = {
    update: (caseId, value, expectedVersion) =>
      unwrapApiResult(
        updateSalesMemo({
          caseId,
          updateMemoRequest: {
            memo: value,
            expectedMemoVersion: expectedVersion,
          },
        }),
      ).then(() => undefined),
  }
  const amendment: SalesAmendmentActions = {
    start: (row) =>
      unwrapApiResult(
        startAmendment({
          caseId: row.caseId,
          startAmendmentRequest: {
            expectedCurrentVersion: row.currentVersion,
          },
        }),
      ),
    save: (
      caseId,
      notional,
      settlementDate,
      text,
      expectedCurrentVersion,
      expectedDraftVersion,
    ) =>
      unwrapApiResult(
        saveAmendment({
          caseId,
          saveAmendmentRequest: {
            notional,
            settlementDate,
            salesAndTradingMessage: text,
            expectedCurrentVersion,
            expectedDraftVersion,
          },
        }),
      ),
    confirm: (row) =>
      unwrapApiResult(
        confirmAmendment({
          confirmAmendmentsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
                expectedDraftVersion: row.draftVersion!,
              },
            ],
          },
        }),
      ).then(() => undefined),
    discard: (row) =>
      unwrapApiResult(
        discardAmendment({
          discardAmendmentsRequest: {
            items: [
              {
                caseId: row.caseId,
                expectedCurrentVersion: row.currentVersion,
                expectedDraftVersion: row.draftVersion!,
              },
            ],
          },
        }),
      ).then(() => undefined),
  }
  const bulk: SalesBulkActions = { execute: runBulk }
  const gridLayout: SalesGridLayoutActions = {
    configJson: gridConfigQuery.data
      ? JSON.stringify(gridConfigQuery.data.config)
      : undefined,
    save: (configJson) =>
      unwrapApiResult(
        saveGridConfig({
          screenId: 'sales',
          configKey: 'main',
          gridConfigRequest: {
            version: (gridConfigQuery.data?.version ?? 0) + 1,
            config: JSON.parse(configJson),
          },
        }),
      ).then(async () => {
        await gridConfigQuery
          .refetch()
          .unwrap()
          .catch(() => undefined)
      }),
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
