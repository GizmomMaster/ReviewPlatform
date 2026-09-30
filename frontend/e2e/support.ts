import { expect, request, type APIRequestContext, type Page } from '@playwright/test'

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
const mailpitURL = process.env.E2E_MAILPIT_URL
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@example.com'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin123!'
/** Первый администратор создаётся с временным паролем; тесты меняют его на этот и дальше входят с ним. */
const adminChangedPassword = `${adminPassword}e2e`

export const password = 'E2e-Passw0rd'
export const unique = (prefix: string) => `${prefix}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

interface Auth {
  accessToken: string
  user: { id: string; mustChangePassword: boolean }
}

async function login(api: APIRequestContext, email: string, pwd: string): Promise<Auth | null> {
  const response = await api.post('/api/auth/login', { data: { email, password: pwd } })
  return response.ok() ? ((await response.json()) as Auth) : null
}

async function withToken(token: string) {
  return request.newContext({ baseURL, extraHTTPHeaders: { Authorization: `Bearer ${token}` } })
}

/** API от имени первого администратора (при первом прогоне меняет его временный пароль). */
export async function adminApi(): Promise<APIRequestContext> {
  const anonymous = await request.newContext({ baseURL })
  let auth = (await login(anonymous, adminEmail, adminPassword)) ?? (await login(anonymous, adminEmail, adminChangedPassword))
  expect(auth, `не удалось войти как ${adminEmail}`).not.toBeNull()
  if (auth!.user.mustChangePassword) {
    const changed = await anonymous.post('/api/auth/change-password', {
      headers: { Authorization: `Bearer ${auth!.accessToken}` },
      data: { currentPassword: adminPassword, newPassword: adminChangedPassword },
    })
    expect(changed.ok()).toBeTruthy()
    auth = (await changed.json()) as Auth
  }
  await anonymous.dispose()
  return withToken(auth!.accessToken)
}

export async function json<T>(response: Awaited<ReturnType<APIRequestContext['get']>>): Promise<T> {
  expect(response.ok(), `${response.url()} → ${response.status()} ${await response.text()}`).toBeTruthy()
  return (await response.json()) as T
}

/** Пользователь с временным паролем (при первом входе потребуется смена). */
export async function createUser(admin: APIRequestContext, role: 'Admin' | 'Manager', fullName: string) {
  const email = `${unique(role.toLowerCase())}@e2e.test`
  const user = await json<{ id: string }>(await admin.post('/api/users', { data: { email, fullName, role, password } }))
  return { id: user.id, email }
}

/** Вход через UI; если нужна смена временного пароля — меняет его на тот же с суффиксом и возвращает новый. */
export async function signIn(page: Page, email: string, pwd: string): Promise<string> {
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Пароль').fill(pwd)
  await page.getByRole('button', { name: 'Войти' }).click()
  await page.waitForURL(/\/(admin|change-password)/)
  if (!page.url().includes('/change-password')) return pwd

  const next = `${pwd}1`
  await page.getByLabel('Текущий пароль').fill(pwd)
  await page.getByLabel('Новый пароль').fill(next)
  await page.getByLabel('Повторите пароль').fill(next)
  await page.getByRole('button', { name: 'Сохранить пароль' }).click()
  await page.waitForURL(/\/admin/)
  return next
}

interface MailpitMessage {
  Subject: string
  To: { Address: string }[]
}

/** Письма получателю в Mailpit; null — Mailpit не настроен (E2E_MAILPIT_URL). */
export async function mailsTo(address: string): Promise<MailpitMessage[] | null> {
  if (!mailpitURL) return null
  const api = await request.newContext({ baseURL: mailpitURL })
  const response = await api.get('/api/v1/search', { params: { query: `to:"${address}"` } })
  const body = (await response.json()) as { messages: MailpitMessage[] }
  await api.dispose()
  return body.messages
}
