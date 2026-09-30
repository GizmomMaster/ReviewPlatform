import { Textarea } from '@/components/ui/textarea'
import { needsComment, scale, type Answer } from '@/features/survey/scale'
import { cn } from '@/lib/utils'

interface IndicatorCardProps {
  number: number
  indicatorId: string
  text: string
  answer: Answer | undefined
  error?: string | undefined
  /** Подсвечивать незаполненное (после попытки отправить) */
  showValidation: boolean
  onChange: (answer: Answer) => void
}

export function IndicatorCard({ number, indicatorId, text, answer, error, showValidation, onChange }: IndicatorCardProps) {
  const current: Answer = answer ?? { indicatorId, score: null, notApplicable: false, comment: null }
  const commentRequired = current.score === 0 || current.score === 3
  const missingComment = showValidation && needsComment(current)
  const message = error ?? (missingComment ? 'Нужен пример в комментарии' : showValidation && !answer ? 'Выберите оценку' : undefined)

  return (
    <article id={`indicator-${indicatorId}`} className={cn('rounded-xl border bg-background p-4 shadow-xs', message && 'border-destructive')}>
      <p className="mb-3 leading-snug">
        <span className="mr-1 text-muted-foreground">{number}.</span>
        {text}
      </p>

      <div className="grid grid-cols-4 gap-1.5" role="radiogroup" aria-label="Оценка">
        {scale
          .filter((o) => !o.notApplicable)
          .map((o) => {
            const selected = current.score === o.score
            return (
              <button
                key={o.score}
                type="button"
                role="radio"
                aria-checked={selected}
                title={o.hint}
                onClick={() => onChange({ ...current, score: o.score, notApplicable: false })}
                className={cn(
                  'flex min-h-16 flex-col items-center justify-center gap-0.5 rounded-lg border px-0.5 py-2 text-center transition-colors',
                  selected ? 'border-foreground bg-foreground text-background' : 'hover:bg-muted',
                )}
              >
                <span className="text-lg leading-none font-semibold">{o.score}</span>
                <span className="text-[10px] leading-tight tracking-tight">{o.label}</span>
              </button>
            )
          })}
      </div>
      <button
        type="button"
        role="radio"
        aria-checked={current.notApplicable}
        onClick={() => onChange({ ...current, score: null, notApplicable: true })}
        className={cn(
          'mt-1.5 w-full rounded-lg border py-2 text-sm transition-colors',
          current.notApplicable ? 'border-foreground bg-foreground text-background' : 'text-muted-foreground hover:bg-muted',
        )}
      >
        Не могу оценить
      </button>

      <Textarea
        className="mt-3 min-h-16 text-sm"
        placeholder={commentRequired ? 'Приведите пример — для оценок 0 и 3 это обязательно' : 'Комментарий или пример (по желанию)'}
        value={current.comment ?? ''}
        maxLength={4000}
        aria-invalid={missingComment || undefined}
        onChange={(e) => onChange({ ...current, comment: e.target.value || null })}
      />
      {message && <p className="mt-1.5 text-sm text-destructive">{message}</p>}
    </article>
  )
}
