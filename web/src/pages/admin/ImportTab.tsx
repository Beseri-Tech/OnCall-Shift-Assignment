import { Alert, Badge, Button, FileInput, Group, Paper, Select, Stack, Table, Text, Title } from '@mantine/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IconCheck, IconFileSpreadsheet, IconInfoCircle } from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import {
  api, type ImportAction, type LeaveImportPreview, type NameMatchType, type OpeningImportPreview,
} from '../../api'
import { PeriodSelect } from '../../components/common'
import { defaultPeriod, notifyError, notifyOk, pick } from '../../lib'
import { formatMonth, formatRange, toRanges } from '../../dates'

const NEW = '__new'
const SKIP = '__skip'

const matchColor: Record<NameMatchType, string> = { Exact: 'green', Fuzzy: 'yellow', None: 'gray' }

export function ImportTab() {
  return (
    <Stack gap="lg">
      <Alert icon={<IconInfoCircle />} color="blue">
        These imports are for moving off the old spreadsheets. After that, people enter leave themselves and shift totals
        update automatically when you publish a rota.
      </Alert>
      <LeaveImport />
      <OpeningImport />
    </Stack>
  )
}

function usePeopleOptions() {
  const people = useQuery({ queryKey: ['admin', 'people'], queryFn: api.admin.people })
  return useMemo(() => (people.data ?? []).filter(p => p.status === 'OnCall').map(p => ({ value: p.id, label: `${p.name} (${p.code})` })), [people.data])
}

