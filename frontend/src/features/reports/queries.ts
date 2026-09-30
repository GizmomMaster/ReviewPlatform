import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import { sessionsKey } from '@/features/sessions/queries'

export const reportQuery = (id: string) =>
  queryOptions({
    queryKey: [...sessionsKey, id, 'report'],
    queryFn: ({ signal }) => unwrap(api.GET('/api/assessment-sessions/{id}/report', { params: { path: { id } }, signal })),
  })
