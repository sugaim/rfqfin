import { useCallback, useEffect, useRef, useState } from 'react'
import { message } from 'antd'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '../../app/App'
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
} from '../../services/api'
import { SalesScreen } from './SalesScreen'
import type { SalesBulkCommand } from './salesModel'

export function SalesWorkspace() {
  const { currentUserId, remoteChangeVersion, acknowledgeRemoteChanges } = useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveSalesRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const gridConfigQuery = useGetGridConfigQuery({ screenId: 'sales', configKey: 'main' })
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
  const [changeContactOwner, changeContactOwnerState] = useChangeContactOwnerMutation()
  const [reopenRfq, reopenState] = useReopenRfqMutation()
  const [createFromExisting, createFromExistingState] = useCreateFromExistingMutation()
  const [updateSalesMemo, updateSalesMemoState] = useUpdateSalesMemoMutation()
  const [saveAmendment, saveAmendmentState] = useSaveAmendmentMutation()
  const [confirmAmendment, confirmAmendmentState] = useConfirmAmendmentMutation()
  const [discardAmendment, discardAmendmentState] = useDiscardAmendmentMutation()
  const [bulkAway, bulkAwayState] = useBulkCloseAwayRfqsMutation()
  const [bulkCancel, bulkCancelState] = useBulkCancelRfqsMutation()
  const [bulkPresent, bulkPresentState] = useBulkPresentRfqsMutation()
  const [bulkUnpresent, bulkUnpresentState] = useBulkUnpresentRfqsMutation()
  const [bulkConfirmDrafts, bulkConfirmDraftsState] = useBulkConfirmInitialDraftsMutation()
  const [bulkDiscardDrafts, bulkDiscardDraftsState] = useBulkDiscardInitialDraftsMutation()
  const [bulkConfirmAmendments, bulkConfirmAmendmentsState] = useBulkConfirmAmendmentsMutation()
  const [bulkDiscardAmendments, bulkDiscardAmendmentsState] = useBulkDiscardAmendmentsMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqCreationContextQuery()
  const [clients, setClients] = useState<ClientSearchResult[]>([])
  const [securities, setSecurities] = useState<SecuritySearchResult[]>([])
  const [protectedState, setProtectedState] = useState(false)
  const [deferredUpdate, setDeferredUpdate] = useState(false)
  const observedRemoteVersion = useRef(remoteChangeVersion)

  const catchUp = useCallback(async () => {
    await Promise.all([rfqsQuery.refetch(), recentQuery.refetch()])
    await acknowledgeRemoteChanges()
    setDeferredUpdate(false)
  }, [acknowledgeRemoteChanges, recentQuery.refetch, rfqsQuery.refetch])

  useEffect(() => {
    if (remoteChangeVersion === observedRemoteVersion.current) return
    observedRemoteVersion.current = remoteChangeVersion
    if (protectedState) setDeferredUpdate(true)
    else void catchUp()
  }, [catchUp, protectedState, remoteChangeVersion])

  useEffect(() => {
    if (!protectedState && deferredUpdate) void catchUp()
  }, [catchUp, deferredUpdate, protectedState])

  const lifecycleItems = (rows: SalesRfq[]) => ({ items: rows.map((row) => ({
    caseId: row.caseId, expectedCurrentVersion: row.currentVersion,
  })) })

  const runBulk = async (command: SalesBulkCommand, rows: SalesRfq[]): Promise<BulkItemResult[]> => {
    switch (command) {
      case 'away': return bulkAway(lifecycleItems(rows)).unwrap()
      case 'cancel': return bulkCancel(lifecycleItems(rows)).unwrap()
      case 'present': return bulkPresent(lifecycleItems(rows)).unwrap()
      case 'unpresent': return bulkUnpresent(lifecycleItems(rows)).unwrap()
      case 'confirm-drafts': return bulkConfirmDrafts({ items: rows.map((row) => ({
        caseId: row.caseId,
        notional: row.notional,
        settlementDate: row.settlementDate!,
        standardSettlementDate: row.standardSettlementDate,
        salesAndTradingMessage: row.salesAndTradingMessage,
        assignedTraderId: row.assignedTraderId,
        expectedVersion: row.version,
      })) }).unwrap()
      case 'discard-drafts': return bulkDiscardDrafts({ items: rows.map((row) => ({
        caseId: row.caseId, expectedVersion: row.version,
      })) }).unwrap()
      case 'confirm-amendments': return bulkConfirmAmendments({ items: rows.map((row) => ({
        caseId: row.caseId, expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      })) }).unwrap()
      case 'discard-amendments': return bulkDiscardAmendments({ items: rows.map((row) => ({
        caseId: row.caseId, expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      })) }).unwrap()
    }
  }

  const mutationStates = [
    createState, updateState, confirmNewState, confirmState, discardState,
    presentState, unpresentState, closeHitState, closeAwayState, correctToHitState,
    correctToAwayState, cancelState, changeContactOwnerState,
    reopenState, createFromExistingState, updateSalesMemoState, saveAmendmentState,
    confirmAmendmentState, discardAmendmentState, bulkAwayState, bulkCancelState,
    bulkPresentState, bulkUnpresentState, bulkConfirmDraftsState, bulkDiscardDraftsState,
    bulkConfirmAmendmentsState, bulkDiscardAmendmentsState,
  ]

  return <SalesScreen
    rfqs={rfqsQuery.data ?? []}
    clients={clients}
    securities={securities}
    traders={(tradersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))}
    users={(usersQuery.data ?? []).map((user) => ({ userId: user.userId, name: user.name }))}
    currentUserId={currentUserId}
    isLoading={rfqsQuery.isLoading || rfqsQuery.isFetching}
    isError={rfqsQuery.isError}
    isMutating={mutationStates.some((state) => state.isLoading)}
    recentRevisions={recentQuery.data ?? []}
    recentRevisionsLoading={recentQuery.isLoading || recentQuery.isFetching}
    remoteUpdatePending={deferredUpdate}
    onTransientStateChange={setProtectedState}
    onClientSearch={async (query) => setClients(query.trim() ? await searchClients(query).unwrap() : [])}
    onSecuritySearch={async (query) => setSecurities(query.trim() ? await searchSecurities(query).unwrap() : [])}
    onResolveDefaults={(securityId) => resolveDefaults({ securityId }).unwrap()}
    onCreate={(request) => createDraft(request).unwrap().then(() => undefined)}
    onUpdate={(caseId, body) => updateDraft({ caseId, body }).unwrap().then(() => undefined)}
    onConfirmNew={(request) => confirmNewRfq(request).unwrap().then(() => undefined)}
    onConfirmDraft={(caseId, body) => confirmDraft({ caseId, body }).unwrap().then(() => undefined)}
    onDiscard={(caseId, expectedVersion) => discardDraft({ caseId, expectedVersion }).unwrap()}
    onPresent={(caseId, expectedCurrentVersion) => presentQuote({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
    onUnpresent={(caseId, expectedCurrentVersion) => unpresentQuote({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
    onClose={(caseId, outcome, expectedCurrentVersion) => (outcome === 'Hit' ? closeHitRfq : closeAwayRfq)({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
    onCorrectOutcome={(caseId, outcome, expectedCurrentVersion) => (outcome === 'Hit' ? correctToHit : correctToAway)({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
    onCancel={(row) => cancelRfq({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion }).unwrap().then(() => undefined)}
    onChangeContactOwner={(caseId, targetUserId, expectedCurrentVersion) => changeContactOwner({
      caseId, targetUserId, expectedCurrentVersion, confirmed: true,
    }).unwrap().then(() => undefined)}
    onReopen={(row) => reopenRfq({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion }).unwrap().then(() => undefined)}
    onCreateFromExisting={(caseId) => createFromExisting(caseId).unwrap().then(() => undefined)}
    onUpdateMemo={(caseId, memo, expectedVersion) => updateSalesMemo({ caseId, memo, expectedVersion }).unwrap().then(() => undefined)}
    onSaveAmendment={(row, notional, settlementDate, text) => saveAmendment({ caseId: row.caseId, notional, settlementDate, salesAndTradingMessage: text, expectedCurrentVersion: row.currentVersion, expectedDraftVersion: row.draftVersion ?? null }).unwrap().then(() => undefined)}
    onConfirmAmendment={(row) => confirmAmendment({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion, expectedDraftVersion: row.draftVersion! }).unwrap().then(() => undefined)}
    onDiscardAmendment={(row) => discardAmendment({ caseId: row.caseId, expectedCurrentVersion: row.currentVersion, expectedDraftVersion: row.draftVersion! }).unwrap().then(() => undefined)}
    onBulk={async (command, rows) => {
      const results = await runBulk(command, rows)
      void message.info(results.map((item) => `Case ${item.caseId}: ${item.status}`).join('; '))
      return results
    }}
    onReload={async () => { await Promise.all([rfqsQuery.refetch(), recentQuery.refetch()]) }}
    gridConfigJson={gridConfigQuery.data ? JSON.stringify(gridConfigQuery.data.config) : undefined}
    onSaveGridConfig={(configJson) => saveGridConfig({ screenId: 'sales', configKey: 'main', version: (gridConfigQuery.data?.version ?? 0) + 1, config: JSON.parse(configJson) }).unwrap().then(() => undefined)}
  />
}
