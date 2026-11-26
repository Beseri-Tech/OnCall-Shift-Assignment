import {
  Alert, Badge, Button, Checkbox, Fieldset, Grid, Group, Modal, NumberInput, Paper, PasswordInput, SimpleGrid, Stack, Table, Text, TextInput,
  Textarea,
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconAlertTriangle, IconCircleCheck, IconId, IconLock, IconShieldCheck } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate } from 'react-router'
import { api, type EtapProfile, type ProfileInput } from '../api'
import { useMe } from '../auth'
import { ErrorBox, Loading, PageHeader } from '../components/common'
import { formatLong } from '../dates'
import { notifyError, notifyOk } from '../lib'

const toInput = (p: EtapProfile): ProfileInput => ({
  fullName: p.fullName, icNo: p.icNo, jawatan: p.jawatan, gred: p.gred, tarikhPewartaanPakar: p.tarikhPewartaanPakar,
  unitTempatBertugas: p.unitTempatBertugas, alamatRumah: p.alamatRumah, jarakKm: p.jarakKm, noGaji: p.noGaji,
  noTelefonPejabat: p.noTelefonPejabat, noTelefonBimbit: p.noTelefonBimbit, noAkaunBank: p.noAkaunBank, namaCawanganBank: p.namaCawanganBank,
})

/** Section A of the ETAP form, as it will be printed. */
const sectionA = (p: EtapProfile): [string, string | null][] => [
  ['1  Nama pegawai', p.fullName], ['2  No. kad pengenalan', p.icNo], ['3  Jawatan', p.jawatan], ['4  Gred jawatan', p.gred],
  ['5  Tarikh pewartaan pakar', p.tarikhPewartaanPakar ? formatLong(p.tarikhPewartaanPakar) : '–'],
  ['6  Unit/tempat bertugas', p.unitTempatBertugas], ['7  Alamat rumah', p.alamatRumah],
  ['8  Jarak (km)', p.jarakKm?.toString() ?? null], ['9  No. gaji', p.noGaji], ['10 No. telefon & samb.', p.noTelefonPejabat ?? '–'],
  ['11 No. telefon bimbit', p.noTelefonBimbit], ['12 E-mel', p.email], ['13 No. akaun bank', p.noAkaunBank],
  ['14 Nama & cawangan bank', p.namaCawanganBank],
]

/** The officer's own details for the ETAP claim form. Only they see it; they verify it before ETAP can be used. */
export function ProfilePage() {
  const me = useMe().data
  const profile = useQuery({ queryKey: ['profile'], queryFn: api.profile.get, enabled: !!me?.personId, gcTime: 0 })
  if (!me?.personId) return <Navigate to="/home" replace />
  if (profile.isLoading) return <Loading />
  if (profile.error) return <ErrorBox error={profile.error} />
  return <ProfileForm profile={profile.data!} />
}

