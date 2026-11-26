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
import { EnvironmentBanner } from './components/EnvironmentBanner'
import { AdminOnly, Home, OfficerOnly, RequireLogin } from './components/RouteGuards'
import { Loading } from './components/common'
import { ChangePasswordPage, ForgotPasswordPage, LoginPage, ResetPasswordPage } from './pages/AuthPages'
import { LandingPage } from './pages/LandingPage'

// Signed-in screens load after sign-in, so the landing and sign-in pages stay light.
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const Layout = lazy(() => import('./components/Layout').then(m => ({ default: m.Layout })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const ChangelogPage = lazy(() => import('./pages/ChangelogPage').then(m => ({ default: m.ChangelogPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const LeavePage = lazy(() => import('./pages/LeavePage').then(m => ({ default: m.LeavePage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const MyOnCallPage = lazy(() => import('./pages/MyOnCallPage').then(m => ({ default: m.MyOnCallPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const OverviewPage = lazy(() => import('./pages/OverviewPage').then(m => ({ default: m.OverviewPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const RotaPage = lazy(() => import('./pages/RotaPage').then(m => ({ default: m.RotaPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const LeaveRequestsPage = lazy(() => import('./pages/LeaveRequestsPage').then(m => ({ default: m.LeaveRequestsPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const HelpPage = lazy(() => import('./pages/HelpPage').then(m => ({ default: m.HelpPage })))
// Admin screens are only needed by the admin, so they load on demand.
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const AdminPage = lazy(() => import('./pages/admin/AdminPage').then(m => ({ default: m.AdminPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const ProfilePage = lazy(() => import('./pages/ProfilePage').then(m => ({ default: m.ProfilePage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const EtapPage = lazy(() => import('./pages/EtapPage').then(m => ({ default: m.EtapPage })))
// oxlint-disable-next-line react/only-export-components -- entry file, not hot-reloaded
const CasesPage = lazy(() => import('./pages/CasesPage').then(m => ({ default: m.CasesPage })))

// Clinical teal. Shade 7 is the default so white button text keeps 4.5:1 contrast.
const theme = createTheme({
  colors: {
    brand: ['#ecfeff', '#cffafe', '#a5f3fc', '#67e8f9', '#22d3ee', '#06b6d4', '#0891b2', '#0e7490', '#155e75', '#164e63'],
  },
  primaryColor: 'brand',
  primaryShade: 7,
  fontFamily: '"Segoe UI", system-ui, -apple-system, Roboto, sans-serif',
  defaultRadius: 'md',
  headings: { fontWeight: '700' },
  components: {
    Paper: { defaultProps: { radius: 'lg' } },
    Card: { defaultProps: { radius: 'lg' } },
  },
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
      <EnvironmentBanner />
      <Notifications position="top-right" />
      <ModalsProvider>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            {/* Who may open what: see components/RouteGuards.tsx. */}
            <Routes>
              <Route index element={<LandingPage />} />
              <Route path="login" element={<LoginPage />} />
              <Route path="forgot-password" element={<ForgotPasswordPage />} />
              <Route path="reset-password" element={<ResetPasswordPage />} />
              <Route element={<RequireLogin />}>
                <Route path="account/password" element={<ChangePasswordPage />} />
                <Route element={<Suspense fallback={<Loading />}><Layout /></Suspense>}>
                  <Route path="home" element={<Home />} />
                  <Route path="hospitals" element={<Navigate to="/home" replace />} />
                  <Route path="overview" element={<Suspense fallback={<Loading />}><OverviewPage /></Suspense>} />
                  <Route path="rota" element={<Suspense fallback={<Loading />}><RotaPage /></Suspense>} />
                  <Route path="help" element={<Suspense fallback={<Loading />}><HelpPage /></Suspense>} />
                  <Route path="rules" element={<Navigate to="/help" replace />} />
                  <Route path="changelog" element={<Suspense fallback={<Loading />}><ChangelogPage /></Suspense>} />
                  <Route path="cases" element={<Suspense fallback={<Loading />}><CasesPage /></Suspense>} />
                  <Route element={<OfficerOnly />}>
                    <Route path="my-leave" element={<Suspense fallback={<Loading />}><LeavePage /></Suspense>} />
                    <Route path="leave-requests" element={<Suspense fallback={<Loading />}><LeaveRequestsPage /></Suspense>} />
                    <Route path="my-oncall" element={<Suspense fallback={<Loading />}><MyOnCallPage /></Suspense>} />
                    <Route path="profile" element={<Suspense fallback={<Loading />}><ProfilePage /></Suspense>} />
                    <Route path="etap" element={<Suspense fallback={<Loading />}><EtapPage /></Suspense>} />
                  </Route>
                  <Route element={<AdminOnly />}>
                    <Route path="leave" element={<Suspense fallback={<Loading />}><LeavePage /></Suspense>} />
                    <Route path="admin/*" element={<Suspense fallback={<Loading />}><AdminPage /></Suspense>} />
                  </Route>
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </BrowserRouter>
        </QueryClientProvider>
      </ModalsProvider>
    </MantineProvider>
  </StrictMode>,
)
