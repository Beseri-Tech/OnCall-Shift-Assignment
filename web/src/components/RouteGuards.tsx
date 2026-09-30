import { Navigate, Outlet, useLocation } from 'react-router'
import { isAdmin, useMe } from '../auth'
import { ChangePasswordPage } from '../pages/AuthPages'
import { LeavePage } from '../pages/LeavePage'
import { Loading } from './common'

/** Signed in with a real password, or off to /login (remembering where they were going). */
export function RequireLogin() {
  const me = useMe()
  const location = useLocation()
  if (me.isLoading) return <Loading />
  if (!me.data) return <Navigate to={`/login?next=${encodeURIComponent(location.pathname + location.search)}`} replace />
  if (me.data.mustChangePassword) return <ChangePasswordPage forced />
  return <Outlet />
}

export function AdminOnly() {
  const me = useMe()
  return isAdmin(me.data) ? <Outlet /> : <Navigate to="/" replace />
}

/** Officers land on their leave; supervisors (not on the rota) on the overview. */
export function Home() {
  const me = useMe()
  return me.data?.personId ? <LeavePage /> : <Navigate to="/overview" replace />
}
