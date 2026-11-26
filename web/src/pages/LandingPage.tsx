import { IconArrowsExchange, IconBell, IconCalendarEvent, IconCheck, IconClipboardList, IconClock, IconLock, IconMail, IconPlayerPause, IconPlayerPlay, IconReceipt, IconX } from '@tabler/icons-react'
import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { Link } from 'react-router'
import { APP_VERSION } from '../changelog'
import './landing.css'

// Deal (taste-skill): software page, triptych hero, nav-centre, surface-pairs, solid buttons, close-type,
// footer-columns, moment liquid-type on the close. Palette is Rota-OC's own teal; type IBM Plex Sans.

/** Logo and name. */
export function Wordmark({ size = 32 }: { size?: number }) {
  return (
    <span className="wordmark">
      <img src="/favicon.svg" width={size} height={size} alt="" />
      <span>Rota-OC</span>
    </span>
  )
}

// Sample month: December 2026 starts on a Tuesday. Names are the launch video's made-up cast.
const OFFICERS = ['Aina', 'Farid', 'Mei Ling', 'Ravi', 'Hana', 'Daniel', 'Siti']
const LEAVE = new Set([8, 9, 17])
const PREFERRED = new Set([4, 12, 22])
const HOLIDAY = 25

/** A rota month drawn the way the app draws it. Decorative: the real rota lives in the app. */
export function RotaPreview({ days = 31, lead = 1, leaveOnly = false, animate = false }: { days?: number; lead?: number; leaveOnly?: boolean; animate?: boolean }) {
  return (
    <div className={`rota-preview${animate ? ' is-anim' : ''}`} aria-hidden="true">
      <div className="rota-head">{['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map(d => <span key={d}>{d}</span>)}</div>
      <div className="rota-grid">
        {Array.from({ length: lead }, (_, i) => <span key={`b${i}`} />)}
        {Array.from({ length: days }, (_, i) => {
          const day = i + 1
          const weekend = (lead + i) % 7 >= 5
          const kind = day === HOLIDAY ? 'ph' : LEAVE.has(day) ? 'leave' : PREFERRED.has(day) ? 'pref' : 'shift'
          const label = kind === 'leave' ? 'Leave' : kind === 'pref' && leaveOnly ? 'Preferred' : leaveOnly ? '' : OFFICERS[(i * 3) % OFFICERS.length]
          return (
            <span key={day} className={`rota-cell${weekend ? ' weekend' : ''}`}>
              <i>{day}</i>
              {label && <em className={`chip chip-${kind}`} style={{ '--i': i } as CSSProperties}>{label}</em>}
            </span>
          )
        })}
      </div>
    </div>
  )
}

/** Mounts the dealt taste-skill moment (liquid-type) with the kit's own loader, served from /assets/moments. */
function useMoment<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  useEffect(() => {
    const el = ref.current as (T & { __moment?: { destroy?: () => void } }) | null
    if (!el) return
    const loader = '/assets/moments/moments.js'
    // The loader mounts once per element; the headline stays plain type if it cannot load.
    import(/* @vite-ignore */ loader).then(m => m.mount(el)).catch(() => {})
    // Destroy only once the headline has left the page (StrictMode re-runs effects on the same element).
    return () => { setTimeout(() => { if (!el.isConnected) el.__moment?.destroy?.() }) }
  }, [])
  return ref
}

/** True once the element has scrolled into view (stays true). */
function useInView<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  const [seen, setSeen] = useState(false)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    const io = new IntersectionObserver(([e]) => { if (e.isIntersecting) { setSeen(true); io.disconnect() } }, { threshold: 0.3 })
    io.observe(el)
    return () => io.disconnect()
  }, [])
  return [ref, seen] as const
}

/** True while the element is on screen. */
function useOnScreen<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  const [on, setOn] = useState(false)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    const io = new IntersectionObserver(([e]) => setOn(e.isIntersecting))
    io.observe(el)
    return () => io.disconnect()
  }, [])
  return [ref, on] as const
}

