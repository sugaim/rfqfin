import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

export interface HealthResponse {
  status: string
}

export interface CreateDraftRequest {
  clientId: string
  securityId: string
  settlementDate: string
  assignedTraderId: string
}

export interface CreateDraftResponse {
  caseId: number
  revisionId: string
  rfqStatus: string
  categoryId: string
  contactOwnerId: string
  assignedTraderId: string
  settlementDate: string
  standardSettlementDate: string
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
  currentRevisionId: string
  revisionStatus: string
  contactOwnerId: string
  assignedTraderId: string
  settlementDate: string
  standardSettlementDate: string
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

export const api = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  endpoints: (builder) => ({
    getHealth: builder.query<HealthResponse, void>({
      query: () => '/health',
    }),
    getSystemDate: builder.query<SystemDateResponse, void>({
      query: () => '/system-date',
    }),
    getActiveSalesRfqs: builder.query<SalesRfq[], void>({
      query: () => '/rfqs/active-sales',
    }),
    createDraft: builder.mutation<CreateDraftResponse, CreateDraftRequest>({
      query: (body) => ({
        url: '/rfqs',
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
  useCreateDraftMutation,
  useGetActiveSalesRfqsQuery,
  useGetHealthQuery,
  useGetSystemDateQuery,
  useGetUsersQuery,
  useLazyResolveRfqDefaultsQuery,
  useLazySearchClientsQuery,
  useLazySearchSecuritiesQuery,
} = api
