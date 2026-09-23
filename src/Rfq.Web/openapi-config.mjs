/** @type {import('@rtk-query/codegen-openapi').ConfigFile} */
const config = {
  schemaFile: '../../artifacts/openapi/Rfq.Api.json',
  apiFile: './src/services/baseApi.ts',
  apiImport: 'baseApi',
  outputFile: './src/generated/rfqApi.ts',
  exportName: 'rfqApi',
  hooks: {
    queries: true,
    lazyQueries: true,
    mutations: true,
  },
  tag: false,
  encodePathParams: true,
  useUnknown: true,
}

export default config
