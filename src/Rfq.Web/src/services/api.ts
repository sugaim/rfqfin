import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

export interface HealthResponse {
  status: string
}

export const api = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  endpoints: (builder) => ({
    getHealth: builder.query<HealthResponse, void>({
      query: () => '/health',
    }),
  }),
})

export const { useGetHealthQuery } = api
