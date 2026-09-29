import { formatPercent } from '@/features/reports/format'
import { cn } from '@/lib/utils'

interface MetricTileProps {
  label: string
  value: number | null
  hint?: number | undefined
  note?: string | undefined
}

/** Показатель с ориентиром из политики. Ориентир — справка, а не вердикт. */
export function MetricTile({ label, value, hint, note }: MetricTileProps) {
  const below = value != null && hint != null && value < hint
  return (
    <div className="rounded-xl border bg-background p-4">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-1 text-3xl font-semibold tabular-nums">{formatPercent(value)}</p>
      <p className={cn('mt-1 text-xs text-muted-foreground')}>
        {hint != null && (
          <>
            ориентир {formatPercent(hint)}
            {value != null && <span className={cn('ml-1', below ? 'text-amber-700' : 'text-emerald-700')}>{below ? '· ниже ориентира' : '· не ниже ориентира'}</span>}
          </>
        )}
        {note}
      </p>
    </div>
  )
}
