import { useEffect } from 'react'

/*
 * Marks every non-production build so nobody mistakes it for the live rota. Production is the only build that sets
 * nothing: the staging deploy sets VITE_APP_ENVIRONMENT=staging, and a local dev build gets its own marker, so a missing
 * or misspelt value shows a warning rather than silently hiding one. In a production build `banner` is null and the
 * component renders nothing.
 */
const environment = import.meta.env.VITE_APP_ENVIRONMENT

const banner =
  environment === 'staging' ? { label: 'Staging · not production data', colour: '#b45309', title: '[STAGING] ' }
  : import.meta.env.DEV ? { label: 'Local development', colour: '#334155', title: '[LOCAL] ' }
  : null

/** Overlays the top of the page (never takes part in the layout, never catches a click) and prefixes the tab title. */
export function EnvironmentBanner() {
  useEffect(() => {
    if (banner && !document.title.startsWith(banner.title)) document.title = banner.title + document.title
  }, [])

  if (!banner) return null
  return (
    <div role="status" aria-label={banner.label} style={{
      position: 'fixed', top: 0, left: '50%', transform: 'translateX(-50%)', zIndex: 20000,
      pointerEvents: 'none', maxWidth: '96vw', padding: '6px 28px',
      borderBottomLeftRadius: 12, borderBottomRightRadius: 12, background: banner.colour, color: '#fff',
      font: '700 14px/1.25 system-ui, sans-serif', letterSpacing: '0.12em', textTransform: 'uppercase',
      whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', boxShadow: '0 3px 14px rgba(0, 0, 0, 0.35)',
    }}>
      {banner.label}
    </div>
  )
}