/** The launch video in a native dialog: Escape or the close button ends it. */
function TourButton() {
  const dialog = useRef<HTMLDialogElement>(null)
  const video = useRef<HTMLVideoElement>(null)
  const open = () => { dialog.current?.showModal(); video.current?.play().catch(() => {}) }
  const close = () => dialog.current?.close()
  return (
    <>
      <button type="button" className="lp-tour" onClick={open}>
        <span className="lp-tour-icon"><IconPlayerPlay size={18} /></span>
        Watch the 1-minute tour
      </button>
      <dialog ref={dialog} className="lp-dialog" aria-label="Rota-OC tour video"
        onClose={() => video.current?.pause()} onClick={e => { if (e.target === dialog.current) close() }}>
        <button type="button" className="lp-dialog-close" onClick={close} aria-label="Close the tour"><IconX size={20} /></button>
        <video ref={video} src="/media/rota-oc-tour.mp4" poster="/media/rota-oc-tour-poster.jpg" controls playsInline preload="none" />
      </dialog>
    </>
  )
}

/** Hero collage: three sets of app pieces, one every 2.8 s, with a pause button. Holds set one under reduced motion. */
function Collage() {
  const [ref, onScreen] = useOnScreen<HTMLDivElement>()
  const [set, setSet] = useState(0)
  const [paused, setPaused] = useState(() => matchMedia('(prefers-reduced-motion: reduce)').matches)
  useEffect(() => {
    if (paused || !onScreen) return
    const t = setInterval(() => setSet(s => (s + 1) % 3), 2800)
    return () => clearInterval(t)
  }, [paused, onScreen])
  const on = (i: number) => `lp-set${set === i ? ' is-on' : ''}`

  return (
    <div ref={ref} className="lp-collage" role="img" aria-label="Rota-OC screens: an officer's leave and points, the admin publishing a balanced rota, and an ETAP claim with the case log">
      <div className={on(0)} aria-hidden="true">
        <div className="lp-piece" style={{ left: '4%', top: '5%', width: '62%' }}>
          <b className="lp-piece-title">Your leave, December</b>
          <RotaPreview days={21} leaveOnly />
        </div>
        <div className="lp-piece" style={{ left: '60%', top: '12%', width: '36%' }}>
          <b className="lp-piece-title">Leave points</b>
          <strong className="lp-big">7 <small>of 12 left</small></strong>
          <span className="lp-bar"><i style={{ width: '42%' }} /></span>
          <span className="lp-row"><span>3 leave days</span><span>3 pts</span></span>
          <span className="lp-row"><span>2 preferred days</span><span>2 pts</span></span>
        </div>
        <div className="lp-piece" style={{ left: '10%', top: '60%', width: '56%' }}>
          <b className="lp-piece-title">Swap request</b>
          <span>Farid asks to swap 12 Dec for your 19 Dec.</span>
          <span className="lp-actions"><span className="lp-mini lp-mini-text">Decline</span><span className="lp-mini lp-mini-on">Accept</span></span>
        </div>
      </div>

      <div className={on(1)} aria-hidden="true">
        <div className="lp-piece" style={{ left: '4%', top: '6%', width: '50%' }}>
          <b className="lp-piece-title">January 2027</b>
          <span className="lp-check"><IconCheck size={14} />Leave locked</span>
          <span className="lp-check"><IconCheck size={14} />Draft generated, 31 days</span>
          <span className="lp-mini lp-mini-on lp-mini-wide">Publish to 14 officers</span>
        </div>
        <div className="lp-piece" style={{ left: '50%', top: '34%', width: '46%' }}>
          <b className="lp-piece-title">Balance</b>
          <span className="lp-row lp-row-head"><span>Officer</span><span>Days</span><span>Wknd</span><span>PH</span></span>
          {[['Aina', 4, 1, 0], ['Farid', 4, 2, 1], ['Mei Ling', 5, 1, 0], ['Ravi', 4, 1, 1], ['Hana', 5, 2, 0]].map(([n, d, w, p]) => (
            <span key={n} className="lp-row"><span>{n}</span><span>{d}</span><span>{w}</span><span>{p}</span></span>
          ))}
        </div>
        <div className="lp-piece lp-toast" style={{ left: '6%', top: '72%', width: '48%' }}>
          <IconBell size={18} />
          <span>Rota published. Your January dates are in My On-Call.</span>
        </div>
      </div>

      <div className={on(2)} aria-hidden="true">
        <div className="lp-piece" style={{ left: '4%', top: '6%', width: '46%' }}>
          <b className="lp-piece-title">ETAP Claim, November</b>
          {['3 Nov', '9 Nov', '15 Nov', '22 Nov'].map(d => <span key={d} className="lp-row"><span>{d}</span><span>On call</span></span>)}
          <span className="lp-row lp-row-total"><span>6 on-call days</span><span>Ready</span></span>
        </div>
        <div className="lp-piece" style={{ left: '54%', top: '16%', width: '42%' }}>
          <b className="lp-piece-title">Case log</b>
          <span className="lp-row"><span>Facial swelling</span><span>2 Dec</span></span>
          <span className="lp-row"><span>Dental trauma</span><span>5 Dec</span></span>
          <span className="lp-row"><span>Post-extraction bleed</span><span>9 Dec</span></span>
        </div>
        <div className="lp-piece" style={{ left: '22%', top: '66%', width: '52%' }}>
          <b className="lp-piece-title">February leave opens in</b>
          <strong className="lp-big">2 days 4 hours</strong>
          <span className="lp-muted">Reminder a day and an hour before</span>
        </div>
      </div>

      <button type="button" className="lp-pause" onClick={() => setPaused(p => !p)} aria-label={paused ? 'Play the screens' : 'Pause the screens'}>
        {paused ? <IconPlayerPlay size={16} /> : <IconPlayerPause size={16} />}
      </button>
    </div>
  )
}

