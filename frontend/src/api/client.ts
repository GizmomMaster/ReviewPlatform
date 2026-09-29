import createClient from 'openapi-fetch'
import type { components, paths } from '@/api/schema'

export type Schemas = components['schemas']

export const api = createClient<paths>({ baseUrl: '' })

export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, problem: unknown) {
    super(`API request failed with status ${status}`)
    this.status = status
    this.problem = problem
  }
}

/** Возвращает data или бросает ApiError — для использования в queryFn/mutationFn. */
export async function unwrap<T>(request: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await request
  if (!response.ok || data === undefined) {
    throw new ApiError(response.status, error)
  }
  return data
}
