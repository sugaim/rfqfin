import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

export interface HealthResponse {
  status: string
}

export interface CreateDraftRequest {
  clientId: string
  securityId: string
}

export interface CreateDraftResponse {
  caseId: number
  revisionId: string
  rfqStatus: string
  createdAt: string
}

export interface SalesRfq {
  caseId: number
  clientId: string
  securityId: string
  rfqStatus: string
  currentRevisionId: string
  revisionStatus: string
  createdAt: string
}

export const api = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  endpoints: (builder) => ({
    getHealth: builder.query<HealthResponse, void>({
      query: () => '/health',
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
  }),
})

export const {
  useCreateDraftMutation,
  useGetActiveSalesRfqsQuery,
  useGetHealthQuery,
} = api
