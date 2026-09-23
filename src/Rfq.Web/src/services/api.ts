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
  expectedCurrentVersion: number
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
  salesId: string | null
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
  stateSince: string
  confirmedQuote: SalesConfirmedQuoteSummary | null
  draftRevisionId?: string | null
  draftVersion?: number | null
  draftSettlementDate?: string | null
  draftNotional?: number | null
  draftSalesAndTradingMessage?: string | null
}

export interface SalesConfirmedQuoteSummary {
  quoteId: string
  mode: 'Calculated' | 'Manual'
  price: number | null
  bbgYield: number | null
  finalSimpleYield: number | null
  gSpread: number | null
  confirmedAt: string
}

export interface SalesRecentRevisionChange {
  field:
    | 'Notional'
    | 'Settlement'
    | 'Message'
    | 'Price'
    | 'Yield'
    | 'Simple'
    | 'GSpread'
  before: string | null
  after: string | null
}

export interface SalesRecentRevision {
  kind: 'Rfq' | 'Quote'
  occurredAt: string
  caseId: number
  clientId: string
  clientName: string
  securityId: string
  securityName: string
  changes: SalesRecentRevisionChange[]
}

export interface AmendmentResult {
  caseId: number
  currentRevisionId: string
  draftRevisionId: string | null
  currentVersion: number
  draftVersion: number | null
  draftNotional: number | null
  draftSettlementDate: string | null
  draftSalesAndTradingMessage: string | null
  rfqStatus: string
  quoteStatus: string | null
  quoteRequestReason: string | null
}
export interface CaseOperationResult {
  caseId: number
  status: 'Applied' | 'NoChange' | 'Failed'
  failureCode: string | null
  message: string | null
}
export interface LifecycleResult {
  caseId: number
  rfqStatus: string
  quoteStatus: string | null
  quoteRequestReason: string | null
  currentVersion: number
}
export type PostProcessPreset = 'Today' | 'Unclosed'
export type PostProcessScope = 'Mine' | 'AllPermitted'
export type PostProcessLifecycleChangeType =
  'Hit' | 'Away' | 'Cancel' | 'CorrectToHit' | 'CorrectToAway'
export interface PostProcessItem {
  caseId: number
  createdAt: string
  createdBusinessDate: string
  clientId: string
  clientName: string
  securityId: string
  securityName: string
  securityBbgDisplay: string
  notional: number | null
  settlementDate: string | null
  contactOwnerId: string
  salesId: string | null
  assignedTraderId: string
  rfqStatus: 'Active' | 'Presented' | 'Hit' | 'Away' | 'Cancelled'
  currentVersion: number
  salesAndTradingMessage: string
  myMemo: string
  myMemoVersion: number
  price: number | null
  finalSimpleYield: number | null
  yield: number | null
  ysc: number | null
  gSpread: number | null
  closedBusinessDate: string | null
  lastCorrectionReason: string | null
  lastChangedBy: string | null
  lastChangedAt: string | null
}
export interface PostProcessCommitItem {
  caseId: number
  expectedCurrentVersion: number
  lifecycleChange?: {
    type: PostProcessLifecycleChangeType
    correctionReason?: string | null
  }
  memoChange?: {
    expectedMemoVersion: number
    value: string
  }
}
export interface RfqSearchItem {
  caseId: number
  createdAt: string
  clientId: string
  clientName: string
  securityId: string
  securityName: string
  categoryId: string
  status: string
  quoteStatus: string | null
  contactOwnerId: string
  salesId: string | null
  assignedTraderId: string
  notional: number | null
  settlementDate: string | null
  price: number | null
  finalSimpleYield: number | null
  ysc: number | null
}
export interface RfqSearchResult {
  items: RfqSearchItem[]
  requiresNarrowing: boolean
}
export interface RfqSearchParams {
  createdFrom?: string
  createdTo?: string
  clientId?: string
  securityId?: string
  categoryId?: string
  contactOwnerId?: string
  salesId?: string
  assignedTraderId?: string
  status?: string
  caseId?: number
}
export interface GridConfig {
  screenId: string
  configKey: string
  version: number
  config: unknown
  updatedAt: string
}

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
  { type: 'None'; minutes: null } | { type: 'After'; minutes: number }

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
  salesAndTradingMessage: string
  workingQuoteMode: 'Calculated' | 'Manual'
  calculated: CalculatedQuotePayload | null
  manual: ManualQuotePayload | null
  workingQuoteVersion: number
  traderMemo: string
  traderMemoVersion: number
  createdAt: string
  stateSince: string
}