/** Live countdown to the period opening (sample time). */
function Countdown() {
  const [left, setLeft] = useState(2 * 86400 + 4 * 3600 + 12 * 60 + 37)
  useEffect(() => {
    const t = setInterval(() => setLeft(v => Math.max(0, v - 1)), 1000)
    return () => clearInterval(t)
  }, [])
  const two = (n: number) => String(n).padStart(2, '0')
  const parts: [string, string][] = [
    [String(Math.floor(left / 86400)), 'days'], [two(Math.floor(left % 86400 / 3600)), 'hours'],
    [two(Math.floor(left % 3600 / 60)), 'min'], [two(left % 60), 'sec'],
  ]
  return <div className="lp-count">{parts.map(([v, l]) => <span key={l}><b>{v}</b><small>{l}</small></span>)}</div>
}

// The stages of one rota period, as the app runs them, each with the screen it shows.
const STAGES: { title: string; text: string; sr: string; screen: ReactNode }[] = [
  { title: 'Rota release announced', text: 'Officers are told by app and email when the next period opens.', sr: 'The app shows a notice that the January rota opens on 1 December at 09:00, an email sent to 14 officers, and reminders set.', screen: <>
    <b className="lp-scr-title">Notifications</b>
    <span className="lp-piece lp-toast lp-pop"><IconBell size={18} /><span>January rota opens 1 Dec at 09:00.</span></span>
    <span className="lp-piece lp-toast lp-pop lp-d2"><IconMail size={18} /><span>Email sent to 14 officers.</span></span>
    <span className="lp-piece lp-toast lp-pop lp-d3"><IconCalendarEvent size={18} /><span>Reminders set for a day and an hour before.</span></span>
  </> },
  { title: 'Rota period opens', text: 'A countdown runs on the Leave page, and the page unlocks on time.', sr: 'The Leave page counts down to opening, with the day-before reminder sent and the hour-before reminder still to come.', screen: <>
    <b className="lp-scr-title">January leave opens in</b>
    <Countdown />
    <span className="lp-check lp-pop lp-d2"><IconCheck size={14} />Reminder a day before, sent</span>
    <span className="lp-check lp-check-wait lp-pop lp-d3"><IconClock size={14} />Reminder an hour before</span>
  </> },
  { title: 'Leave entered for the rota', text: 'Officers pick leave and preferred days, each paid from the same points allowance.', sr: "An officer's January calendar shows leave and preferred days, with 7 of 12 points left.", screen: <>
    <b className="lp-scr-title">Your leave, January</b>
    <RotaPreview days={14} lead={4} leaveOnly animate />
    <span className="lp-piece lp-pop lp-d3 lp-points-row"><span>7 of 12 points left</span><span className="lp-bar"><i className="lp-bar-grow" style={{ width: '58%' }} /></span></span>
  </> },
  { title: 'Leave locked for the rota', text: 'The admin locks leave, so the draft works from fixed dates.', sr: 'January leave is locked for 14 officers, with 23 leave days and 11 preferred days.', screen: <>
    <span className="lp-big-icon lp-pop"><IconLock size={32} stroke={1.5} /></span>
    <b className="lp-scr-title lp-center">January leave locked</b>
    <span className="lp-piece lp-pop lp-d2">
      <span className="lp-row"><span>Officers</span><span>14</span></span>
      <span className="lp-row"><span>Leave days</span><span>23</span></span>
      <span className="lp-row"><span>Preferred days</span><span>11</span></span>
    </span>
  </> },
  { title: 'Draft rota generated', text: 'One click spreads weekends, public holidays and peak days evenly across everyone.', sr: 'The draft January rota fills in day by day, balanced across 14 officers.', screen: <>
    <b className="lp-scr-title">Draft, January 2027</b>
    <RotaPreview days={21} lead={4} animate />
    <span className="lp-tag lp-pop lp-d4">Balanced across 14 officers</span>
  </> },
  { title: 'Rota review and swaps', text: 'The admin adjusts any day, and officers swap shifts with a colleague in two taps.', sr: 'Farid asks to swap 12 January for 19 January; the swap is accepted and both rotas update.', screen: <>
    <b className="lp-scr-title">Swap request</b>
    <span className="lp-piece lp-pop">
      <span>Farid asks to swap 12 Jan for your 19 Jan.</span>
      <span className="lp-actions"><span className="lp-mini lp-mini-text">Decline</span><span className="lp-mini lp-mini-on lp-press">Accept</span></span>
    </span>
    <span className="lp-piece lp-toast lp-late"><IconArrowsExchange size={18} /><span>Swap done. Both rotas are updated.</span></span>
  </> },
  { title: 'Rota published', text: 'Everyone is notified the moment it goes live, and every later change stays on record.', sr: 'The January rota is published, sent to 14 officers by app and email, and later changes go to the audit log.', screen: <>
    <span className="lp-big-icon lp-big-icon-on lp-pop"><IconCheck size={36} stroke={2} /></span>
    <b className="lp-scr-title lp-center">January rota published</b>
    <span className="lp-piece lp-toast lp-pop lp-d2"><IconBell size={18} /><span>Sent to 14 officers by app and email.</span></span>
    <span className="lp-piece lp-toast lp-pop lp-d3"><IconClipboardList size={18} /><span>Every change from here is kept in the audit log.</span></span>
  </> },
]

