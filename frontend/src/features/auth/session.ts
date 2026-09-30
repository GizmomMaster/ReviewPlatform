import { useSyncExternalStore } from 'react'
import type { components } from '@/api/schema'

export type AuthUser = components['schemas']['AuthUser']
type AuthResponse = components['schemas']['AuthResponse']

export interface Session {
  accessToken: string
  expiresAt: number
  user: AuthUser
}

interface SessionState {
  /** false, пока не выполнена первая попытка восстановить сессию по refresh-cookie */
  ready: boolean
  session: Session | null
}

let state: SessionState = { ready: false, session: null }
const listeners = new Set<() => void>()

function setState(next: Partial<SessionState>) {
  state = { ...state, ...next }
  listeners.forEach((listener) => listener())
}

export const sessionStore = {
  get: () => state.session,
  subscribe(listener: () => void) {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
  set(response: AuthResponse | null) {
    setState({
      session: response
        ? { accessToken: response.accessToken, expiresAt: Date.parse(response.expiresAtUtc), user: response.user }
        : null,
    })
  },
}

export function useSessionState() {
  return useSyncExternalStore(sessionStore.subscribe, () => state)
}

let refreshing: Promise<boolean> | null = null

/** Обновляет access-токен по refresh-cookie. Параллельные вызовы объединяются в один запрос. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' })
    .then(async (response) => {
      sessionStore.set(response.ok ? ((await response.json()) as AuthResponse) : null)
      return response.ok
    })
    .catch(() => {
      sessionStore.set(null)
      return false
    })
    .finally(() => {
      refreshing = null
    })
  return refreshing
}

let bootstrap: Promise<void> | null = null

/** Однократное восстановление сессии при загрузке приложения. */
export function bootstrapSession(): Promise<void> {
  bootstrap ??= refreshSession().then(() => setState({ ready: true }))
  return bootstrap
}