export interface CalculatedQuotePayload {
  driver:
    | 'Price'
    | 'BbgYield'
    | 'SimpleYield'
    | 'Ysc'
    | 'GSpread'
    | 'Asw'
    | 'ISpread'
    | 'ZSpread'
  driverValue: number
  price: number
  bbgYield: number
  baseSimpleYield: number
  simpleYieldSlide: number
  finalSimpleYield: number
  internalYield: number
  gSpread: number
  asw: number
  ysc: number
  iSpread: number
  zSpread: number
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

export interface QuoteModeSetting {
  mode: 'Calculated' | 'Manual'
}

export interface ThemeSetting {
  mode: 'Light' | 'Dark'
}

export interface ApiProblemDetails {
  status?: number
  title?: string
  detail?: string
  code?: string
  traceId?: string
  calculationErrorCode?: string
  failureLogId?: string
}

export const api = createApi({
  reducerPath: 'api',
  tagTypes: ['PostProcess', 'QuoteExpiry', 'QuoteMode', 'Theme', 'GridConfig'],
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
      query: () => '/system/health',
    }),
    getBusinessDate: builder.query<BusinessDateResponse, void>({
      query: () => '/business-date',
    }),
    getMe: builder.query<CurrentUserResponse, void>({
      query: () => '/me',
    }),
    getActiveSalesRfqs: builder.query<SalesRfq[], void>({
      query: () => '/worklists/sales',
    }),
    getSalesRecentRevisions: builder.query<
      SalesRecentRevision[],
      number | void
    >({
      query: (limit) => ({
        url: '/worklists/sales/recent-revisions',
        params: limit === undefined ? undefined : { limit },
      }),
    }),
    getPostProcess: builder.query<
      PostProcessItem[],
      { preset: PostProcessPreset; scope: PostProcessScope }
    >({
      query: (params) => ({ url: '/post-process', params }),
      providesTags: ['PostProcess'],
    }),
    commitPostProcess: builder.mutation<
      CaseOperationResult[],
      { items: PostProcessCommitItem[] }
    >({
      query: (body) => ({
        url: '/post-process/commit',
        method: 'POST',
        body,
      }),
      invalidatesTags: (result) => (result ? ['PostProcess'] : []),
    }),
    searchRfqs: builder.query<RfqSearchResult, RfqSearchParams>({
      query: (params) => ({ url: '/search/rfqs', params }),
    }),
    getGridConfig: builder.query<
      GridConfig,
      { screenId: string; configKey: string }
    >({
      query: ({ screenId, configKey }) =>
        `/me/grid-configs/${screenId}/${configKey}`,
      providesTags: (_result, _error, { screenId, configKey }) => [
        { type: 'GridConfig', id: `${screenId}/${configKey}` },
      ],
    }),
    saveGridConfig: builder.mutation<
      GridConfig,
      { screenId: string; configKey: string; version: number; config: unknown }
    >({
      query: ({ screenId, configKey, ...body }) => ({
        url: `/me/grid-configs/${screenId}/${configKey}`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_result, _error, { screenId, configKey }) => [
        { type: 'GridConfig', id: `${screenId}/${configKey}` },
      ],
    }),
    getQuoteExpiry: builder.query<QuoteExpiry, void>({
      query: () => '/me/settings/quote-expiry',
      providesTags: ['QuoteExpiry'],
    }),
    saveQuoteExpiry: builder.mutation<QuoteExpiry, QuoteExpiry>({
      query: (body) => ({
        url: '/me/settings/quote-expiry',
        method: 'PUT',
        body,
      }),
      invalidatesTags: ['QuoteExpiry'],
    }),
    getDefaultQuoteMode: builder.query<QuoteModeSetting, void>({
      query: () => '/me/settings/default-quote-mode',
      providesTags: ['QuoteMode'],
    }),
    saveDefaultQuoteMode: builder.mutation<QuoteModeSetting, QuoteModeSetting>({
      query: (body) => ({
        url: '/me/settings/default-quote-mode',
        method: 'PUT',
        body,
      }),
      invalidatesTags: ['QuoteMode'],
    }),
    getTheme: builder.query<ThemeSetting, void>({
      query: () => '/me/settings/theme',
      providesTags: ['Theme'],
    }),
    saveTheme: builder.mutation<ThemeSetting, ThemeSetting>({
      query: (body) => ({
        url: '/me/settings/theme',
        method: 'PUT',
        body,
      }),
      invalidatesTags: ['Theme'],
    }),
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
    getActiveTraderRfqs: builder.query<TraderRfq[], void>({
      query: () => '/worklists/trader',
    }),
    pickUpRfqs: builder.mutation<
      CaseOperationResult[],
      {
        confirmed: boolean
        items: { caseId: number; expectedCurrentVersion: number }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/pick-up',
        method: 'POST',
        body,
      }),
    }),
    releaseRfqs: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({
        url: '/rfqs/release',
        method: 'POST',
        body,
      }),
    }),
    assignTraders: builder.mutation<
      CaseOperationResult[],
      {
        targetTraderId: string
        items: { caseId: number; expectedCurrentVersion: number }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/assign-trader',
        method: 'POST',
        body,
      }),
    }),
    takeOverRfqs: builder.mutation<
      CaseOperationResult[],
      {
        confirmed: boolean
        items: { caseId: number; expectedCurrentVersion: number }[]
      }
    >({
      query: (body) => ({ url: '/rfqs/take-over', method: 'POST', body }),
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
    confirmQuotes: builder.mutation<
      CaseOperationResult[],
      {
        items: {
          caseId: number
          expiry: QuoteExpiry
          expectedCurrentVersion: number
          expectedWorkingQuoteVersion: number
        }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/confirm-quotes',
        method: 'POST',
        body,
      }),
    }),
    withdrawQuotes: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({
        url: '/rfqs/withdraw-quotes',
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
    closeAwayRfqs: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({ url: '/rfqs/close-away', method: 'POST', body }),
    }),
    presentRfqs: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({ url: '/rfqs/present', method: 'POST', body }),
    }),
    unpresentRfqs: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({ url: '/rfqs/unpresent', method: 'POST', body }),
    }),
    cancelRfqs: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({ url: '/rfqs/cancel', method: 'POST', body }),
    }),
    confirmInitialDrafts: builder.mutation<
      CaseOperationResult[],
      {
        items: {
          caseId: number
          notional: number | null
          settlementDate: string
          standardSettlementDate: string
          salesAndTradingMessage: string
          assignedTraderId: string
          expectedCurrentVersion: number
        }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/confirm-initial-drafts',
        method: 'POST',
        body,
      }),
    }),
    discardInitialDrafts: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({
        url: '/rfqs/discard-initial-drafts',
        method: 'POST',
        body,
      }),
    }),
    correctOutcomeToHit: builder.mutation<
      CloseRfqResult,
      { caseId: number; reason: string; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/outcome/correct-to-hit`,
        method: 'POST',
        body,
      }),
    }),
    correctOutcomeToAway: builder.mutation<
      CloseRfqResult,
      { caseId: number; reason: string; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/outcome/correct-to-away`,
        method: 'POST',
        body,
      }),
    }),
    changeContactOwners: builder.mutation<
      CaseOperationResult[],
      {
        targetContactOwnerId: string
        confirmed: boolean
        items: { caseId: number; expectedCurrentVersion: number }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/change-contact-owner',
        method: 'POST',
        body,
      }),
    }),
    updateSalesMemo: builder.mutation<
      MemoResult,
      { caseId: number; memo: string; expectedMemoVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/memos/sales`,
        method: 'PUT',
        body,
      }),
    }),
    updateTraderMemo: builder.mutation<
      MemoResult,
      { caseId: number; memo: string; expectedMemoVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/memos/trader`,
        method: 'PUT',
        body,
      }),
    }),
    saveAmendment: builder.mutation<
      AmendmentResult,
      {
        caseId: number
        notional: number | null
        settlementDate: string | null
        salesAndTradingMessage: string
        expectedCurrentVersion: number
        expectedDraftVersion: number | null
      }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/amendment`,
        method: 'PUT',
        body,
      }),
    }),
    startAmendment: builder.mutation<
      AmendmentResult,
      { caseId: number; expectedCurrentVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/rfqs/${caseId}/amendment/start`,
        method: 'POST',
        body,
      }),
    }),
    confirmAmendments: builder.mutation<
      CaseOperationResult[],
      {
        items: {
          caseId: number
          expectedCurrentVersion: number
          expectedDraftVersion: number
        }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/confirm-amendments',
        method: 'POST',
        body,
      }),
    }),
    discardAmendments: builder.mutation<
      CaseOperationResult[],
      {
        items: {
          caseId: number
          expectedCurrentVersion: number
          expectedDraftVersion: number
        }[]
      }
    >({
      query: (body) => ({
        url: '/rfqs/discard-amendments',
        method: 'POST',
        body,
      }),
    }),
    createFromExisting: builder.mutation<InitialRfqResponse, number>({
      query: (caseId) => ({
        url: `/rfqs/${caseId}/create-from-existing`,
        method: 'POST',
      }),
    }),
    reopenRfqs: builder.mutation<
      CaseOperationResult[],
      { items: { caseId: number; expectedCurrentVersion: number }[] }
    >({
      query: (body) => ({ url: '/rfqs/reopen', method: 'POST', body }),
    }),
    scratchPrice: builder.mutation<
      CalculatedQuotePayload,
      {
        securityId: string
        settlementDate: string
        driver: CalculatedQuotePayload['driver']
        value: number
        simpleYieldSlide: number
      }
    >({ query: (body) => ({ url: '/pricing/scratch', method: 'POST', body }) }),
    searchClients: builder.query<ClientSearchResult[], string>({
      query: (q) => ({ url: '/reference-data/clients', params: { q } }),
      keepUnusedDataFor: 0,
    }),
    searchSecurities: builder.query<SecuritySearchResult[], string>({
      query: (q) => ({ url: '/reference-data/securities', params: { q } }),
      keepUnusedDataFor: 0,
    }),
    getAssignableTraders: builder.query<UserSummary[], void>({
      query: () => '/reference-data/assignable-traders',
    }),
    getContactOwnerCandidates: builder.query<UserSummary[], void>({
      query: () => '/reference-data/contact-owner-candidates',
    }),
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
  useAssignTradersMutation,
  useConfirmQuotesMutation,
  useCloseAwayRfqsMutation,
  usePickUpRfqsMutation,
  useReleaseRfqsMutation,
  useWithdrawQuotesMutation,
  usePresentRfqsMutation,
  useUnpresentRfqsMutation,
  useCancelRfqsMutation,
  useConfirmInitialDraftsMutation,
  useDiscardInitialDraftsMutation,
  useConfirmAmendmentsMutation,
  useDiscardAmendmentsMutation,
  useCalculateWorkingQuoteMutation,
  useChangeContactOwnersMutation,
  useChangeWorkingQuoteModeMutation,
  useCloseHitRfqMutation,
  useConfirmNewRfqMutation,
  useCreateDraftMutation,
  useCreateFromExistingMutation,
  useCorrectOutcomeToHitMutation,
  useCorrectOutcomeToAwayMutation,
  useSaveAmendmentMutation,
  useStartAmendmentMutation,
  useReopenRfqsMutation,
  useScratchPriceMutation,
  useGetPostProcessQuery,
  useCommitPostProcessMutation,
  useSearchRfqsQuery,
  useGetGridConfigQuery,
  useSaveGridConfigMutation,
  useGetActiveTraderRfqsQuery,
  useGetActiveSalesRfqsQuery,
  useGetSalesRecentRevisionsQuery,
  useLazyGetSalesRecentRevisionsQuery,
  useGetMeQuery,
  useGetHealthQuery,
  useGetBusinessDateQuery,
  useGetQuoteExpiryQuery,
  useGetDefaultQuoteModeQuery,
  useGetThemeQuery,
  useSaveDefaultQuoteModeMutation,
  useSaveQuoteExpiryMutation,
  useSaveThemeMutation,
  useGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useLazyResolveRfqCreationContextQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
  useLazySearchRfqsQuery,
  useTakeOverRfqsMutation,
  useUpdateSalesMemoMutation,
  useUpdateTraderMemoMutation,
  useUpdateManualWorkingQuoteMutation,
  useUpdateDraftMutation,
} = api
