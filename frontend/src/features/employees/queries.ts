import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'

export const employeesKey = ['employees'] as const

export const employeesQuery = (search: string, includeArchived: boolean) =>
  queryOptions({
    queryKey: [...employeesKey, { search, includeArchived }],
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/employees', { params: { query: { search: search || undefined, includeArchived } }, signal })),
  })
