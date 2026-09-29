import { api } from '@/api/client'
import { queryClient } from '@/app/query-client'
import { sessionStore } from '@/features/auth/session'

export async function logout() {
  await api.POST('/api/auth/logout').catch(() => undefined)
  sessionStore.set(null)
  queryClient.clear()
}
