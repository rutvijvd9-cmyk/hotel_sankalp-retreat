import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../HotelAutomation.Worker/wwwroot',
    emptyOutDir: false
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:59725',
        changeOrigin: true
      },
      '/uploads': {
        target: 'http://localhost:59725',
        changeOrigin: true
      }
    }
  }
})
