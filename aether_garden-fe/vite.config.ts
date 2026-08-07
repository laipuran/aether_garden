import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    fs: {
      // Serve the sibling aether_garden-wasm crate (compiled pkg) in dev.
      allow: ['..'],
    },
  },
})
