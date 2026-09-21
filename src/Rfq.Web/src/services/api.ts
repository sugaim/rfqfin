import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

export interface HealthResponse {
  status: string
}

export interface CreateDraftRequest {
  clientId: string
  securityId: string
  notional?: number
  settlementDate: string
  standardSettlementDate: string
  salesAndTradingMessage: string
  assignedTraderId: string
}

export interface UpdateDraftRequest {
  notional?: number
  settlementDate: string
  standardSettlementDate: string
  salesAndTradingMessage: string
  assignedTraderId: string
  expectedVersion: number
}

export interface InitialRfqResponse {
  caseId: number
  revisionId: string
  rfqStatus: string
  revisionStatus: string
  quoteStatus: string | null
  quoteRequestReason: string | null
  categoryId: string
  contactOwnerId: string
  assignedTraderId: string
  notional: number | null
  settlementDate: string | null
  standardSettlementDate: string
  salesAndTradingMessage: string
  version: number
  createdAt: string
}

export interface SalesRfq {
  caseId: number
  clientId: string
  clientName: string
  securityId: string
  securityJapaneseName: string
  securityBbgDisplay: string
  categoryId: string
  rfqStatus: string
  quoteStatus: string | null
  quoteRequestReason: string | null
  currentRevisionId: string
  currentQuoteId: string | null
  closedQuoteId: string | null
  currentVersion: number
  revisionStatus: string
  contactOwnerId: string
  assignedTraderId: string
  settlementDate: string | null
  standardSettlementDate: string
  notional: number | null
  salesAndTradingMessage: string
  salesMemo: string
  salesMemoVersion: number
  version: number
  createdAt: string
  draftRevisionId?: string | null
  draftVersion?: number | null
  draftSettlementDate?: string | null
  draftNotional?: number | null
  draftSalesAndTradingMessage?: string | null
}

export interface AmendmentResult { caseId: number; currentRevisionId: string; draftRevisionId: string | null; currentVersion: number; draftVersion: number | null; rfqStatus: string; quoteStatus: string | null; quoteRequestReason: string | null }
export interface BulkItemResult { caseId: number; status: 'Succeeded' | 'Skipped' | 'Failed'; code: string | null; message: string | null }
export interface LifecycleResult { caseId: number; rfqStatus: string; quoteStatus: string | null; quoteRequestReason: string | null; currentVersion: number }
export interface PersistedEvent { type: string; eventId: number; occurredAt: string; actorUserId: string | null; caseId: number; quoteId?: string; [key: string]: unknown }
export interface EodSummary { contactOwnerId: string; open: number; hit: number; away: number }
export interface RfqSearchItem { caseId: number; createdAt: string; clientId: string; clientName: string; securityId: string; securityName: string; categoryId: string; status: string; quoteStatus: string | null; contactOwnerId: string; salesId: string | null; assignedTraderId: string; notional: number | null; settlementDate: string | null }
export interface RfqSearchResult { items: RfqSearchItem[]; requiresNarrowing: boolean }
export interface GridConfig { screenId: string; configKey: string; version: number; config: unknown; updatedAt: string }

export interface SecuritySearchResult {
  securityId: string
  japaneseName: string
  bbgDisplay: string
  internalCode: string
  isin: string
  categoryId: string
  categoryName: string
}

export interface ClientSearchResult {
  clientId: string
  code: string
  name: string
}

export interface UserSummary {
  userId: string
  name: string
}

export interface RfqCreationContext {
  categoryId: string
  categoryName: string
  defaultAssignedTraderId: string
  defaultAssignedTraderName: string
  standardSettlementDate: string
}

export interface BusinessDateResponse {
  date: string
}

export interface CurrentUserResponse {
  userId: string
  roles: string[]
  deskId: string
}

export type QuoteExpiry =
  | { type: 'None'; minutes: null }
  | { type: 'After'; minutes: number }

export interface TraderRfq {
  caseId: number
  clientId: string
  clientName: string
  securityId: string
  securityJapaneseName: string
  securityBbgDisplay: string
  categoryId: string
  rfqStatus: string
  quoteStatus: string | null
  quoteRequestReason: string | null
  currentRevisionId: string
  currentQuoteId: string | null
  closedQuoteId: string | null
  confirmedAt: string | null
  expiresAt: string | null
  quoteSeedRevisionId: string | null
  contactOwnerId: string
  assignedTraderId: string
  owned: boolean
  currentVersion: number
  settlementDate: string | null
  notional: number | null
  workingQuoteMode: 'Calculated' | 'Manual'
  calculated: CalculatedQuotePayload | null
  manual: ManualQuotePayload | null
  workingQuoteVersion: number
  traderMemo: string
  traderMemoVersion: number
  createdAt: string
}

