// @vitest-environment jsdom
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MantineProvider } from '@mantine/core'
import { MemoryRouter } from 'react-router'
import App from './App'

beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
  }))
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  Reflect.deleteProperty(navigator, 'clipboard')
})

test('посетитель переходит с главной на страницу бронирования', () => {
  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  fireEvent.click(screen.getByRole('link', { name: 'Перейти к бронированию' }))

  expect(screen.getByRole('heading', { name: 'Бронирование' })).toBeTruthy()
  expect(screen.getByText('Выберите тип события и свободное время')).toBeTruthy()
})

test('страница бронирования показывает доступность API через сгенерированный клиент', async () => {
  vi.stubGlobal('Request', class {
    url: string
    constructor(url: string) {
      this.url = url
    }
  })
  const fetchMock = vi.fn(async (request: { url: string }) => ({
    ok: true,
    status: 200,
    headers: new Headers({ 'Content-Type': 'application/json' }),
    text: async () => request.url.endsWith('/api/health') ? '{"status":"ok"}' : '[]',
  }))
  vi.stubGlobal('fetch', fetchMock)

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/booking']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  expect(await screen.findByText('Сервер доступен')).toBeTruthy()
  expect(fetchMock.mock.calls[0]?.[0].url).toBe(`${window.location.origin}/api/health`)
})

test('владелец входит в закрытую часть и выходит', async () => {
  vi.stubGlobal('Request', class {
    url: string
    method: string
    constructor(url: string, init: RequestInit) {
      this.url = url
      this.method = init.method ?? 'GET'
    }
  })
  let authenticated = false
  vi.stubGlobal('fetch', vi.fn(async (request: { method: string; url: string }) => {
    expect(request.url).toMatch(/\/api\/owner\/(session|bookings)$/)
    if (request.url.endsWith('/api/owner/bookings')) return {
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      text: async () => '[]',
    }
    if (request.method === 'POST') authenticated = true
    if (request.method === 'DELETE') authenticated = false
    const status = request.method === 'DELETE' ? 204 : authenticated ? 200 : 401
    return {
      ok: status < 400,
      status,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      text: async () => status === 200 ? '{"username":"test-owner"}' : '',
    }
  }))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  fireEvent.click(screen.getByRole('link', { name: 'Вход для владельца' }))
  expect(await screen.findByRole('heading', { name: 'Вход владельца' })).toBeTruthy()
  fireEvent.change(screen.getByRole('textbox', { name: 'Логин' }), { target: { value: 'test-owner' } })
  fireEvent.change(screen.getByLabelText(/Пароль/), { target: { value: 'test-password' } })
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }))
  expect(await screen.findByText('Вы вошли как test-owner')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Выйти' }))
  expect(await screen.findByRole('heading', { name: 'Вход владельца' })).toBeTruthy()
})