function ProfileForm({ profile }: { profile: EtapProfile }) {
  const qc = useQueryClient()
  const [v, setV] = useState<ProfileInput>(() => toInput(profile))
  const [verifying, setVerifying] = useState(false)
  const set = <K extends keyof ProfileInput>(k: K, value: ProfileInput[K]) => setV(s => ({ ...s, [k]: value }))
  const dirty = JSON.stringify(v) !== JSON.stringify(toInput(profile))

  const save = useMutation({
    mutationFn: () => api.profile.save(v),
    onSuccess: p => { qc.setQueryData(['profile'], p); setV(toInput(p)); notifyOk(p.missing.length ? 'Saved. Some fields are still empty.' : 'Saved. Verify it to use ETAP.') },
    onError: e => notifyError(e, 'Could not save'),
  })

  // Everything is typed in capitals, as on the printed form.
  const field = (k: keyof ProfileInput, label: string, extra?: { required?: boolean; placeholder?: string; max?: number }) => (
    <TextInput label={label} required={extra?.required ?? true} placeholder={extra?.placeholder} maxLength={extra?.max ?? 200}
      value={(v[k] as string | null) ?? ''} onChange={e => set(k, (e.currentTarget.value.toUpperCase() || null) as never)} />
  )
  // The specialist date only applies to specialists: off (and empty) unless they tick the box.
  const [pakar, setPakar] = useState(!!profile.tarikhPewartaanPakar)

  return (
    <Stack gap="md">
      <PageHeader icon={IconId} title="My ETAP profile"
        description="Your details for the ETAP claim form (section A). Only you can see them. Verify them once they're right; any change needs a new verification." />
      {profile.verified
        ? <Alert color="green" variant="light" icon={<IconCircleCheck size={18} />}>Verified{profile.verifiedAt && ` on ${new Date(profile.verifiedAt).toLocaleDateString('en-GB', { dateStyle: 'medium' })}`}. You can use ETAP.</Alert>
        : <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
            Not verified. {profile.missing.length > 0 ? 'Fill in every required field, save, then verify.' : 'Check the details and verify them to use ETAP.'}
          </Alert>}
      <Paper className="panel" p="lg">
        <form onSubmit={e => { e.preventDefault(); save.mutate() }}>
          <Stack gap="md">
            <Fieldset legend="Peribadi">
              <SimpleGrid cols={{ base: 1, sm: 2 }}>
                {field('fullName', 'Nama penuh (as in IC)')}
                {field('icNo', 'No. kad pengenalan', { placeholder: '900101-09-1234', max: 20 })}
              </SimpleGrid>
            </Fieldset>

            <Fieldset legend="Jawatan">
              <SimpleGrid cols={{ base: 1, sm: 2 }}>
                {field('jawatan', 'Jawatan', { placeholder: 'PEGAWAI PERGIGIAN', max: 100 })}
                {field('gred', 'Gred jawatan', { placeholder: 'UD10', max: 20 })}
                {field('unitTempatBertugas', 'Unit / tempat bertugas', { max: 300 })}
                {field('noGaji', 'No. gaji', { max: 30 })}
              </SimpleGrid>
              <Stack gap={6} mt="md">
                <Checkbox label="I am a specialist (pakar): specialist rates apply" checked={pakar}
                  onChange={e => { const on = e.currentTarget.checked; setPakar(on); if (!on) set('tarikhPewartaanPakar', null) }} />
                <DatePickerInput label="Tarikh pewartaan pakar" required={pakar} disabled={!pakar} maw={260} clearable
                  placeholder={pakar ? 'Pick the date' : 'Not a specialist'} value={v.tarikhPewartaanPakar} valueFormat="D MMM YYYY"
                  onChange={d => set('tarikhPewartaanPakar', (d as string) ?? null)} />
              </Stack>
            </Fieldset>

            <Fieldset legend="Alamat">
              <Grid gap="md">
                <Grid.Col span={{ base: 12, sm: 9 }}>
                  <Textarea label="Alamat rumah" required autosize minRows={2} maxLength={400} value={v.alamatRumah ?? ''}
                    onChange={e => set('alamatRumah', e.currentTarget.value.toUpperCase() || null)} />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <NumberInput label="Jarak (km)" placeholder="Home to workplace" required min={0} max={10000} decimalScale={1}
                    value={v.jarakKm ?? ''} onChange={x => set('jarakKm', typeof x === 'number' ? x : null)} />
                </Grid.Col>
              </Grid>
            </Fieldset>

            <Fieldset legend="Hubungan">
              <SimpleGrid cols={{ base: 1, sm: 3 }}>
                {field('noTelefonPejabat', 'No. telefon & samb. (pejabat)', { required: false, max: 40 })}
                {field('noTelefonBimbit', 'No. telefon bimbit', { max: 32 })}
                <TextInput label="E-mel (sign-in)" value={profile.email ?? ''} readOnly variant="filled" />
              </SimpleGrid>
            </Fieldset>

            <Fieldset legend="Bank">
              <SimpleGrid cols={{ base: 1, sm: 2 }}>
                {field('noAkaunBank', 'No. akaun bank', { max: 30 })}
                {field('namaCawanganBank', 'Nama & cawangan bank', { placeholder: 'MAYBANK KANGAR', max: 150 })}
              </SimpleGrid>
            </Fieldset>


            <Group justify="space-between">
              <Group gap={6}><IconLock size={14} /><Text size="xs" c="dimmed">Only you can see these details.</Text></Group>
              <Group>
                <Button type="submit" variant={dirty ? 'filled' : 'default'} loading={save.isPending} disabled={!dirty}>Save</Button>
                <Button leftSection={<IconShieldCheck size={16} />} disabled={dirty || profile.missing.length > 0 || profile.verified}
                  onClick={() => setVerifying(true)}>
                  {profile.verified ? 'Verified' : 'Review and verify'}
                </Button>
              </Group>
            </Group>
          </Stack>
        </form>
      </Paper>
      {verifying && <VerifyModal profile={profile} onClose={() => setVerifying(false)} />}
    </Stack>
  )
}

function VerifyModal({ profile, onClose }: { profile: EtapProfile; onClose: () => void }) {
  const qc = useQueryClient()
  const [confirm, setConfirm] = useState(false)
  const [password, setPassword] = useState('')
  const verify = useMutation({
    mutationFn: () => api.profile.verify(password, confirm),
    onSuccess: p => { qc.setQueryData(['profile'], p); onClose(); notifyOk('Your profile is verified. You can use ETAP.') },
  })
  return (
    <Modal opened onClose={onClose} title="Verify your ETAP details" size="lg">
      <form onSubmit={e => { e.preventDefault(); verify.mutate() }}>
        <Stack>
          <Text size="sm">This is section A of your ETAP form as it will be printed.</Text>
          <Paper withBorder>
            <Table verticalSpacing={4}>
              <Table.Tbody>
                {sectionA(profile).map(([k, val]) => (
                  <Table.Tr key={k}><Table.Td w={220}><Text size="sm" c="dimmed">{k}</Text></Table.Td><Table.Td><Text size="sm" fw={500}>{val}</Text></Table.Td></Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Paper>
          <Checkbox label="These details are mine and correct." checked={confirm} onChange={e => setConfirm(e.currentTarget.checked)} />
          <PasswordInput label="Your password" required value={password} onChange={e => setPassword(e.currentTarget.value)}
            error={verify.error?.message} autoComplete="current-password" />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" leftSection={<IconShieldCheck size={16} />} loading={verify.isPending} disabled={!confirm || !password}>
              Verify
            </Button>
          </Group>
          <Badge variant="light" color="gray" style={{ alignSelf: 'flex-start' }}>Changing anything later needs a new verification</Badge>
        </Stack>
      </form>
    </Modal>
  )
}
