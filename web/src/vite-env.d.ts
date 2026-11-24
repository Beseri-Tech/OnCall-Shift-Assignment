/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "staging" on the staging deploy (shows the staging ribbon); unset in production. */
  readonly VITE_APP_ENVIRONMENT?: string
}

/** From package.json, injected by vite.config.ts. */
declare const __APP_VERSION__: string
/** Short git commit of the build ("dev" when unavailable), injected by vite.config.ts. */
declare const __APP_COMMIT__: string
