import '@mantine/core/styles.css'
import '@mantine/dates/styles.css'
import '@mantine/notifications/styles.css'
import './styles.css'

import { MantineProvider, createTheme } from '@mantine/core'
import { ModalsProvider } from '@mantine/modals'
import { Notifications } from '@mantine/notifications'
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode, Suspense, lazy } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { ApiError } from './api'
import { AdminOnly, Home, RequireLogin } from './components/RouteGuards'
import { Loading } from './components/common'
import { Layout } from './components/Layout'
import { ChangePasswordPage, ForgotPasswordPage, LoginPage, ResetPasswordPage } from './pages/AuthPages'
import { LeavePage } from './pages/LeavePage'
import { MyOnCallPage } from './pages/MyOnCallPage'
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

// A 401 anywhere means the session ended (logged out, disabled, password changed elsewhere): back to sign in.
const signedOut = (e: unknown) => {
  if (e instanceof ApiError && e.status === 401) queryClient.setQueryData(['me'], null)
}
const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: signedOut }),
  mutationCache: new MutationCache({ onError: signedOut }),
  defaultOptions: { queries: { retry: (n, e) => !(e instanceof ApiError && e.status < 500) && n < 1, refetchOnWindowFocus: false } },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <MantineProvider theme={theme} defaultColorScheme="light">
      <Notifications position="top-right" />
      <ModalsProvider>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <Routes>
              <Route path="login" element={<LoginPage />} />
              <Route path="forgot-password" element={<ForgotPasswordPage />} />
              <Route path="reset-password" element={<ResetPasswordPage />} />
              <Route element={<RequireLogin />}>
                <Route path="account/password" element={<ChangePasswordPage />} />
                <Route element={<Layout />}>
                  <Route index element={<Home />} />
                  <Route path="my-oncall" element={<MyOnCallPage />} />
                  <Route path="overview" element={<OverviewPage />} />
                  <Route path="rota" element={<RotaPage />} />
                  <Route element={<AdminOnly />}>
                    <Route path="leave" element={<LeavePage />} />
                    <Route path="admin/*" element={<Suspense fallback={<Loading />}><AdminPage /></Suspense>} />
                  </Route>
                  <Route path="*" element={<Navigate to="/" replace />} />
                </Route>
              </Route>
            </Routes>
          </BrowserRouter>
        </QueryClientProvider>
      </ModalsProvider>
    </MantineProvider>
  </StrictMode>,
)
