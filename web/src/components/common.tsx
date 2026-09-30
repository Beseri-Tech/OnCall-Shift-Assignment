import { Alert, Badge, Center, Loader, Select } from '@mantine/core'
import { IconAlertTriangle } from '@tabler/icons-react'
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

const statusColor: Record<PeriodStatus, string> = { Open: 'green', Locked: 'orange', Review: 'violet', Published: 'blue' }

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
