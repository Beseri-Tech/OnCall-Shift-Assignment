import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development the API runs separately (`dotnet run` in api/Rota.Api); in production
// the API serves the built files from wwwroot, so /api is same-origin either way.
export default defineConfig({
  plugins: [react()],
  // Mantine + the date pickers make up most of the main chunk (~200 kB gzipped).
  build: { chunkSizeWarningLimit: 800 },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5159',
    },
  },
})
