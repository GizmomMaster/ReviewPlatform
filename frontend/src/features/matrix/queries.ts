import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'

export const tracksQuery = queryOptions({
  queryKey: ['tracks'],
  queryFn: ({ signal }) => unwrap(api.GET('/api/tracks', { signal })),
  staleTime: Infinity,
})

export const matrixQuery = (trackId: string) =>
  queryOptions({
    queryKey: ['tracks', trackId, 'matrix'],
    queryFn: ({ signal }) => unwrap(api.GET('/api/tracks/{trackId}/matrix', { params: { path: { trackId } }, signal })),
  })

export const gradesQuery = queryOptions({
  queryKey: ['grades'],
  queryFn: ({ signal }) => unwrap(api.GET('/api/grades', { signal })),
  staleTime: Infinity,
})
