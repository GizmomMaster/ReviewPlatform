import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { ApiError } from '@/api/client'

/** Раскладывает ошибки ProblemDetails по полям формы; остальное — в root. */
export function applyServerErrors<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>, fields: readonly Path<T>[]) {
  if (!(error instanceof ApiError)) {
    setError('root', { message: 'Не удалось выполнить запрос. Проверьте соединение.' })
    return
  }

  const unmatched: string[] = []
  for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
    const field = fields.find((f) => f === key)
    if (field) setError(field, { message: messages.join(' ') })
    else unmatched.push(...messages)
  }

  if (unmatched.length > 0 || !error.problem?.errors) {
    setError('root', { message: unmatched.length > 0 ? unmatched.join(' ') : error.message })
  }
}
