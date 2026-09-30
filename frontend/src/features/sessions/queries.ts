import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import type { SessionStatus } from '@/lib/labels'

export const sessionsKey = ['sessions'] as const

export const sessionsQuery = (status?: SessionStatus) =>
  queryOptions({
    queryKey: [...sessionsKey, 'list', { status }],
    queryFn: ({ signal }) => unwrap(api.GET('/api/assessment-sessions', { params: { query: { status } }, signal })),
  })

export const sessionQuery = (id: string) =>
  queryOptions({
    queryKey: [...sessionsKey, id],
    queryFn: ({ signal }) => unwrap(api.GET('/api/assessment-sessions/{id}', { params: { path: { id } }, signal })),
  })

export const surveyPreviewQuery = (id: string) =>
  queryOptions({
    queryKey: [...sessionsKey, id, 'preview'],
    queryFn: ({ signal }) => unwrap(api.GET('/api/assessment-sessions/{id}/survey-preview', { params: { path: { id } }, signal })),
  })

export const gradeRoleRulesQuery = queryOptions({
  queryKey: ['grade-role-rules'],
  queryFn: ({ signal }) => unwrap(api.GET('/api/grade-role-rules', { signal })),
  staleTime: 5 * 60_000,
})
