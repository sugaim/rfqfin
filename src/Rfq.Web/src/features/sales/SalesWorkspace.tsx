import { useEffect, useState } from 'react'
import { message } from 'antd'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '../../app/App'
import {
  useBulkCloseAwayRfqsMutation,
  useBulkConfirmAmendmentsMutation,
  useBulkDiscardAmendmentsMutation,
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
  type ClientSearchResult,
  type SecuritySearchResult,
} from '../../services/api'
import { SalesScreen } from './SalesScreen'

type RfqOutcome = 'Hit' | 'Away'
type CloseItem = { caseId: number; expectedCurrentVersion: number }

export function SalesWorkspace() {
  const { currentUserId, refreshToken } = useOutletContext<AppOutletContext>()
  const rfqsQuery = useGetActiveSalesRfqsQuery()
  const tradersQuery = useGetAssignableTradersQuery()
  const usersQuery = useGetContactOwnerCandidatesQuery()
  const gridConfigQuery = useGetGridConfigQuery({ screenId: 'sales', configKey: 'main' })
  const [createDraft, createState] = useCreateDraftMutation()
  const [updateDraft, updateState] = useUpdateDraftMutation()
  const [confirmNewRfq, confirmNewState] = useConfirmNewRfqMutation()
  const [confirmDraft, confirmState] = useConfirmDraftMutation()
  const [discardDraft, discardState] = useDiscardDraftMutation()
  const [presentQuote, presentState] = usePresentQuoteMutation()
  const [unpresentQuote, unpresentState] = useUnpresentQuoteMutation()
  const [closeHitRfq, closeHitState] = useCloseHitRfqMutation()
  const [closeAwayRfq, closeAwayState] = useCloseAwayRfqMutation()
  const [bulkCloseAwayRfqs, bulkCloseState] = useBulkCloseAwayRfqsMutation()
  const [correctOutcomeToHit, correctToHitState] = useCorrectOutcomeToHitMutation()
  const [correctOutcomeToAway, correctToAwayState] = useCorrectOutcomeToAwayMutation()
  const [changeContactOwner, changeContactOwnerState] = useChangeContactOwnerMutation()
  const [updateSalesMemo, updateSalesMemoState] = useUpdateSalesMemoMutation()
  const [saveAmendment, saveAmendmentState] = useSaveAmendmentMutation()
  const [confirmAmendment, confirmAmendmentState] = useConfirmAmendmentMutation()
  const [discardAmendment, discardAmendmentState] = useDiscardAmendmentMutation()
  const [createFromExisting, createFromExistingState] = useCreateFromExistingMutation()
  const [cancelRfq, cancelState] = useCancelRfqMutation()
  const [reopenRfq, reopenState] = useReopenRfqMutation()
  const [bulkConfirmAmendments, bulkConfirmState] = useBulkConfirmAmendmentsMutation()
  const [bulkDiscardAmendments, bulkDiscardState] = useBulkDiscardAmendmentsMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const [searchClients] = useLazySearchClientsQuery()
  const [searchSecurities] = useLazySearchSecuritiesQuery()
  const [resolveDefaults] = useLazyResolveRfqCreationContextQuery()
  const [clients, setClients] = useState<ClientSearchResult[]>([])
  const [securities, setSecurities] = useState<SecuritySearchResult[]>([])

  useEffect(() => {
    if (refreshToken > 0) void rfqsQuery.refetch()
  }, [refreshToken, rfqsQuery.refetch])

  const mutationStates = [
    createState, updateState, confirmNewState, confirmState, discardState,
    presentState, unpresentState, closeHitState, closeAwayState, bulkCloseState,
    correctToHitState, correctToAwayState, changeContactOwnerState,
    updateSalesMemoState, saveAmendmentState, confirmAmendmentState,
    discardAmendmentState, createFromExistingState, cancelState, reopenState,
    bulkConfirmState, bulkDiscardState,
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
    <SalesScreen
      rfqs={rfqsQuery.data ?? []}
      clients={clients}
      securities={securities}
      traders={traders}
      users={users}
      currentUserId={currentUserId}
      isLoading={rfqsQuery.isLoading || rfqsQuery.isFetching}
      isError={rfqsQuery.isError}
      isMutating={mutationStates.some((state) => state.isLoading)}
      onClientSearch={async (query) => setClients(
        query.trim() ? await searchClients(query).unwrap() : [])}
      onSecuritySearch={async (query) => setSecurities(
        query.trim() ? await searchSecurities(query).unwrap() : [])}
      onResolveDefaults={(securityId) => resolveDefaults({ securityId }).unwrap()}
      onCreate={(request) => createDraft(request).unwrap().then(() => undefined)}
      onUpdate={(caseId, body) => updateDraft({ caseId, body }).unwrap().then(() => undefined)}
      onConfirmNew={(request) => confirmNewRfq(request).unwrap().then(() => undefined)}
      onConfirmDraft={(caseId, body) => confirmDraft({ caseId, body }).unwrap().then(() => undefined)}
      onDiscard={(caseId, expectedVersion) => discardDraft({ caseId, expectedVersion }).unwrap()}
      onPresent={(caseId, expectedCurrentVersion) =>
        presentQuote({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
      onUnpresent={(caseId, expectedCurrentVersion) =>
        unpresentQuote({ caseId, expectedCurrentVersion }).unwrap().then(() => undefined)}
      onClose={closeCase}
      onBulkClose={(items: CloseItem[]) => bulkCloseAwayRfqs({ items }).unwrap()}
      onCorrectOutcome={correctOutcome}
      onChangeContactOwner={handOffContactOwner}
      onUpdateMemo={(caseId, memo, expectedVersion) =>
        updateSalesMemo({ caseId, memo, expectedVersion }).unwrap().then(() => undefined)}
      onSaveAmendment={(row, notional, settlementDate, salesAndTradingMessage) =>
        saveAmendment({
          caseId: row.caseId, notional, settlementDate, salesAndTradingMessage,
          expectedCurrentVersion: row.currentVersion,
          expectedDraftVersion: row.draftVersion ?? null,
        }).unwrap().then(() => undefined)}
      onConfirmAmendment={(row) => confirmAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      }).unwrap().then(() => undefined)}
      onDiscardAmendment={(row) => discardAmendment({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
        expectedDraftVersion: row.draftVersion!,
      }).unwrap().then(() => undefined)}
      onCreateFromExisting={(caseId) =>
        createFromExisting(caseId).unwrap().then(() => undefined)}
      onCancel={(row) => cancelRfq({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap().then(() => undefined)}
      onReopen={(row) => reopenRfq({
        caseId: row.caseId,
        expectedCurrentVersion: row.currentVersion,
      }).unwrap().then(() => undefined)}
      onBulkConfirmAmendments={(rows) => bulkConfirmAmendments({
        items: rows.map((row) => ({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
          expectedDraftVersion: row.draftVersion!,
        })),
      }).unwrap().then((results) => {
        void message.info(results.map((item) =>
          `Case ${item.caseId}: ${item.status}${item.message ? ` (${item.message})` : ''}`,
        ).join('; '))
      })}
      onBulkDiscardAmendments={(rows) => bulkDiscardAmendments({
        items: rows.map((row) => ({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
          expectedDraftVersion: row.draftVersion!,
        })),
      }).unwrap().then((results) => {
        void message.info(results.map((item) =>
          `Case ${item.caseId}: ${item.status}${item.message ? ` (${item.message})` : ''}`,
        ).join('; '))
      })}
      gridConfigJson={gridConfigQuery.data
        ? JSON.stringify(gridConfigQuery.data.config)
        : undefined}
      onSaveGridConfig={(configJson) => saveGridConfig({
        screenId: 'sales',
        configKey: 'main',
        version: 1,
        config: JSON.parse(configJson),
      }).unwrap().then(() => undefined)}
      onReload={rfqsQuery.refetch}
    />
  )
}
