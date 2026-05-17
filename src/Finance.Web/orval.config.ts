import { defineConfig } from 'orval'

export default defineConfig({
  finance: {
    input: 'http://localhost:5000/openapi/v1.json',
    output: {
      target: './src/api/generated.ts',
      client: 'fetch'
    }
  }
})
