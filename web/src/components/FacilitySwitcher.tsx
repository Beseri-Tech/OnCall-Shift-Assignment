import { Select } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconBuildingHospital } from '@tabler/icons-react'
import { api } from '../api'
import { useMe, useSwitchFacility } from '../auth'

/** Header picker for masters. */
export function FacilitySwitcher({ w = 200 }: { w?: number | string }) {
  const me = useMe().data
  const facilities = useQuery({ queryKey: ['facilities'], queryFn: api.facilities })
  const switchTo = useSwitchFacility()
  return (
    <Select size="xs" w={w} aria-label="Hospital" placeholder="All hospitals" allowDeselect={false}
      leftSection={<IconBuildingHospital size={14} />} value={me?.facility?.id ?? ''} disabled={switchTo.isPending}
      data={[{ value: '', label: 'All hospitals' }, ...(facilities.data ?? []).map(f => ({ value: f.id, label: `${f.code} · ${f.name}` }))]}
      onChange={v => v !== (me?.facility?.id ?? '') && switchTo.mutate(v || null)} />
  )
}
