const dateFormat = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' })
const dateTimeFormat = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

export const formatDate = (iso: string) => dateFormat.format(new Date(iso))
export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso))

/** yyyy-mm-dd для input[type=date] в локальной зоне. */
export function toDateInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** Дедлайн — конец выбранного дня по локальному времени, в UTC ISO. */
export const endOfDayUtc = (dateInput: string) => new Date(`${dateInput}T23:59:59`).toISOString()

export function daysLeft(iso: string): number {
  return Math.floor((new Date(iso).getTime() - Date.now()) / 86_400_000)
}
