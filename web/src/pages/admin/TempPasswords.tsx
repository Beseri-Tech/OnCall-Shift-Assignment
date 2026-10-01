import { Alert, Button, Code, CopyButton, Group, Modal, Stack, Table, Text } from '@mantine/core'
import { IconCheck, IconCopy } from '@tabler/icons-react'

export interface IssuedLogin { name?: string; email: string; tempPassword: string }

/** Shown when invite emails couldn't be sent: the admin passes these sign-in details on themselves. */
export function TempPasswordsModal({ logins, onClose }: { logins: IssuedLogin[]; onClose: () => void }) {
  const single = logins.length === 1 ? logins[0] : null
  const all = logins.map(l => `${l.name ? `${l.name}\t` : ''}${l.email}\t${l.tempPassword}`).join('\n')

  return (
    <Modal opened={logins.length > 0} onClose={onClose} title="Email not sent – share the password" size={single ? 'md' : 'lg'}>
      <Stack>
        <Alert color="orange">
          Email isn't set up on the server (or sending failed). Send the sign-in details
          {single ? ` to ${single.email}` : ' to each officer'} yourself, e.g. by WhatsApp. Passwords are shown only once and must
          be changed at first sign-in.
        </Alert>
        {single ? (
          <Group>
            <Code fz="lg" p="sm">{single.tempPassword}</Code>
            <Copy value={single.tempPassword} />
          </Group>
        ) : (
          <>
            <Table striped verticalSpacing={4}>
              <Table.Thead><Table.Tr><Table.Th>Officer</Table.Th><Table.Th>Email</Table.Th><Table.Th>Temporary password</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {logins.map(l => (
                  <Table.Tr key={l.email}>
                    <Table.Td>{l.name ?? '–'}</Table.Td>
                    <Table.Td><Text size="sm">{l.email}</Text></Table.Td>
                    <Table.Td><Code>{l.tempPassword}</Code></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
            <Group><Copy value={all} label="Copy all" /></Group>
          </>
        )}
        <Group justify="flex-end"><Button onClick={onClose}>Done</Button></Group>
      </Stack>
    </Modal>
  )
}

function Copy({ value, label = 'Copy' }: { value: string; label?: string }) {
  return (
    <CopyButton value={value}>
      {({ copied, copy }) => (
        <Button variant="default" leftSection={copied ? <IconCheck size={16} /> : <IconCopy size={16} />} onClick={copy}>
          {copied ? 'Copied' : label}
        </Button>
      )}
    </CopyButton>
  )
}