test('созданный владельцем тип виден гостю в каталоге', async () => {
  vi.stubGlobal('Request', class {
    url: string
    method: string
    body: BodyInit | null | undefined
    constructor(url: string, init: RequestInit) {
      this.url = url
      this.method = init.method ?? 'GET'
      this.body = init.body
    }
  })
  let created = false
  vi.stubGlobal('fetch', vi.fn(async (request: { method: string; url: string; body?: BodyInit | null }) => {
    if (request.url.endsWith('/api/event-types') && request.method === 'POST') {
      expect(JSON.parse(String(request.body))).toEqual({ title: 'Консультация', description: 'Знакомство', durationMinutes: 45 })
      created = true
    }
    const data = request.url.endsWith('/api/owner/bookings') ? '[]'
      : request.url.endsWith('/api/owner/session')
      ? '{"username":"test-owner"}'
      : request.url.endsWith('/api/health') ? '{"status":"ok"}'
      : request.url.endsWith('/api/event-types/consultation/slots')
        ? '{"ownerTimeZone":"Europe/Moscow","windowStartDate":"2026-10-24","slots":[{"start":"2026-10-26T09:00:00+03:00","end":"2026-10-26T09:45:00+03:00"}]}'
      : created ? '[{"id":"consultation","title":"Консультация","description":"Знакомство","durationMinutes":45}]' : '[]'
    return {
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      text: async () => request.method === 'POST' && request.url.endsWith('/api/event-types')
        ? '{"id":"consultation","title":"Консультация","description":"Знакомство","durationMinutes":45}' : data,
    }
  }))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/owner']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  expect(await screen.findByRole('heading', { name: 'Новый тип события' })).toBeTruthy()
  expect(screen.queryByRole('textbox', { name: 'Идентификатор' })).toBeNull()
  fireEvent.change(screen.getByRole('textbox', { name: 'Название' }), { target: { value: 'Консультация' } })
  fireEvent.change(screen.getByRole('textbox', { name: 'Описание' }), { target: { value: 'Знакомство' } })
  fireEvent.change(screen.getByRole('spinbutton', { name: 'Длительность в минутах' }), { target: { value: '45' } })
  fireEvent.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByText('Тип события «Консультация» создан')).toBeTruthy()

  fireEvent.click(screen.getByRole('link', { name: 'На главную' }))
  fireEvent.click(screen.getByRole('link', { name: 'Перейти к бронированию' }))
  expect(await screen.findByText('Консультация')).toBeTruthy()
  expect(screen.getByText('Знакомство')).toBeTruthy()
  expect(screen.getByText('45 мин.')).toBeTruthy()
  fireEvent.click(screen.getByRole('link', { name: 'Открыть календарь: Консультация' }))
  expect(await screen.findByText('Выбранный тип: Консультация')).toBeTruthy()
  expect(screen.queryByRole('heading', { name: 'Типы событий' })).toBeNull()
  expect(screen.queryByRole('link', { name: 'Открыть календарь: Консультация' })).toBeNull()
  expect(await screen.findByText('Часовой пояс владельца: Europe/Moscow')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: /26 окт/i }))
  expect(await screen.findByRole('button', { name: /26 окт.*09:00/ })).toBeTruthy()
})

test('пустой календарь объясняет отсутствие слотов', async () => {
  vi.stubGlobal('Request', class {
    url: string
    constructor(url: string) {
      this.url = url
    }
  })
  vi.stubGlobal('fetch', vi.fn(async (request: { url: string }) => ({
    ok: true,
    status: 200,
    headers: new Headers({ 'Content-Type': 'application/json' }),
    text: async () => request.url.endsWith('/api/health') ? '{"status":"ok"}'
      : request.url.endsWith('/slots') ? '{"ownerTimeZone":"Europe/Moscow","windowStartDate":"2026-10-24","slots":[]}'
      : '[{"id":"long","title":"Длинный звонок","description":"Тест","durationMinutes":541}]',
  })))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/booking']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  fireEvent.click(await screen.findByRole('link', { name: 'Открыть календарь: Длинный звонок' }))
  expect(await screen.findByText('В ближайшие 14 дней свободных слотов нет')).toBeTruthy()
})

test('гость выбирает день и слот без прокрутки слотов других дней', async () => {
  vi.stubGlobal('Request', class {
    url: string
    constructor(url: string) { this.url = url }
  })
  vi.stubGlobal('fetch', vi.fn(async (request: { url: string }) => ({
    ok: true,
    status: 200,
    headers: new Headers({ 'Content-Type': 'application/json' }),
    text: async () => request.url.endsWith('/slots')
      ? JSON.stringify({ ownerTimeZone: 'Europe/Moscow', windowStartDate: '2026-10-24', slots: [
        { start: '2026-10-24T09:00:00+03:00', end: '2026-10-24T09:30:00+03:00' },
        { start: '2026-10-25T09:00:00+03:00', end: '2026-10-25T09:30:00+03:00' },
        { start: '2026-10-26T00:30:00+03:00', end: '2026-10-26T01:00:00+03:00' },
        { start: '2026-10-26T09:00:00+03:00', end: '2026-10-26T09:30:00+03:00' },
        { start: '2026-10-27T09:00:00+03:00', end: '2026-10-27T09:30:00+03:00' },
      ] })
      : request.url.endsWith('/api/health') ? '{"status":"ok"}'
      : '[{"id":"call","title":"Звонок","description":"Тест","durationMinutes":30}]',
  })))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/booking/call']}><App /></MemoryRouter>
    </MantineProvider>,
  )

  const days = await screen.findByRole('group', { name: 'День бронирования' })
  expect(within(days).getAllByRole('button')).toHaveLength(14)
  expect(within(days).getByRole('button', { name: /24 окт/i }).hasAttribute('disabled')).toBe(false)
  expect(within(days).getByRole('button', { name: /25 окт/i }).hasAttribute('disabled')).toBe(false)
  expect(within(days).getByRole('button', { name: /6 ноя/i })).toBeTruthy()
  const firstDay = within(days).getByRole('button', { name: /26 окт/i })
  const secondDay = within(days).getByRole('button', { name: /27 окт/i })
  expect(firstDay.getAttribute('aria-pressed')).toBe('false')
  expect(screen.getByText('Шаг 1. Выберите день')).toBeTruthy()
  expect(screen.queryByRole('group', { name: 'Свободное время' })).toBeNull()
  fireEvent.click(within(days).getByRole('button', { name: /24 окт/i }))
  expect(screen.getByRole('button', { name: /24 окт.*09:00/ })).toBeTruthy()
  fireEvent.click(firstDay)
  expect(screen.getByText('Шаг 2. Выберите время')).toBeTruthy()
  expect(screen.getByRole('button', { name: /26 окт.*00:30/i })).toBeTruthy()
  expect(screen.queryByRole('button', { name: /27 окт.*09:00/i })).toBeNull()

  fireEvent.click(secondDay)
  expect(secondDay.getAttribute('aria-pressed')).toBe('true')
  expect(screen.queryByRole('button', { name: /26 окт.*00:30/i })).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: /27 окт.*09:00/i }))
  expect(screen.getByText(/Выбрано:.*27 окт.*09:00/i)).toBeTruthy()

  fireEvent.click(firstDay)
  expect(screen.queryByText(/Выбрано:/)).toBeNull()
})

