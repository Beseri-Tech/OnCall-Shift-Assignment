import { Alert, Badge, Card, Center, Group, Loader, Select, Text, ThemeIcon, Title } from '@mantine/core'
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

const statusColor: Record<PeriodStatus, string> = { Open: 'green', Locked: 'orange', Review: 'violet', Published: 'brand' }

export function StatusBadge({ status }: { status: PeriodStatus }) {
  return <Badge color={statusColor[status]} variant="light">{status === 'Review' ? 'In review' : status}</Badge>
}

export function PeriodSelect({ periods, value, onChange, label = 'Rota period' }: {
  periods: Period[]; value: string | null; onChange: (id: string | null) => void; label?: string
}) {
  return (
    <Select
      label={label}
      data={periods.map(p => ({ value: p.id, label: `${p.name} · ${p.status}` }))}
      value={value}
      onChange={onChange}
      allowDeselect={false}
      w={260}
    />
  )
}

/** Page title with an icon tile; controls (filters, pickers) sit on the right and wrap below on small screens. */
export function PageHeader({ icon: I, title, description, children }: {
  icon: Icon; title: ReactNode; description?: ReactNode; children?: ReactNode
}) {
  return (
    <Group justify="space-between" align="flex-end" wrap="wrap" gap="md">
      <Group gap="md" wrap="nowrap" align="center">
        <div className="page-icon"><I size={24} stroke={1.8} /></div>
        <div>
          <Title order={2} c="brand.9" lh={1.2}>{title}</Title>
          {description && <Text c="dimmed" size="sm">{description}</Text>}
        </div>
      </Group>
      {children && <Group align="flex-end" wrap="wrap" gap="sm">{children}</Group>}
    </Group>
  )
}

/** A number with a coloured icon, e.g. tally counts or points used. */
export function StatCard({ icon: I, color = 'brand', label, value, hint, children }: {
  icon: Icon; color?: string; label: string; value?: ReactNode; hint?: ReactNode; children?: ReactNode
}) {
  return (
    <Card className="panel" padding="md">
      <Group gap="sm" wrap="nowrap" align="flex-start">
        <ThemeIcon size={40} radius="md" variant="light" color={color}><I size={22} stroke={1.8} /></ThemeIcon>
        <div style={{ minWidth: 0, flex: 1 }}>
          <Text size="xs" c="dimmed" fw={700} tt="uppercase">{label}</Text>
          {value !== undefined && <Text fw={800} fz={26} lh={1.15}>{value}</Text>}
          {hint && <Text size="xs" c="dimmed">{hint}</Text>}
          {children}
        </div>
      </Group>
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
