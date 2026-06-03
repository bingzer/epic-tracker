import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';

const cwd = process.cwd();

export default defineConfig({
  root: cwd,
  plugins: [react(), tailwindcss()],
  server: {
    port: 6791,
    proxy: {
      '/api': {
        target: 'http://localhost:6790',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/hubs': {
        target: 'http://localhost:6790',
        ws: true,
      },
    },
  },
  build: {
    outDir: path.join(cwd, '../EpicTracker.Api/wwwroot'),
    emptyOutDir: true,
    sourcemap: true,
  },
});
