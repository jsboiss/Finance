import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
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