/** The stages scroll past on the left while a pinned app screen shows the one in the middle of the view. */
function Journey() {
  const [active, setActive] = useState(0)
  const [pinRef, pinOnScreen] = useOnScreen<HTMLDivElement>()
  const go = (i: number) => items.current[i]?.scrollIntoView({ block: 'center', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' })
  const items = useRef<(HTMLLIElement | null)[]>([])
  useEffect(() => {
    const io = new IntersectionObserver(entries => {
      for (const e of entries) if (e.isIntersecting) setActive(Number((e.target as HTMLElement).dataset.i))
    }, { rootMargin: '-50% 0px -50% 0px' })
    items.current.forEach(el => el && io.observe(el))
    return () => io.disconnect()
  }, [])
  return (
    <div className="lp-journey" style={{ '--p': (active + 1) / STAGES.length } as CSSProperties}>
      <div ref={pinRef} className="lp-journey-pin">
        <div className="lp-journey-screen" data-ui aria-hidden="true">
          <div className="lp-app-bar">
            <span className="lp-app-brand"><img src="/favicon.svg" width={22} height={22} alt="" />Rota-OC</span>
            <span className="lp-journey-step">Stage {active + 1} of {STAGES.length}</span>
          </div>
          <div key={active} className="lp-journey-body">{pinOnScreen && STAGES[active].screen}</div>
        </div>
      </div>
      <ol className="lp-journey-list">
        {STAGES.map((st, i) => (
          <li key={st.title} data-i={i} ref={el => { items.current[i] = el }} className={i === active ? 'is-on' : i < active ? 'is-done' : ''}>
            <button type="button" className="lp-journey-num" onClick={() => go(i)} aria-label={`Go to stage ${i + 1}: ${st.title}`} aria-current={i === active ? 'step' : undefined}>{i + 1}</button>
            <div>
              <h3>{st.title}</h3>
              <p>{st.text}</p>
              <p className="lp-sr">{st.sr}</p>
            </div>
          </li>
        ))}
      </ol>
    </div>
  )
}

const FEATURES = [
  { icon: IconArrowsExchange, title: 'Shift swaps in two taps', text: 'Ask a colleague. When they accept, both rotas update.',
    ui: <span className="lp-row"><span>Swap 12 Dec, Farid</span><span className="lp-tag">Accepted</span></span> },
  { icon: IconBell, title: 'Alerts by app and email', text: 'Release times, reminders and swaps reach every officer.',
    ui: <span className="lp-row"><span>Leave opens 09:00</span><span className="lp-tag">Email sent</span></span> },
  { icon: IconClipboardList, title: 'A case log for each shift', text: 'Each case is recorded against its on-call shift.',
    ui: <span className="lp-row"><span>Dental trauma, 5 Dec</span><span className="lp-tag">Logged</span></span> },
  { icon: IconReceipt, title: 'ETAP Claim from worked shifts', text: 'Built from the on-call days you worked.',
    ui: <span className="lp-row"><span>November, 6 days</span><span className="lp-tag">Ready</span></span> },
]

const FAQ = [
  { q: 'How do I get an account?', a: 'Your department admin adds you and sends a temporary password. You choose your own password the first time you sign in.' },
  { q: 'How do leave points work?', a: 'Leave and preferred days use points from an allowance that is the same for every officer. The full rules are on the Help page once you sign in.' },
  { q: 'Can I change a shift after the rota is published?', a: 'Yes. Send a swap request to a colleague. When they accept, the rota updates for both of you.' },
  { q: 'When can I enter leave for next month?', a: 'When the admin opens the period. If an opening time is set, you see a countdown and get a reminder a day and an hour before.' },
]

export function LandingPage() {
  const closeRef = useMoment<HTMLHeadingElement>()
  const [rotaRef, rotaSeen] = useInView<HTMLDivElement>()
  return (
    <div className="lp">
      <a className="lp-skip" href="#main">Skip to content</a>
      <header className="nav lp-nav">
        <div className="wrap lp-wrap lp-nav-row">
          <Link to="/" className="lp-home" aria-label="Rota-OC home"><Wordmark /></Link>
          <nav aria-label="Page">
            <a href="#plan">How it works</a>
            <a href="#features">Features</a>
            <a href="#faq">Questions</a>
          </nav>
          <Link to="/login" className="lp-btn lp-btn-sm">Sign in to your rota</Link>
        </div>
      </header>

      <main id="main">
        <section className="hero lp-hero" data-enter aria-labelledby="hero-h">
          <div className="wrap lp-wrap">
          <div className="hero-text lp-hero-grid">
            <div className="lp-hero-text">
              <h1 id="hero-h">Automatic on-call rotas for dental officers</h1>
              <p>Rota-OC builds a fair on-call schedule from each officer's leave and preferred days, balances weekends and public holidays, and takes each month from release to published rota in 7 stages.</p>
              <TourButton />
            </div>
            <Collage />
          </div>
          </div>
        </section>

        <section id="rota" className="section lp-section" aria-labelledby="rota-h">
          <div className="wrap lp-wrap">
            <h2 id="rota-h">The month rota, balanced by points. <span className="lp-muted">Everyone sees the same page.</span></h2>
            <div ref={rotaRef} className="lp-app app-frame" data-ui data-anim={rotaSeen ? "in" : "wait"} aria-hidden="true">
              <div className="lp-app-bar">
                <span className="lp-app-brand"><img src="/favicon.svg" width={22} height={22} alt="" />Rota-OC</span>
                <span className="lp-app-tabs"><span>Leave</span><span className="on">Rota</span><span>My On-Call</span><span>Cases</span><span>ETAP Claim</span></span>
              </div>
              <div className="lp-app-body">
                <div className="lp-app-list">
                  <b className="lp-piece-title">December 2026</b>
                  {[['Aina', 5, 1], ['Farid', 4, 2], ['Mei Ling', 5, 1], ['Ravi', 4, 1], ['Hana', 4, 2], ['Daniel', 5, 1], ['Siti', 4, 1]].map(([n, d, w]) => (
                    <span key={n} className="lp-row"><span>{n}</span><span>{d} days, {w} wknd</span></span>
                  ))}
                </div>
                <div className="lp-app-month">
                  <RotaPreview animate={rotaSeen} />
                  <div className="rota-legend">
                    <span><i className="chip-shift" />On call</span>
                    <span><i className="chip-leave" />Leave</span>
                    <span><i className="chip-pref" />Preferred</span>
                    <span><i className="chip-ph" />Public holiday</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section id="plan" className="section lp-section lp-surface lp-journey-section" aria-labelledby="plan-h">
          <div className="wrap lp-wrap">
            <h2 id="plan-h">One rota period, from release to published rota</h2>
            <Journey />
          </div>
        </section>

        <section id="features" className="section lp-section" aria-labelledby="features-h">
          <div className="wrap lp-wrap">
            <h2 id="features-h">Rota swaps, case log and ETAP Claim</h2>
            <div className="lp-features">
              {FEATURES.map(({ icon: Icon, title, text, ui }, i) => (
                <div key={title} className={`lp-feature lp-feature-${i}`}>
                  <div className="lp-feature-ui" data-ui aria-hidden="true">
                    <span className="lp-feature-icon"><Icon size={22} stroke={1.75} /></span>
                    <span className="lp-piece">{ui}</span>
                  </div>
                  <h3>{title}</h3>
                  <p>{text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section id="faq" className="section lp-section lp-surface" aria-labelledby="faq-h">
          <div className="wrap lp-wrap lp-faq">
            <div className="lp-faq-side">
              <h2 id="faq-h">Questions from officers</h2>
              <div className="lp-inbox" data-ui aria-hidden="true">
                <span className="lp-piece lp-toast"><IconBell size={18} /><span>January leave opens tomorrow at 09:00.</span></span>
                <span className="lp-piece lp-toast"><IconArrowsExchange size={18} /><span>Farid accepted your swap for 12 Dec.</span></span>
              </div>
              <Link to="/login" className="lp-btn lp-btn-lg">Sign in to your rota</Link>
            </div>
            <dl>
              {FAQ.map(f => (
                <div key={f.q}>
                  <dt>{f.q}</dt>
                  <dd>{f.a}</dd>
                </div>
              ))}
            </dl>
          </div>
        </section>
      </main>

      <footer className="closing lp-closing" aria-labelledby="close-h">
        <div className="wrap lp-wrap lp-closing-wrap">
          <div className="lp-close">
            <h2 id="close-h" ref={closeRef} data-moment="liquid-type">Fair rotas, fewer arguments</h2>
            <p>One admin plans the month in minutes, and every officer sees it the moment it is published.</p>
            <Link to="/login" className="lp-btn lp-btn-lg lp-btn-inv">Sign in to your rota</Link>
          </div>
          <div className="lp-footer">
            <div className="lp-footer-cols">
              <div><Wordmark size={26} /></div>
              <div>
                <b>Account</b>
                <Link to="/forgot-password">Reset your password</Link>
              </div>
              <div>
                <b>On this page</b>
                <a href="#plan">How it works</a>
                <a href="#faq">Questions</a>
              </div>
            </div>
            <div className="lp-legal">
              <span>Developed by Beseri-Tech</span>
              <span>Version {APP_VERSION}</span>
            </div>
          </div>
        </div>
      </footer>
    </div>
  )
}
