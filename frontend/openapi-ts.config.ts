import { defineConfig } from '@hey-api/openapi-ts'

export default defineConfig({
  input: '../api-contract/tsp-output/@typespec/openapi3/openapi.yaml',
  output: 'src/generated/api',
  plugins: ['@hey-api/client-fetch', '@hey-api/sdk'],
})
