export interface ScaleOption {
  score: number | null
  notApplicable: boolean
  label: string
  hint: string
}

/** Шкала оценки (ТЗ, 6.1). */
export const scale: ScaleOption[] = [
  { score: 0, notApplicable: false, label: 'Не проявляет', hint: 'Не видел такого поведения или были обратные примеры' },
  { score: 1, notApplicable: false, label: 'Эпизодически', hint: 'Бывает, но редко или только с помощью и подсказкой' },
  { score: 2, notApplicable: false, label: 'Как правило', hint: 'Проявляет в большинстве ситуаций, иногда нужна помощь' },
  { score: 3, notApplicable: false, label: 'Стабильно', hint: 'Всегда и самостоятельно, может быть примером для других' },
  { score: null, notApplicable: true, label: 'Не могу оценить', hint: 'Не было возможности наблюдать. В расчётах не участвует' },
]

export interface Answer {
  indicatorId: string
  score: number | null
  notApplicable: boolean
  comment: string | null
}

export const isAnswered = (a: Answer | undefined) => !!a && (a.score !== null || a.notApplicable)
export const needsComment = (a: Answer | undefined) => !!a && (a.score === 0 || a.score === 3) && !a.comment?.trim()
