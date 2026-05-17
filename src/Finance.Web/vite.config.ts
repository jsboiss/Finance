import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  build: {
    outDir: '../Finance.Api/wwwroot',
    emptyOutDir: true
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5000',
      '/webhooks': 'http://localhost:5000'
    }
  }
})
