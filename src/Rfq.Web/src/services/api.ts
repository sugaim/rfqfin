import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

export interface HealthResponse {
  status: string
}

export interface CreateDraftRequest {
  clientId: string
  securityId: string
  notional?: number
  settlementDate?: string
  salesAndTradingMessage?: string
  assignedTraderId?: string
}

export interface UpdateDraftRequest {
  notional?: number
  settlementDate?: string
  salesAndTradingMessage?: string
  assignedTraderId?: string
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
  currentVersion: number
  revisionStatus: string
  contactOwnerId: string
  assignedTraderId: string
  settlementDate: string | null
  standardSettlementDate: string
  notional: number | null
  salesAndTradingMessage: string
  version: number
  createdAt: string
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
  roles: string[]
  deskId: string
  defaultQuoteExpiryMinutes: number | null
}

export interface RfqDefaults {
  securityId: string
  categoryId: string
  categoryName: string
  contactOwnerId: string
  contactOwnerName: string
  assignedTraderId: string
  assignedTraderName: string
  systemDate: string
  standardSettlementDate: string
}

export interface SystemDateResponse {
  date: string
}

export interface CurrentUserResponse {
  userId: string
  roles: string[]
  deskId: string
  defaultQuoteExpiryMinutes: number | null
}

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
  expiryMinutes: number | null
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
    getSystemDate: builder.query<SystemDateResponse, void>({
      query: () => '/system-date',
    }),
    getCurrentUser: builder.query<CurrentUserResponse, void>({
      query: () => '/current-user',
    }),
    getActiveSalesRfqs: builder.query<SalesRfq[], void>({
      query: () => '/rfqs/active-sales',
    }),
    createDraft: builder.mutation<InitialRfqResponse, CreateDraftRequest>({
      query: (body) => ({
        url: '/rfqs',
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
        url: '/rfqs/confirm',
        method: 'POST',
        body,
      }),
    }),
    confirmDraft: builder.mutation<
      InitialRfqResponse,
      { caseId: number; body: UpdateDraftRequest }
    >({
      query: ({ caseId, body }) => ({
        url: `/rfqs/${caseId}/confirm`,
        method: 'POST',
        body,
      }),
    }),
    discardDraft: builder.mutation<void, { caseId: number; expectedVersion: number }>({
      query: ({ caseId, expectedVersion }) => ({
        url: `/rfqs/${caseId}/discard`,
        method: 'POST',
        body: { expectedVersion },
      }),
    }),
    getActiveTraderRfqs: builder.query<TraderRfq[], void>({
      query: () => '/trader/rfqs/active',
    }),
    pickUpRfq: builder.mutation<
      OwnershipResult,
      { caseId: number; expectedVersion: number; confirmed: boolean }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/trader/rfqs/${caseId}/pick-up`,
        method: 'POST',
        body,
      }),
    }),
    releaseRfq: builder.mutation<
      OwnershipResult,
      { caseId: number; expectedVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/trader/rfqs/${caseId}/release`,
        method: 'POST',
        body,
      }),
    }),
    assignTrader: builder.mutation<
      OwnershipResult,
      { caseId: number; targetTraderId: string; expectedVersion: number }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/trader/rfqs/${caseId}/assign`,
        method: 'POST',
        body,
      }),
    }),
    takeOverRfq: builder.mutation<
      OwnershipResult,
      { caseId: number; expectedVersion: number; confirmed: boolean }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/trader/rfqs/${caseId}/take-over`,
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
        url: `/trader/rfqs/${caseId}/working-quote/calculate`,
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
        url: `/trader/rfqs/${caseId}/working-quote/mode`,
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
        url: `/trader/rfqs/${caseId}/working-quote/manual`,
        method: 'PUT',
        body,
      }),
    }),
    confirmQuote: builder.mutation<
      ConfirmQuoteResult,
      {
        caseId: number
        expiryMinutes: number | null
        expectedCurrentVersion: number
        expectedWorkingQuoteVersion: number
      }
    >({
      query: ({ caseId, ...body }) => ({
        url: `/trader/rfqs/${caseId}/confirm-quote`,
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
    searchClients: builder.query<ClientSearchResult[], string>({
      query: (q) => ({ url: '/masters/clients/search', params: { q } }),
      keepUnusedDataFor: 0,
    }),
    searchSecurities: builder.query<SecuritySearchResult[], string>({
      query: (q) => ({ url: '/masters/securities/search', params: { q } }),
      keepUnusedDataFor: 0,
    }),
    getUsers: builder.query<UserSummary[], string>({
      query: (role) => ({ url: '/masters/users', params: { role } }),
    }),
    resolveRfqDefaults: builder.query<
      RfqDefaults,
      { securityId: string }
    >({
      query: (params) => ({ url: '/rfq-defaults', params }),
      keepUnusedDataFor: 0,
    }),
  }),
})

export const {
  useAssignTraderMutation,
  useCalculateWorkingQuoteMutation,
  useChangeWorkingQuoteModeMutation,
  useConfirmQuoteMutation,
  useConfirmDraftMutation,
  useConfirmNewRfqMutation,
  useCreateDraftMutation,
  useDiscardDraftMutation,
  useGetActiveTraderRfqsQuery,
  useGetActiveSalesRfqsQuery,
  useGetCurrentUserQuery,
  useGetHealthQuery,
  useGetSystemDateQuery,
  useGetUsersQuery,
  useLazyResolveRfqDefaultsQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
  usePickUpRfqMutation,
  usePresentQuoteMutation,
  useReleaseRfqMutation,
  useTakeOverRfqMutation,
  useUpdateManualWorkingQuoteMutation,
  useUnpresentQuoteMutation,
  useUpdateDraftMutation,
} = api