export interface CalculatedQuotePayload {
  driver: 'Price' | 'BbgYield' | 'SimpleYield' | 'GSpread'
  driverValue: number
  price: number
  bbgYield: number
  baseSimpleYield: number
  simpleYieldSlide: number
  finalSimpleYield: number
  internalYield: number
  gSpread: number
  asw: number
}

export interface ManualQuotePayload {
  price: number | null
  finalSimpleYield: number | null
}

export interface WorkingQuoteResult {
  caseId: number
  revisionId: string
  mode: 'Calculated' | 'Manual'
  calculated: CalculatedQuotePayload | null
  manual: ManualQuotePayload | null
  version: number
  currentVersion: number
}

export interface OwnershipResult {
  caseId: number
  assignedTraderId: string
  owned: boolean
  currentVersion: number
}

export interface ConfirmQuoteResult {
  caseId: number
  quoteId: string
  revisionId: string
  rfqStatus: string
  quoteStatus: string
  mode: 'Calculated' | 'Manual'
  calculated: CalculatedQuotePayload | null
  manual: ManualQuotePayload | null
  confirmedAt: string
  expiry: QuoteExpiry
  expiresAt: string | null
  currentVersion: number
}

export interface PresentationResult {
  caseId: number
  quoteId: string
  rfqStatus: string
  quoteStatus: string
  currentVersion: number
}

export interface CloseRfqResult {
  caseId: number
  rfqStatus: 'Hit' | 'Away'
  closedQuoteId: string
  owned: boolean
  currentVersion: number
}

export interface ContactOwnerResult {
  caseId: number
  contactOwnerId: string
  currentVersion: number
}

export interface MemoResult {
  caseId: number
  memo: string
  version: number
}

