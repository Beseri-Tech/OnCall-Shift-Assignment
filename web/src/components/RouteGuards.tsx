import { Navigate, Outlet, useLocation } from 'react-router'
import { isAdmin, useMe } from '../auth'
import { ChangePasswordPage } from '../pages/AuthPages'
import { Loading } from './common'

// Who may open what:
//   anyone            /, /login, /forgot-password, /reset-password (and unknown addresses go to /)
//   signed in         everything else; signed out, you go to /login and come back after signing in
//   officers only     their own leave, leave requests, on call, ETAP Claim and profile
//   admins only       /leave (everyone's leave) and /admin
// A signed-in account that may not open a page goes to /home, which picks its start page.

/** Signed in with a real password, or off to sign in (remembering where they were going). */
export function RequireLogin() {
  const me = useMe()
  const location = useLocation()
  if (me.isLoading) return <Loading />
  if (!me.data) return <Navigate to={`/login?next=${encodeURIComponent(location.pathname + location.search)}`} replace />
  if (me.data.mustChangePassword) return <ChangePasswordPage forced />
  return <Outlet />
}

/** Admins, supervisors and masters. */
export function AdminOnly() {
  const me = useMe()
  return isAdmin(me.data) ? <Outlet /> : <Navigate to="/home" replace />
}

/** Officers on the rota: pages about their own leave and on call. */
export function OfficerOnly() {
  const me = useMe()
  return me.data?.personId ? <Outlet /> : <Navigate to="/home" replace />
}

/** /home: officers start on their leave, supervisors on the overview. A master with no hospital chosen sees the hospitals list (Layout). */
export function Home() {
  const me = useMe()
  return <Navigate to={me.data?.personId ? '/my-leave' : '/overview'} replace />
}
