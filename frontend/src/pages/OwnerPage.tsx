import { useEffect, useState, type FormEvent } from 'react'
import { Button, Container, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { Link } from 'react-router'
import { createEventType, getOwnerSession, listOwnerBookings, login, logout, type OwnerMeeting } from '../generated/api'

function OwnerPage() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [owner, setOwner] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [durationMinutes, setDurationMinutes] = useState('30')
  const [created, setCreated] = useState('')
  const [meetings, setMeetings] = useState<OwnerMeeting[]>([])
  const [meetingsStatus, setMeetingsStatus] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    let active = true
    void getOwnerSession({ baseUrl: window.location.origin }).then(({ data }) => {
      if (active) {
        setOwner(data?.username ?? null)
        setLoading(false)
      }
    }).catch(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [])

  useEffect(() => {
    if (!owner) return
    let active = true
    void listOwnerBookings({ baseUrl: window.location.origin }).then(({ data }) => {
      if (active) {
        setMeetings(data ?? [])
        setMeetingsStatus(data ? 'ready' : 'error')
      }
    }).catch(() => {
      if (active) setMeetingsStatus('error')
    })
    return () => { active = false }
  }, [owner])

  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    try {
      const { data } = await login({ baseUrl: window.location.origin, body: { username, password } })
      if (!data) {
        setError('Неверный логин или пароль')
        return
      }
      setOwner(data.username)
      setPassword('')
    } catch {
      setError('Не удалось войти. Попробуйте ещё раз.')
    }
  }

  async function signOut() {
    try {
      const { error: signOutError } = await logout({ baseUrl: window.location.origin })
      if (signOutError) {
        setError('Не удалось выйти. Попробуйте ещё раз.')
        return
      }
      setOwner(null)
      setMeetings([])
    } catch {
      setError('Не удалось выйти. Попробуйте ещё раз.')
    }
  }

  async function createEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setCreated('')
    try {
      const { data, error: createError } = await createEventType({
        baseUrl: window.location.origin,
        body: { title, description, durationMinutes: Number(durationMinutes) },
      })
      if (createError || !data) {
        setError('Не удалось создать тип события: проверьте данные.')
        return
      }
      setCreated(data.title)
      setTitle('')
      setDescription('')
    } catch {
      setError('Не удалось создать тип события.')
    }
  }

  return (
    <Container component="main" size="sm" py="xl">
      <Stack gap="lg">
        <Title order={1}>Управление календарём</Title>
        {loading ? <Text>Проверяем вход…</Text> : owner ? (
          <>
            <Text>Вы вошли как {owner}</Text>
            <form onSubmit={(event) => void createEvent(event)}>
              <Stack gap="md">
                <Title order={2}>Новый тип события</Title>
                <TextInput label="Название" required value={title} onChange={(event) => setTitle(event.currentTarget.value)} />
                <TextInput label="Описание" required value={description} onChange={(event) => setDescription(event.currentTarget.value)} />
                <TextInput label="Длительность в минутах" type="number" min={1} step={1} required value={durationMinutes} onChange={(event) => setDurationMinutes(event.currentTarget.value)} />
                <Button type="submit">Создать тип события</Button>
              </Stack>
            </form>
            {created && <Text role="status">Тип события «{created}» создан</Text>}
            <Title order={2}>Предстоящие встречи</Title>
            {meetingsStatus === 'loading' && <Text>Загружаем встречи…</Text>}
            {meetingsStatus === 'error' && <Text role="alert">Не удалось загрузить встречи</Text>}
            {meetingsStatus === 'ready' && meetings.length === 0 && <Text>Предстоящих встреч нет</Text>}
            {meetings.map((meeting) => (
              <Stack key={meeting.id} gap="xs">
                <Text fw={700}>{meeting.eventType.title}</Text>
                <Text>Начало: {meeting.start.replace('T', ' ').slice(0, 16)} UTC{meeting.start.slice(-6)}</Text>
                <Text>Окончание: {meeting.end.replace('T', ' ').slice(0, 16)} UTC{meeting.end.slice(-6)}</Text>
                <Text>Гость: {meeting.guestName}</Text>
                <Text>Email гостя: {meeting.guestEmail}</Text>
              </Stack>
            ))}
            <Button onClick={() => void signOut()}>Выйти</Button>
          </>
        ) : (
          <form onSubmit={(event) => void signIn(event)}>
            <Stack gap="md">
              <Title order={2}>Вход владельца</Title>
              <TextInput label="Логин" required value={username} onChange={(event) => setUsername(event.currentTarget.value)} />
              <PasswordInput label="Пароль" required value={password} onChange={(event) => setPassword(event.currentTarget.value)} />
              <Button type="submit">Войти</Button>
            </Stack>
          </form>
        )}
        {error && <Text role="alert" c="red">{error}</Text>}
        <Button component={Link} to="/" variant="subtle">На главную</Button>
      </Stack>
    </Container>
  )
}

export default OwnerPage
