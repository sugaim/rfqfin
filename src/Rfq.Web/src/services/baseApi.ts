import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

const apiFetch = fetchBaseQuery({
  baseUrl: '/api',
  prepareHeaders: (headers) => {
    const identity = window.localStorage.getItem('rfq-development-user')
    if (identity) headers.set('X-Development-User', identity)

    return headers
  },
})

function relativeApiUrl(url: string): string {
  if (url === '/api') return ''

  return url.startsWith('/api/') ? url.slice('/api'.length) : url
}

const apiBaseQuery: typeof apiFetch = (args, api, extraOptions) =>
  apiFetch(
    typeof args === 'string'
      ? relativeApiUrl(args)
      : { ...args, url: relativeApiUrl(args.url) },
    api,
    extraOptions,
  )

export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: apiBaseQuery,
  endpoints: () => ({}),
})
