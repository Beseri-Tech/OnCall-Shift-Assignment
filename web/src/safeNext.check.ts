// Run: node src/safeNext.check.ts   (Node 22.6+ strips the types)
import { safeNext } from './safeNext.ts'

const cases: [string | null, string][] = [
  ['/rota', '/rota'],
  ['/admin/people?tab=2', '/admin/people?tab=2'],
  [null, '/home'],
  ['', '/home'],
  ['/', '/home'],
  ['/login', '/home'],
  ['/login?next=/rota', '/home'],
  ['/reset-password?token=x', '/home'],
  ['//evil.example', '/home'],
  ['/' + String.fromCharCode(92) + 'evil.example', '/home'], // a backslash after the slash: browsers read it as //
  ['https://evil.example/rota', '/home'],
  ['rota', '/home'],
]
for (const [input, want] of cases) {
  const got = safeNext(input)
  if (got !== want) throw new Error(`safeNext(${JSON.stringify(input)}) gave ${got}, wanted ${want}`)
}
console.log(`safeNext: ${cases.length} cases pass`)