test('гость после конфликта выбирает другой слот и видит подтверждение', async () => {
  vi.stubGlobal('Request', class {
    url: string
    method: string
    constructor(url: string, init: RequestInit) {
      this.url = url
      this.method = init.method ?? 'GET'
    }
  })
  let attempts = 0
  vi.stubGlobal('fetch', vi.fn(async (request: { method: string; url: string }) => {
    if (request.url.endsWith('/api/bookings') && request.method === 'POST') attempts++
    const firstSlot = '{"start":"2026-10-26T09:00:00+03:00","end":"2026-10-26T09:30:00+03:00"}'
    const secondSlot = '{"start":"2026-10-26T09:30:00+03:00","end":"2026-10-26T10:00:00+03:00"}'
    const status = request.url.endsWith('/api/bookings') && attempts === 1 ? 409 : 200
    const data = request.url.endsWith('/api/health') ? '{"status":"ok"}'
      : request.url.endsWith('/api/event-types')
        ? '[{"id":"consultation","title":"Консультация","description":"Тест","durationMinutes":30}]'
        : request.url.endsWith('/slots')
          ? `{"ownerTimeZone":"Europe/Moscow","windowStartDate":"2026-10-24","slots":[${attempts ? '' : `${firstSlot},`}${secondSlot}]}`
          : status === 409 ? '{"error":"Время занято"}'
            : '{"id":"booking-1","eventType":{"id":"consultation","title":"Консультация","description":"Тест","durationMinutes":30},"start":"2026-10-26T09:30:00+03:00","end":"2026-10-26T10:00:00+03:00"}'
    return { ok: status === 200, status, headers: new Headers({ 'Content-Type': 'application/json' }), text: async () => data }
  }))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/booking/consultation']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  fireEvent.click(await screen.findByRole('button', { name: /26 окт/i }))
  fireEvent.click(screen.getByRole('button', { name: /26 окт.*09:00/ }))
  fireEvent.change(screen.getByRole('textbox', { name: 'Ваше имя' }), { target: { value: 'Гость' } })
  fireEvent.change(screen.getByRole('textbox', { name: 'Email' }), { target: { value: 'guest@example.com' } })
  fireEvent.click(screen.getByRole('button', { name: 'Забронировать' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Выбранное время занято. Выберите другой слот.')
  expect(screen.queryByRole('button', { name: /26 окт.*09:00/ })).toBeNull()

  fireEvent.click(await screen.findByRole('button', { name: /26 окт.*09:30/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Забронировать' }))
  expect(await screen.findByRole('heading', { name: 'Бронирование подтверждено' })).toBeTruthy()
  expect(screen.queryByText('Выберите тип события и свободное время')).toBeNull()
  expect(screen.queryByText(/Номер: booking-1/)).toBeNull()
  expect(screen.getByText('Тип события: Консультация')).toBeTruthy()
  expect(screen.getByText(/Окончание:.*10:00/)).toBeTruthy()
  expect(screen.getByText('Часовой пояс владельца: Europe/Moscow')).toBeTruthy()
})

function stubConfirmedBooking() {
  vi.stubGlobal('Request', class {
    url: string
    method: string
    constructor(url: string, init: RequestInit) {
      this.url = url
      this.method = init.method ?? 'GET'
    }
  })
  vi.stubGlobal('fetch', vi.fn(async (request: { method: string; url: string }) => {
    const respond = (data: string) => ({ ok: true, status: 200, headers: new Headers({ 'Content-Type': 'application/json' }), text: async () => data })
    if (request.url.endsWith('/api/health')) {
      return respond('{"status":"ok"}')
    }
    if (request.url.endsWith('/api/event-types')) {
      return respond('[{"id":"consultation","title":"Консультация","description":"Тест","durationMinutes":30}]')
    }
    if (request.url.endsWith('/slots')) {
      return respond('{"ownerTimeZone":"Europe/Moscow","windowStartDate":"2026-10-24","slots":[{"start":"2026-10-26T09:30:00+03:00","end":"2026-10-26T10:00:00+03:00"}]}')
    }
    if (request.method === 'POST' && request.url.endsWith('/api/bookings')) {
      return respond('{"id":"booking-1","eventType":{"id":"consultation","title":"Консультация","description":"Тест","durationMinutes":30},"start":"2026-10-26T09:30:00+03:00","end":"2026-10-26T10:00:00+03:00"}')
    }
    throw new Error(`Неожиданный запрос: ${request.method} ${request.url}`)
  }))
}

async function confirmBooking() {
  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/booking/consultation']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )
  fireEvent.click(await screen.findByRole('button', { name: /26 окт/i }))
  fireEvent.click(screen.getByRole('button', { name: /26 окт.*09:30/ }))
  fireEvent.change(screen.getByRole('textbox', { name: 'Ваше имя' }), { target: { value: 'Гость' } })
  fireEvent.change(screen.getByRole('textbox', { name: 'Email' }), { target: { value: 'guest@example.com' } })
  fireEvent.click(screen.getByRole('button', { name: 'Забронировать' }))
  await screen.findByRole('heading', { name: 'Бронирование подтверждено' })
}

test('гость копирует информацию о подтверждённой встрече', async () => {
  stubConfirmedBooking()
  const writeText = vi.fn().mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })

  await confirmBooking()
  fireEvent.click(screen.getByRole('button', { name: 'Скопировать информацию' }))

  expect(await screen.findByText('Информация скопирована')).toBeTruthy()
  expect(writeText).toHaveBeenCalledWith('Консультация — 26.10.2026, 09:30–10:00 (Europe/Moscow)')
  expect(screen.getByText('Тип события: Консультация')).toBeTruthy()
})

test('если скопировать не удалось, гость видит ошибку и данные встречи', async () => {
  stubConfirmedBooking()
  const writeText = vi.fn().mockRejectedValue(new Error('denied'))
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })

  await confirmBooking()
  fireEvent.click(screen.getByRole('button', { name: 'Скопировать информацию' }))

  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Не удалось скопировать информацию. Скопируйте её вручную.')
  expect(screen.getByText('Тип события: Консультация')).toBeTruthy()
  expect(screen.getByText('Начало: 26 окт. 2026 г., 09:30')).toBeTruthy()
  const field = screen.getByRole('textbox', { name: 'Текст для копирования' }) as HTMLInputElement
  expect(field.value).toBe('Консультация — 26.10.2026, 09:30–10:00 (Europe/Moscow)')
  expect(field.readOnly).toBe(true)
})

test('если Clipboard API недоступен, гость копирует текст из поля вручную', async () => {
  stubConfirmedBooking()
  Reflect.deleteProperty(navigator, 'clipboard')

  await confirmBooking()
  fireEvent.click(screen.getByRole('button', { name: 'Скопировать информацию' }))

  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Не удалось скопировать информацию. Скопируйте её вручную.')
  const field = screen.getByRole('textbox', { name: 'Текст для копирования' }) as HTMLInputElement
  expect(field.value).toBe('Консультация — 26.10.2026, 09:30–10:00 (Europe/Moscow)')
  expect(field.readOnly).toBe(true)
  expect(screen.getByText('Тип события: Консультация')).toBeTruthy()
})

test('после подтверждения переход к другому типу показывает его календарь', async () => {
  vi.stubGlobal('Request', class {
    url: string
    method: string
    constructor(url: string, init: RequestInit) {
      this.url = url
      this.method = init.method ?? 'GET'
    }
  })
  vi.stubGlobal('fetch', vi.fn(async (request: { method: string; url: string }) => {
    const eventTypes = [
      { id: 'first', title: 'Первый тип', description: 'Первый звонок', durationMinutes: 30 },
      { id: 'second', title: 'Второй тип', description: 'Второй звонок', durationMinutes: 30 },
    ]
    const data = request.url.endsWith('/api/health') ? '{"status":"ok"}'
      : request.url.endsWith('/api/event-types') ? JSON.stringify(eventTypes)
        : request.url.endsWith('/api/event-types/first/slots')
          ? '{"ownerTimeZone":"Europe/Moscow","windowStartDate":"2026-10-24","slots":[{"start":"2026-10-26T09:00:00+03:00","end":"2026-10-26T09:30:00+03:00"}]}'
          : request.url.endsWith('/api/event-types/second/slots')
            ? '{"ownerTimeZone":"Europe/Moscow","windowStartDate":"2026-10-24","slots":[{"start":"2026-10-26T10:00:00+03:00","end":"2026-10-26T10:30:00+03:00"}]}'
            : JSON.stringify({ id: 'booking-1', eventType: eventTypes[0], start: '2026-10-26T09:00:00+03:00', end: '2026-10-26T09:30:00+03:00' })
    return { ok: true, status: 200, headers: new Headers({ 'Content-Type': 'application/json' }), text: async () => data }
  }))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/booking/first']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  fireEvent.click(await screen.findByRole('button', { name: /26 окт/i }))
  fireEvent.click(screen.getByRole('button', { name: /26 окт.*09:00/ }))
  fireEvent.change(screen.getByRole('textbox', { name: 'Ваше имя' }), { target: { value: 'Гость' } })
  fireEvent.change(screen.getByRole('textbox', { name: 'Email' }), { target: { value: 'guest@example.com' } })
  fireEvent.click(screen.getByRole('button', { name: 'Забронировать' }))
  expect(await screen.findByText('Тип события: Первый тип')).toBeTruthy()

  expect(screen.queryByRole('heading', { name: 'Типы событий' })).toBeNull()
  fireEvent.click(screen.getByRole('link', { name: 'На главную' }))
  fireEvent.click(screen.getByRole('link', { name: 'Перейти к бронированию' }))
  expect(await screen.findByRole('heading', { name: 'Типы событий' })).toBeTruthy()
  fireEvent.click(screen.getByRole('link', { name: 'Открыть календарь: Второй тип' }))
  fireEvent.click(await screen.findByRole('button', { name: /26 окт/i }))
  expect(await screen.findByRole('button', { name: /26 окт.*10:00/ })).toBeTruthy()
  expect(screen.queryByRole('heading', { name: 'Бронирование подтверждено' })).toBeNull()
  expect(screen.queryByRole('button', { name: /26 окт.*09:00/ })).toBeNull()
})

test('владелец после входа видит общий список с контактами гостей', async () => {
  vi.stubGlobal('Request', class {
    url: string
    constructor(url: string) {
      this.url = url
    }
  })
  vi.stubGlobal('fetch', vi.fn(async (request: { url: string }) => ({
    ok: true,
    status: 200,
    headers: new Headers({ 'Content-Type': 'application/json' }),
    text: async () => request.url.endsWith('/api/owner/session') ? '{"username":"test-owner"}'
      : '[{"id":"booking-1","eventType":{"id":"call","title":"Консультация","description":"Тест","durationMinutes":30},"start":"2026-10-21T09:00:00+03:00","end":"2026-10-21T09:30:00+03:00","guestName":"Анна","guestEmail":"anna@example.com"}]',
  })))

  render(
    <MantineProvider env="test">
      <MemoryRouter initialEntries={['/owner']}>
        <App />
      </MemoryRouter>
    </MantineProvider>,
  )

  expect(await screen.findByRole('heading', { name: 'Предстоящие встречи' })).toBeTruthy()
  expect(await screen.findByText('Гость: Анна')).toBeTruthy()
  expect(screen.getByText('Email гостя: anna@example.com')).toBeTruthy()
  expect(screen.getByText('Начало: 2026-10-21 09:00 UTC+03:00')).toBeTruthy()
})
