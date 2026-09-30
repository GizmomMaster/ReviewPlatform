import createClient from 'openapi-fetch'
import type { components, paths } from '@/api/schema'
import { refreshSession, sessionStore } from '@/features/auth/session'

export type Schemas = components['schemas']

export const api = createClient<paths>({ baseUrl: '' })

/** Эндпоинты, которым не нужен access-токен. */
const anonymousPaths = new Set(['/api/auth/login', '/api/auth/refresh', '/api/auth/logout'])
const REFRESH_AHEAD_MS = 30_000

api.use({
  async onRequest({ request }) {
    const path = new URL(request.url, window.location.origin).pathname
    if (anonymousPaths.has(path) || path.startsWith('/api/surveys/')) return request

    let session = sessionStore.get()
    if (session && session.expiresAt - Date.now() < REFRESH_AHEAD_MS) {
      await refreshSession()
      session = sessionStore.get()
    }
    if (session) request.headers.set('Authorization', `Bearer ${session.accessToken}`)
    return request
  },
  onResponse({ request, response }) {
    const path = new URL(request.url, window.location.origin).pathname
    // Токен отозван (блокировка, сброс пароля) — выходим, RequireAuth отправит на /login
    if (response.status === 401 && !anonymousPaths.has(path)) sessionStore.set(null)
    return response
  },
})

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined

  constructor(status: number, problem: unknown) {
    const details = typeof problem === 'object' && problem !== null ? (problem as ProblemDetails) : undefined
    super(details?.detail ?? details?.title ?? `Ошибка запроса (${status})`)
    this.status = status
    this.problem = details
  }
}

/** Возвращает data или бросает ApiError — для queryFn/mutationFn. */
export async function unwrap<T>(request: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await request
  if (!response.ok) throw new ApiError(response.status, error)
  return data as T
}
