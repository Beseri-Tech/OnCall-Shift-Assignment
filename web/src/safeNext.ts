// No imports, so src/safeNext.check.ts can run it under plain Node.
const PUBLIC = ['/', '/login', '/forgot-password', '/reset-password']

/** Where to go after signing in: a page of this app that needs sign-in, otherwise /home. */
export function safeNext(next: string | null): string {
  // Only same-site paths: "//host" and "/\host" would leave the site.
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.startsWith('/\\')) return '/home'
  return PUBLIC.includes(next.split(/[?#]/)[0]) ? '/home' : next
}
