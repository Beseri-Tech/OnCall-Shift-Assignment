import { Alert, Card, Center, Group, Loader, Select, Text, Title } from '@mantine/core'
import { IconAlertTriangle, type Icon } from '@tabler/icons-react'
import type { ReactNode } from 'react'
import type { Period, PeriodStatus } from '../api'

export function Loading() {
  return <Center py="xl"><Loader /></Center>
}

export function ErrorBox({ error }: { error: unknown }) {
  return (
    <Alert color="red" icon={<IconAlertTriangle />} title="Could not load">
      {error instanceof Error ? error.message : String(error)}
    </Alert>
  )
}

const statusLabel: Record<PeriodStatus, string> = { Open: 'Open', Locked: 'Locked', Review: 'In review', Published: 'Published' }

/** One quiet style for every period status: neutral chip with a small coloured dot (see .status-badge in styles.css). */
export function StatusBadge({ status }: { status: PeriodStatus }) {
  return <span className={`status-badge status-${status.toLowerCase()}`}><span className="status-dot" />{statusLabel[status]}</span>
}

export function PeriodSelect({ periods, value, onChange, label = 'Rota period' }: {
  periods: Period[]; value: string | null; onChange: (id: string | null) => void; label?: string
}) {
  return (
    <Select
      label={label}
      data={periods.map(p => ({ value: p.id, label: `${p.name} (${statusLabel[p.status]})` }))}
      value={value}
      onChange={onChange}
      allowDeselect={false}
      w={260}
    />
  )
}

/** Page title and optional description; controls (filters, pickers) sit on the right and wrap below on small screens. `icon` is accepted for older callers and not drawn. */
export function PageHeader({ title, description, children }: {
  icon?: Icon; title: ReactNode; description?: ReactNode; children?: ReactNode
}) {
  return (
    <Group justify="space-between" align="flex-end" wrap="wrap" gap="md">
      <div style={{ minWidth: 0 }}>
        <Title order={2} fw={500} c="var(--ink)">{title}</Title>
        {description && <Text c="var(--text-2)" fz={16} mt={4}>{description}</Text>}
      </div>
      {children && <Group align="flex-end" wrap="wrap" gap="sm">{children}</Group>}
    </Group>
  )
}

/** A number card: sentence-case label, tabular value, optional hint. `color="red"` tints the value (over a limit); other colours and `icon` are ignored. */
export function StatCard({ color, label, value, hint, children }: {
  icon?: Icon; color?: string; label: string; value?: ReactNode; hint?: ReactNode; children?: ReactNode
}) {
  return (
    <Card className="panel" padding="md" h="100%">
      <Text fz={14} fw={500} c="var(--text-2)">{label}</Text>
      {value !== undefined && (
        <Text fz={28} fw={500} lh={1.2} mt={4} c={color === 'red' ? 'red.8' : 'var(--ink)'} style={{ fontVariantNumeric: 'tabular-nums' }}>{value}</Text>
      )}
      {hint && <Text fz={14} c="var(--text-3)" mt={4}>{hint}</Text>}
      {children}
    </Card>
  )
}

export function Legend({ bg, border, label }: { bg: string; border: string; label: ReactNode }) {
  return (
    <Group gap={6} wrap="nowrap">
      <span className="legend-swatch" style={{ background: bg, borderColor: border }} />
      <Text size="xs">{label}</Text>
    </Group>
  )
}
