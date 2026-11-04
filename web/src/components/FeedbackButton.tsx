import { Button, Group, Modal, SegmentedControl, Stack, Text, Textarea, Tooltip, UnstyledButton } from '@mantine/core'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { IconMessage2 } from '@tabler/icons-react'
import { useState } from 'react'
import { useLocation } from 'react-router'
import { api, type FeedbackCategory } from '../api'
import { notifyError, notifyOk, usePhone } from '../lib'

const MAX = 2000

const CATEGORIES: { value: FeedbackCategory; label: string; hint: string }[] = [
  { value: 'Bug', label: 'Problem', hint: 'What did you do, what happened, and what did you expect?' },
  { value: 'Suggestion', label: 'Idea', hint: 'What would make the app work better for you?' },
  { value: 'Other', label: 'Other', hint: 'Anything else you’d like the admins to know.' },
]

/** Header icon: opens the feedback dialog, which lives in the layout so the phone menu can open it too. */
export function FeedbackButton({ onClick }: { onClick: () => void }) {
  return (
    <Tooltip label="Send feedback" withinPortal>
      <UnstyledButton className="nav-link" aria-label="Send feedback" onClick={onClick}>
        <IconMessage2 size={20} stroke={1.8} />
      </UnstyledButton>
    </Tooltip>
  )
}

/** Anyone signed in can send feedback to the admins. The current page is sent along with it. */
export function FeedbackModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const { pathname } = useLocation()
  const phone = usePhone()
  const [category, setCategory] = useState<FeedbackCategory>('Bug')
  const [message, setMessage] = useState('')

  const close = onClose
  const send = useMutation({
    mutationFn: () => api.sendFeedback(category, message.trim(), pathname),
    onSuccess: () => {
      notifyOk('The admins will read it soon.', 'Thanks for your feedback')
      qc.invalidateQueries({ queryKey: ['admin', 'feedback'] })
      qc.invalidateQueries({ queryKey: ['notifications'] })
      setMessage('')
      setCategory('Bug')
      close()
    },
    onError: e => notifyError(e, 'Could not send feedback'),
  })

  const hint = CATEGORIES.find(c => c.value === category)?.hint
  const tooLong = message.length > MAX

  return (
    <>
      <Modal opened={opened} onClose={close} title="Send feedback" size="lg" fullScreen={phone}>
        <form onSubmit={e => { e.preventDefault(); if (message.trim() && !tooLong) send.mutate() }}>
          <Stack gap="md">
            <SegmentedControl fullWidth value={category} onChange={v => setCategory(v as FeedbackCategory)}
              data={CATEGORIES.map(c => ({ value: c.value, label: c.label }))} />
            <Textarea label="Your feedback" description={hint} autosize minRows={4} maxRows={12} data-autofocus
              value={message} onChange={e => setMessage(e.currentTarget.value)}
              error={tooLong ? `Keep it under ${MAX} characters.` : undefined} />
            <Group justify="space-between">
              <Text size="xs" c="dimmed">Sent with the page you’re on ({pathname}) so the admins can find the problem.</Text>
              <Text size="xs" c={tooLong ? 'red' : 'dimmed'}>{message.length}/{MAX}</Text>
            </Group>
            <Group justify="flex-end">
              <Button variant="default" onClick={close}>Cancel</Button>
              <Button type="submit" loading={send.isPending} disabled={!message.trim() || tooLong}>Send</Button>
            </Group>
          </Stack>
        </form>
      </Modal>
    </>
  )
}