export const api = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({
    baseUrl: '/api',
    prepareHeaders: (headers) => {
      const identity = window.localStorage.getItem('rfq-development-user')
      if (identity) headers.set('X-Development-User', identity)
      return headers
    },
  }),
  endpoints: (builder) => ({
    getHealth: builder.query<HealthResponse, void>({
      query: () => '/health',
    }),
    getBusinessDate: builder.query<BusinessDateResponse, void>({
      query: () => '/business-date',
    }),
    getMe: builder.query<CurrentUserResponse, void>({
      query: () => '/me',
    }),
    getActiveSalesRfqs: builder.query<SalesRfq[], void>({
      query: () => '/sales-rfqs',
    }),
    getEvents: builder.query<PersistedEvent[], number>({ query: (after) => ({ url: '/events', params: { after } }) }),
    getEod: builder.query<EodSummary[], string>({ query: (date) => ({ url: '/eod', params: { date } }) }),
    searchRfqs: builder.query<RfqSearchResult, void>({ query: () => '/rfqs/search' }),
    getGridConfig: builder.query<GridConfig, { screenId: string; configKey: string }>({ query: ({ screenId, configKey }) => `/me/grid-configs/${screenId}/${configKey}` }),
    saveGridConfig: builder.mutation<GridConfig, { screenId: string; configKey: string; version: number; config: unknown }>({ query: ({ screenId, configKey, ...body }) => ({ url: `/me/grid-configs/${screenId}/${configKey}`, method: 'PUT', body }) }),
    getQuoteExpiry: builder.query<QuoteExpiry, void>({ query: () => '/me/settings/quote-expiry' }),
    saveQuoteExpiry: builder.mutation<QuoteExpiry, QuoteExpiry>({ query: (body) => ({ url: '/me/settings/quote-expiry', method: 'PUT', body }) }),
    createDraft: builder.mutation<InitialRfqResponse, CreateDraftRequest>({
      query: (body) => ({
        url: '/rfqs/drafts',
        method: 'POST',
        body,
      }),
    }),
    updateDraft: builder.mutation<
      InitialRfqResponse,
      { caseId: number; body: UpdateDraftRequest }
    >({
      query: ({ caseId, body }) => ({
        url: `/rfqs/${caseId}/draft`,
        method: 'PUT',
        body,
      }),
    }),
    confirmNewRfq: builder.mutation<InitialRfqResponse, CreateDraftRequest>({
      query: (body) => ({
        url: '/rfqs/drafts/confirm',
        method: 'POST',
        body,
      }),
    }),
    confirmDraft: builder.mutation<
      InitialRfqResponse,
      { caseId: number; body: UpdateDraftRequest }
    >({
      query: ({ caseId, body }) => ({
        url: `/rfqs/${caseId}/draft/confirm`,
        method: 'POST',
        body,
      }),
    }),
    discardDraft: builder.mutation<void, { caseId: number; expectedVersion: number }>({
      query: ({ caseId, expectedVersion }) => ({
        url: `/rfqs/${caseId}/draft/discard`,
        method: 'POST',
        body: { expectedVersion },
      }),
    }),
    getActiveTraderRfqs: builder.query<TraderRfq[], void>({
      query: () => '/trader-rfqs',
    }),
    pickUpRfq: builder.mutation<
      OwnershipResult,
      { caseId: number; expectedVersion: number; confirmed: boolean }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/ownership/pick-up`,
        method: 'POST',
        body,
      }),
    }),
    releaseRfq: builder.mutation<
      OwnershipResult,
      { caseId: number; expectedVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/ownership/release`,
        method: 'POST',
        body,
      }),
    }),
    assignTrader: builder.mutation<
      OwnershipResult,
      { caseId: number; targetTraderId: string; expectedVersion: number }
    >({
      query: ({ caseId, targetTraderId, ...body }) => ({
        url: `/rfqs/${caseId}/assigned-trader`,
        method: 'PUT',
        body: { assignedTraderId: targetTraderId, ...body },
      }),
    }),
    takeOverRfq: builder.mutation<
      OwnershipResult,
      { caseId: number; expectedVersion: number; confirmed: boolean }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/ownership/take-over`,
        method: 'POST',
        body,
      }),
    }),
    calculateWorkingQuote: builder.mutation<
      WorkingQuoteResult,
      {
        caseId: number
        driver: string
        value: number
        simpleYieldSlide: number
        expectedCurrentVersion: number
        expectedWorkingQuoteVersion: number
      }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/working-quote/calculate`,
        method: 'PUT',
        body,
      }),
    }),
    changeWorkingQuoteMode: builder.mutation<
      WorkingQuoteResult,
      {
        caseId: number
        mode: 'Calculated' | 'Manual'
        expectedCurrentVersion: number
        expectedWorkingQuoteVersion: number
      }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/working-quote/mode`,
        method: 'PUT',
        body,
      }),
    }),
    updateManualWorkingQuote: builder.mutation<
      WorkingQuoteResult,
      {
        caseId: number
        price: number | null
        finalSimpleYield: number | null
        expectedCurrentVersion: number
        expectedWorkingQuoteVersion: number
      }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/working-quote/manual`,
        method: 'PUT',
        body,
      }),
    }),
    confirmQuote: builder.mutation<
      ConfirmQuoteResult,
      {
        caseId: number
        expiry: QuoteExpiry
        expectedCurrentVersion: number
        expectedWorkingQuoteVersion: number
      }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/quote/confirm`,
        method: 'POST',
        body,
      }),
    }),
    presentQuote: builder.mutation<
      PresentationResult,
      { caseId: number; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/present`,
        method: 'POST',
        body,
      }),
    }),
    unpresentQuote: builder.mutation<
      PresentationResult,
      { caseId: number; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/unpresent`,
        method: 'POST',
        body,
      }),
    }),
    closeHitRfq: builder.mutation<
      CloseRfqResult,
      { caseId: number; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/close/hit`,
        method: 'POST',
        body,
      }),
    }),
    closeAwayRfq: builder.mutation<
      CloseRfqResult,
      { caseId: number; expectedCurrentVersion: number }
    >({ query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/close/away`, method: 'POST', body }) }),
    bulkCloseAwayRfqs: builder.mutation<
      BulkItemResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({ url: '/rfqs/bulk-close-away', method: 'POST', body }),
    }),
    correctOutcomeToHit: builder.mutation<
      CloseRfqResult,
      { caseId: number; reason?: string; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/outcome/correct-to-hit`,
        method: 'POST',
        body,
      }),
    }),
    correctOutcomeToAway: builder.mutation<
      CloseRfqResult,
      { caseId: number; reason?: string; expectedCurrentVersion: number }
    >({ query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/outcome/correct-to-away`, method: 'POST', body }) }),
    changeContactOwner: builder.mutation<
      ContactOwnerResult,
      {
        caseId: number
        targetUserId: string
        expectedCurrentVersion: number
        confirmed: boolean
      }
    >({
      query: ({ caseId, targetUserId, ...body }) => ({
        url: `/rfqs/${caseId}/contact-owner`,
        method: 'PUT',
        body: { contactOwnerId: targetUserId, ...body },
      }),
    }),
    updateSalesMemo: builder.mutation<
      MemoResult,
      { caseId: number; memo: string; expectedVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/memos/sales`,
        method: 'PUT',
        body,
      }),
    }),
    updateTraderMemo: builder.mutation<
      MemoResult,
      { caseId: number; memo: string; expectedVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/memos/trader`,
        method: 'PUT',
        body,
      }),
    }),
    saveAmendment: builder.mutation<AmendmentResult, { caseId: number; notional: number | null; settlementDate: string | null; salesAndTradingMessage: string; expectedCurrentVersion: number; expectedDraftVersion: number | null }>({
      query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/amendment`, method: 'PUT', body }),
    }),
    confirmAmendment: builder.mutation<AmendmentResult, { caseId: number; expectedCurrentVersion: number; expectedDraftVersion: number }>({
      query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/amendment/confirm`, method: 'POST', body }),
    }),
    discardAmendment: builder.mutation<AmendmentResult, { caseId: number; expectedCurrentVersion: number; expectedDraftVersion: number }>({
      query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/amendment/discard`, method: 'POST', body }),
    }),
    bulkConfirmAmendments: builder.mutation<BulkItemResult[], { items: { caseId: number; expectedCurrentVersion: number; expectedDraftVersion: number }[] }>({ query: (body) => ({ url: '/rfqs/amendment/bulk-confirm', method: 'POST', body }) }),
    bulkDiscardAmendments: builder.mutation<BulkItemResult[], { items: { caseId: number; expectedCurrentVersion: number; expectedDraftVersion: number }[] }>({ query: (body) => ({ url: '/rfqs/amendment/bulk-discard', method: 'POST', body }) }),
    createFromExisting: builder.mutation<InitialRfqResponse, number>({ query: (caseId) => ({ url: `/rfqs/${caseId}/create-from-existing`, method: 'POST' }) }),
    cancelRfq: builder.mutation<LifecycleResult, { caseId: number; expectedCurrentVersion: number }>({ query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/cancel`, method: 'POST', body }) }),
    reopenRfq: builder.mutation<LifecycleResult, { caseId: number; expectedCurrentVersion: number }>({ query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/reopen`, method: 'POST', body }) }),
    withdrawQuote: builder.mutation<LifecycleResult, { caseId: number; expectedVersion: number }>({ query: ({ caseId, ...body }) => ({ url: `/rfqs/${caseId}/quote/withdraw`, method: 'POST', body }) }),
    scratchPrice: builder.mutation<CalculatedQuotePayload, { securityId: string; settlementDate: string; driver: string; value: number; simpleYieldSlide: number }>({ query: (body) => ({ url: '/pricer', method: 'POST', body }) }),
    searchClients: builder.query<ClientSearchResult[], string>({
      query: (q) => ({ url: '/rfqs/candidates/clients', params: { q } }),
      keepUnusedDataFor: 0,
    }),
    searchSecurities: builder.query<SecuritySearchResult[], string>({
      query: (q) => ({ url: '/rfqs/candidates/securities', params: { q } }),
      keepUnusedDataFor: 0,
    }),
    getAssignableTraders: builder.query<UserSummary[], void>({ query: () => '/assignable-traders' }),
    getContactOwnerCandidates: builder.query<UserSummary[], void>({ query: () => '/contact-owner-candidates' }),
    resolveRfqCreationContext: builder.query<
      RfqCreationContext,
      { securityId: string }
    >({
      query: (params) => ({ url: '/rfqs/creation-context', params }),
      keepUnusedDataFor: 0,
    }),
  }),
})

