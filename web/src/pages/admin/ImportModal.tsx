import { Alert, Badge, Button, FileButton, Group, List, Modal, Paper, Stack, Table, Text, Textarea } from '@mantine/core'
import { useMutation } from '@tanstack/react-query'
import { IconAlertTriangle, IconDownload, IconUpload } from '@tabler/icons-react'
import { useState } from 'react'
import { api, type Clinic, type ImportResult } from '../../api'
import { notifyError, notifyOk } from '../../lib'
import type { IssuedLogin } from './TempPasswords'

const HEADER = ['Name', 'Code', 'Email', 'Role', 'Phone', 'State', 'District', 'Clinic']

const csvField = (s: string) => (/[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s)

/** Header plus two example rows placed in a real clinic, so the file shows the expected spelling. */
function downloadTemplate(clinics: Clinic[]) {
  const c = clinics[0]
  const place = c ? [c.state, c.district, c.name] : ['Perlis', 'Kangar', 'KP KANGAR']
  const rows = [
    HEADER,
    ['Dr Ahmad bin Ali', '', 'ahmad@example.com', 'Officer', '012-3456789', ...place],
    ['Dr Siti binti Abu', 'D050', '', '', '', ...place],
  ]
  const blob = new Blob(['﻿' + rows.map(r => r.map(csvField).join(',')).join('\r\n') + '\r\n'], { type: 'text/csv' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = 'officers-template.csv'
  a.click()
  URL.revokeObjectURL(a.href)
}

/** Officers from a CSV file (or a paste from a spreadsheet): checked first, then added all at once. */
export function ImportModal({ clinics, onClose, onSaved, onIssued }: {
  clinics: Clinic[]; onClose: () => void; onSaved: () => void; onIssued: (logins: IssuedLogin[]) => void
}) {
  const [csv, setCsv] = useState('')
  const [checked, setChecked] = useState<{ csv: string; result: ImportResult } | null>(null)

  const check = useMutation({
    mutationFn: (text: string) => api.admin.importPeople(text, false),
    onSuccess: (result, text) => setChecked({ csv: text, result }),
    onError: e => notifyError(e, 'Could not check the file'),
  })
  const commit = useMutation({
    mutationFn: () => api.admin.importPeople(csv, true),
    onSuccess: result => {
      if (!result.committed) { setChecked({ csv, result }); return }
      onSaved()
      onClose()
      notifyOk(`${result.added} officer(s) added${result.invited ? `, ${result.invited} invited` : ''}.`)
      if (result.passwords.length) onIssued(result.passwords)
    },
    onError: e => notifyError(e, 'Import failed'),
  })

  const load = async (file: File | null) => {
    if (!file) return
    const text = await file.text()
    setCsv(text)
    check.mutate(text)
  }

  const result = checked?.csv === csv ? checked.result : null
  const problems = result ? result.rows.filter(r => r.errors.length).length : 0
  const ready = !!result && result.fileErrors.length === 0 && result.rows.length > 0 && problems === 0

  return (
    <Modal opened onClose={onClose} title="Import officers" size="xl">
      <Stack>
        <Text size="sm">
          One officer per row with the columns <b>{HEADER.join(', ')}</b>. Name, State, District and Clinic are required, and the
          state, district and clinic must already exist under <i>Clinics &amp; districts</i>. Officers with an Email are invited
          (Role: Officer or Admin, default Officer). A blank Code gets the next D-number. Nothing is added unless every row is valid.
        </Text>
        <Group>
          <Button variant="default" leftSection={<IconDownload size={16} />} onClick={() => downloadTemplate(clinics)}>
            Download template
          </Button>
          <FileButton onChange={load} accept=".csv,text/csv,text/plain">
            {props => <Button {...props} leftSection={<IconUpload size={16} />} loading={check.isPending}>Choose CSV file</Button>}
          </FileButton>
        </Group>
        <Textarea label="Or paste the rows (with the header), e.g. copied from Excel" autosize minRows={4} maxRows={10}
          value={csv} onChange={e => setCsv(e.currentTarget.value)} styles={{ input: { fontFamily: 'monospace', fontSize: 12 } }} />

        {result && result.fileErrors.length > 0 && (
          <Alert color="red" icon={<IconAlertTriangle size={18} />}>
            <List size="sm">{result.fileErrors.map(e => <List.Item key={e}>{e}</List.Item>)}</List>
          </Alert>
        )}
        {result && result.rows.length > 0 && (
          <>
            <Text size="sm" fw={600} c={problems ? 'red' : 'green'}>
              {result.rows.length} officer(s){problems ? `, ${problems} with problems – fix the file and check again` : ', all ready'}
            </Text>
            <Paper withBorder>
              <Table.ScrollContainer minWidth={760} mah={360}>
                <Table striped verticalSpacing={4} stickyHeader>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Line</Table.Th><Table.Th>Name</Table.Th><Table.Th>Sign-in</Table.Th><Table.Th>Clinic</Table.Th>
                      <Table.Th>Problems</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {result.rows.map(r => (
                      <Table.Tr key={r.line} bg={r.errors.length ? 'var(--mantine-color-red-light)' : undefined}>
                        <Table.Td>{r.line}</Table.Td>
                        <Table.Td fw={600}>{r.name || '–'}{r.code && <Text size="xs" c="dimmed">{r.code}</Text>}</Table.Td>
                        <Table.Td>
                          {r.email
                            ? <><Text size="sm">{r.email}</Text>{r.role === 'Admin' && <Badge size="xs" color="violet" variant="light">Admin</Badge>}</>
                            : <Text size="sm" c="dimmed">No invite</Text>}
                        </Table.Td>
                        <Table.Td>
                          {r.clinic ?? '–'}
                          {(r.district || r.state) && <Text size="xs" c="dimmed">{[r.district, r.state].filter(Boolean).join(', ')}</Text>}
                        </Table.Td>
                        <Table.Td>
                          {r.errors.length
                            ? <List size="xs" c="red">{r.errors.map(e => <List.Item key={e}>{e}</List.Item>)}</List>
                            : <Text size="xs" c="green">OK</Text>}
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            </Paper>
          </>
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancel</Button>
          {ready
            ? <Button onClick={() => commit.mutate()} loading={commit.isPending}>Add {result.rows.length} officer(s)</Button>
            : <Button onClick={() => check.mutate(csv)} loading={check.isPending} disabled={!csv.trim()}>Check</Button>}
        </Group>
      </Stack>
    </Modal>
  )
}
