import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'

export const usersQuery = queryOptions({
  queryKey: ['users'],
  queryFn: ({ signal }) => unwrap(api.GET('/api/users', { signal })),
})