export const {
  useAssignTraderMutation,
  useBulkCloseAwayRfqsMutation,
  useBulkConfirmAmendmentsMutation,
  useBulkDiscardAmendmentsMutation,
  useCancelRfqMutation,
  useCalculateWorkingQuoteMutation,
  useChangeContactOwnerMutation,
  useChangeWorkingQuoteModeMutation,
  useCloseHitRfqMutation,
  useCloseAwayRfqMutation,
  useConfirmQuoteMutation,
  useConfirmDraftMutation,
  useConfirmNewRfqMutation,
  useCreateDraftMutation,
  useCreateFromExistingMutation,
  useCorrectOutcomeToHitMutation,
  useCorrectOutcomeToAwayMutation,
  useDiscardDraftMutation,
  useDiscardAmendmentMutation,
  useConfirmAmendmentMutation,
  useSaveAmendmentMutation,
  useReopenRfqMutation,
  useWithdrawQuoteMutation,
  useScratchPriceMutation,
  useGetEodQuery,
  useGetEventsQuery,
  useSearchRfqsQuery,
  useGetGridConfigQuery,
  useSaveGridConfigMutation,
  useGetActiveTraderRfqsQuery,
  useGetActiveSalesRfqsQuery,
  useGetMeQuery,
  useGetHealthQuery,
  useGetBusinessDateQuery,
  useGetQuoteExpiryQuery,
  useSaveQuoteExpiryMutation,
  useGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useLazyResolveRfqCreationContextQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
  usePickUpRfqMutation,
  usePresentQuoteMutation,
  useReleaseRfqMutation,
  useTakeOverRfqMutation,
  useUpdateSalesMemoMutation,
  useUpdateTraderMemoMutation,
  useUpdateManualWorkingQuoteMutation,
  useUnpresentQuoteMutation,
  useUpdateDraftMutation,
} = api
