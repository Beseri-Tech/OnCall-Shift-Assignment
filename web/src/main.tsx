import '@mantine/core/styles.css'
import '@mantine/dates/styles.css'
import '@mantine/notifications/styles.css'
import './styles.css'

import { MantineProvider, createTheme } from '@mantine/core'
import { ModalsProvider } from '@mantine/modals'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode, Suspense, lazy } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { Loading } from './components/common'
import { Layout } from './components/Layout'
import { LeavePage } from './pages/LeavePage'
import { OverviewPage } from './pages/OverviewPage'
import { RotaPage } from './pages/RotaPage'

// Admin screens are only needed by the admin, so they load on demand.
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const AdminPage = lazy(() => import('./pages/admin/AdminPage').then(m => ({ default: m.AdminPage })))

const theme = createTheme({
  primaryColor: 'blue',
  fontFamily: '"Segoe UI", system-ui, -apple-system, Roboto, sans-serif',
  defaultRadius: 'md',
  headings: { fontWeight: '700' },
})

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <MantineProvider theme={theme} defaultColorScheme="light">
      <Notifications position="top-right" />
      <ModalsProvider>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <Routes>
              <Route element={<Layout />}>
                <Route index element={<LeavePage />} />
                <Route path="overview" element={<OverviewPage />} />
                <Route path="rota" element={<RotaPage />} />
                <Route path="admin/*" element={<Suspense fallback={<Loading />}><AdminPage /></Suspense>} />
                <Route path="*" element={<Navigate to="/" replace />} />
              </Route>
            </Routes>
          </BrowserRouter>
        </QueryClientProvider>
      </ModalsProvider>
    </MantineProvider>
  </StrictMode>,
)
