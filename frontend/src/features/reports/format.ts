const scoreFormat = new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 1, maximumFractionDigits: 1 })
const percentFormat = new Intl.NumberFormat('ru-RU', { style: 'percent', maximumFractionDigits: 0 })

export const formatScore = (value: number | null | undefined) => (value == null ? '—' : scoreFormat.format(value))
export const formatPercent = (value: number | null | undefined) => (value == null ? '—' : percentFormat.format(value))

/** Цвета серий: слоты 1–2 эталонной палитры (проверены валидатором для светлой темы). */
export const seriesColors = {
  others: '#2a78d6',
  self: '#eb6834',
  threshold: '#8a8984',
} as const