function LeaveImport() {
  const qc = useQueryClient()
  const periods = useQuery({ queryKey: ['admin', 'periods'], queryFn: api.admin.periods })
  const [chosenId, setPeriodId] = useState<string | null>(null)
  const [preview, setPreview] = useState<LeaveImportPreview | null>(null)
  const [choice, setChoice] = useState<Record<number, string>>({})
  const peopleOptions = usePeopleOptions()

  const importable = (periods.data ?? []).filter(p => p.status !== 'Published')
  const periodId = pick(importable, chosenId, defaultPeriod(importable, p => p.status === 'Open'))?.id ?? null

  const upload = useMutation({
    mutationFn: (file: File) => api.admin.importLeave(periodId!, file),
    onSuccess: p => {
      setPreview(p)
      setChoice(Object.fromEntries(p.rows.map((r, i) => [i, r.personId ?? NEW])))
    },
    onError: e => notifyError(e, 'Could not read the file'),
  })

  const commit = useMutation({
    mutationFn: () => api.admin.commitLeave(periodId!, preview!.rows.map((r, i) => {
      const c = choice[i]
      const action: ImportAction = c === SKIP ? 'Skip' : c === NEW ? 'New' : 'Match'
      return { rawName: r.rawName, action, personId: action === 'Match' ? c : null, leave: r.leave }
    })),
    onSuccess: r => {
      notifyOk(`${r.updated} updated, ${r.added} added, ${r.skipped} skipped.${r.warnings.length ? ` ${r.warnings.join(' ')}` : ''}`, 'Leave imported')
      setPreview(null)
      qc.invalidateQueries()
    },
    onError: e => notifyError(e, 'Import failed'),
  })

  const counts = preview && {
    fuzzy: preview.rows.filter((r, i) => r.matchType === 'Fuzzy' && choice[i] !== SKIP).length,
    added: Object.values(choice).filter(c => c === NEW).length,
    warned: preview.rows.filter(r => r.warnings.length).length,
  }

  return (
    <Paper withBorder p="md">
      <Title order={4} mb={4}>Import leave from the old sheet</Title>
      <Text size="sm" c="dimmed" mb="md">
        Upload the shared leave sheet (NAMA + one column per month). Leave inside the chosen period is replaced for each matched person.
      </Text>
      <Group align="flex-end" mb="md" wrap="wrap">
        <PeriodSelect periods={importable} value={periodId} onChange={id => { setPeriodId(id); setPreview(null) }} label="Into period" />
        <FileInput
          label="Leave sheet (.xlsx)"
          placeholder="Choose file"
          accept=".xlsx"
          leftSection={<IconFileSpreadsheet size={16} />}
          disabled={!periodId}
          onChange={f => f && upload.mutate(f)}
          w={280}
          clearable
        />
        {upload.isPending && <Text size="sm">Reading…</Text>}
      </Group>

      {preview && counts && (
        <Stack gap="sm">
          <Group gap="xs">
            <Text size="sm">Months: {preview.months.map(formatMonth).join(', ')} ·</Text>
            <Badge color="yellow" variant="light">{counts.fuzzy} near-matches to check</Badge>
            <Badge color="teal" variant="light">{counts.added} new people</Badge>
            <Badge color="orange" variant="light">{counts.warned} rows with warnings</Badge>
          </Group>
          <Table.ScrollContainer minWidth={900} mah={520}>
            <Table stickyHeader striped verticalSpacing={4}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Name in sheet</Table.Th>
                  <Table.Th>Match</Table.Th>
                  <Table.Th w={300}>Rota person</Table.Th>
                  <Table.Th>Leave</Table.Th>
                  <Table.Th>Warnings</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {preview.rows.map((r, i) => (
                  <Table.Tr key={i} bg={r.matchType === 'Fuzzy' ? 'yellow.0' : choice[i] === NEW ? 'teal.0' : undefined}>
                    <Table.Td fw={600}>{r.rawName}</Table.Td>
                    <Table.Td><Badge color={matchColor[r.matchType]} variant="light">{r.matchType === 'None' ? 'No match' : r.matchType}</Badge></Table.Td>
                    <Table.Td>
                      <Select
                        size="xs"
                        searchable
                        allowDeselect={false}
                        data={[
                          { value: NEW, label: '➕ Add as new person' },
                          { value: SKIP, label: '⏭ Skip' },
                          ...peopleOptions,
                        ]}
                        value={choice[i]}
                        onChange={v => v && setChoice(c => ({ ...c, [i]: v }))}
                      />
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs">{r.leave.length} day(s)</Text>
                      <Text size="xs" c="dimmed" lineClamp={2}>{toRanges(r.leave.map(l => l.date)).map(formatRange).join(', ')}</Text>
                    </Table.Td>
                    <Table.Td><Text size="xs" c="orange.8">{r.warnings.join('; ')}</Text></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPreview(null)}>Cancel</Button>
            <Button leftSection={<IconCheck size={16} />} loading={commit.isPending} onClick={() => commit.mutate()}>
              Apply leave
            </Button>
          </Group>
        </Stack>
      )}
    </Paper>
  )
}

function OpeningImport() {
  const qc = useQueryClient()
  const [preview, setPreview] = useState<OpeningImportPreview | null>(null)

  const upload = useMutation({
    mutationFn: (file: File) => api.admin.importOpening(file),
    onSuccess: setPreview,
    onError: e => notifyError(e, 'Could not read the file'),
  })
  const commit = useMutation({
    mutationFn: () => api.admin.commitOpening(preview!.rows.filter(r => r.personId)
      .map(r => ({ personId: r.personId!, openingWeekendHoliday: r.value }))),
    onSuccess: r => {
      notifyOk(`${r.updated} people updated.`, 'Opening totals imported')
      setPreview(null)
      qc.invalidateQueries({ queryKey: ['admin', 'people'] })
    },
    onError: e => notifyError(e, 'Import failed'),
  })

  return (
    <Paper withBorder p="md">
      <Title order={4} mb={4}>One-time: opening weekend/PH totals</Title>
      <Text size="sm" c="dimmed" mb="md">
        Upload “Oncaller Total Weekend And Public Shift.xlsx” to set everyone's weekend + public holiday count before this app.
        You can also type opening numbers per person on the People tab.
      </Text>
      <FileInput
        label="History sheet (.xlsx)"
        placeholder="Choose file"
        accept=".xlsx"
        leftSection={<IconFileSpreadsheet size={16} />}
        onChange={f => f && upload.mutate(f)}
        w={320}
        clearable
      />
      {preview && (
        <Stack gap="sm" mt="md">
          {preview.peopleWithoutRow.length > 0 && (
            <Alert color="yellow" title={`${preview.peopleWithoutRow.length} on-call officers are not in the sheet`}>
              <Text size="sm">{preview.peopleWithoutRow.join(', ')}. They keep their current opening numbers.</Text>
            </Alert>
          )}
          <Table.ScrollContainer minWidth={600} mah={420}>
            <Table stickyHeader striped verticalSpacing={4}>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Name in sheet</Table.Th>
                  <Table.Th>Weekend + PH</Table.Th>
                  <Table.Th>Match</Table.Th>
                  <Table.Th>Rota person</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {preview.rows.map((r, i) => (
                  <Table.Tr key={i} bg={r.matchType === 'Fuzzy' ? 'yellow.0' : undefined} opacity={r.personId ? 1 : 0.5}>
                    <Table.Td>{r.rawName}</Table.Td>
                    <Table.Td>{r.value ?? 'blank'}</Table.Td>
                    <Table.Td><Badge color={matchColor[r.matchType]} variant="light">{r.matchType === 'None' ? 'Not in rota' : r.matchType}</Badge></Table.Td>
                    <Table.Td>{r.personName ?? '–'}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setPreview(null)}>Cancel</Button>
            <Button leftSection={<IconCheck size={16} />} loading={commit.isPending} onClick={() => commit.mutate()}>
              Apply {preview.rows.filter(r => r.personId).length} matched rows
            </Button>
          </Group>
        </Stack>
      )}
    </Paper>
  )
}
