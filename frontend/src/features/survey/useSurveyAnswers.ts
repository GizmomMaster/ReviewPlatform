import { useCallback, useEffect, useRef, useState } from 'react'
import { api, unwrap } from '@/api/client'
import type { Answer } from '@/features/survey/scale'

export type SaveStatus = 'idle' | 'pending' | 'saving' | 'saved' | 'error'

const SAVE_DELAY_MS = 1000
const storageKey = (token: string) => `survey-pending:${token}`

function readPending(token: string): Record<string, Answer> {
  try {
    return JSON.parse(localStorage.getItem(storageKey(token)) ?? '{}') as Record<string, Answer>
  } catch {
    return {}
  }
}

function writePending(token: string, pending: Record<string, Answer>) {
  try {
    if (Object.keys(pending).length === 0) localStorage.removeItem(storageKey(token))
    else localStorage.setItem(storageKey(token), JSON.stringify(pending))
  } catch {
    // localStorage недоступен (приватный режим) — остаётся только сервер
  }
}

/**
 * Ответы анкеты с автосохранением: изменения копятся в «pending», дублируются в localStorage
 * и отправляются на сервер через секунду после последней правки. Несохранённое переживает перезагрузку.
 */
export function useSurveyAnswers(token: string, serverAnswers: Answer[]) {
  const [initialPending] = useState(() => readPending(token))
  const [answers, setAnswers] = useState<Record<string, Answer>>(() => ({
    ...Object.fromEntries(serverAnswers.map((a) => [a.indicatorId, a])),
    ...initialPending,
  }))
  const pending = useRef<Record<string, Answer>>(initialPending)
  const timer = useRef<number | undefined>(undefined)
  const [status, setStatus] = useState<SaveStatus>(Object.keys(initialPending).length > 0 ? 'pending' : 'idle')

  const flush = useCallback(async () => {
    window.clearTimeout(timer.current)
    const batch = pending.current
    if (Object.keys(batch).length === 0) return true
    pending.current = {}
    setStatus('saving')
    try {
      await unwrap(api.PUT('/api/surveys/{token}/draft', { params: { path: { token } }, body: { answers: Object.values(batch) } }))
      writePending(token, pending.current)
      setStatus(Object.keys(pending.current).length > 0 ? 'pending' : 'saved')
      return true
    } catch {
      // Возвращаем неотправленное в очередь; более свежие правки важнее
      pending.current = { ...batch, ...pending.current }
      writePending(token, pending.current)
      setStatus('error')
      return false
    }
  }, [token])

  const update = useCallback(
    (answer: Answer) => {
      setAnswers((prev) => ({ ...prev, [answer.indicatorId]: answer }))
      pending.current = { ...pending.current, [answer.indicatorId]: answer }
      writePending(token, pending.current)
      setStatus('pending')
      window.clearTimeout(timer.current)
      timer.current = window.setTimeout(() => void flush(), SAVE_DELAY_MS)
    },
    [flush, token],
  )

  // Досылаем то, что осталось с прошлого раза, и сохраняем при уходе со страницы
  useEffect(() => {
    if (Object.keys(pending.current).length > 0) void flush()
    const onHide = () => {
      if (document.visibilityState === 'hidden') void flush()
    }
    document.addEventListener('visibilitychange', onHide)
    return () => {
      document.removeEventListener('visibilitychange', onHide)
      window.clearTimeout(timer.current)
    }
  }, [flush])

  const clear = useCallback(() => {
    pending.current = {}
    writePending(token, {})
  }, [token])

  return { answers, update, flush, clear, status }
}
