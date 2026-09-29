import { useState } from 'react'
import type { Schemas } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatScore } from '@/features/reports/format'

type Report = Schemas['SessionReportDto']
type Indicator = Report['indicators'][number]
type Kind = 'growth' | 'gaps' | 'disputed' | 'blind'

/** Выводы для разговора с сотрудником (ТЗ, 6.2 п.7–9). */
export function Highlights({ report, onSelect }: { report: Report; onSelect: (id: string) => void }) {
  const lists: Record<Kind, { title: string; empty: string; items: Indicator[] }> = {
    growth: {
      title: 'Зоны роста',
      empty: 'Все индикаторы следующего грейда выполнены или по ним мало данных.',
      items: report.indicators.filter((i) => i.level === 'Target' && i.isMet === false),
    },
    gaps: {
      title: 'Пробелы текущего уровня',
      empty: 'Все индикаторы текущего грейда выполнены или по ним мало данных.',
      items: report.indicators.filter((i) => i.level === 'Current' && i.isMet === false),
    },
    disputed: {
      title: 'Спорные',
      empty: 'Нет индикаторов с сильным разбросом оценок.',
      items: report.indicators.filter((i) => i.isDisputed),
    },
    blind: {
      title: 'Слепые пятна',
      empty: 'Самооценка нигде сильно не расходится с оценкой окружения.',
      items: report.indicators.filter((i) => i.blindSpot !== 'None'),
    },
  }
  const kinds = (Object.keys(lists) as Kind[]).filter((k) => k !== 'growth' || report.targetGrade)
  const [kind, setKind] = useState<Kind>(kinds[0] ?? 'gaps')
  const current = lists[kind]

  return (
    <div className="rounded-xl border bg-background p-4">
      <Tabs value={kind} onValueChange={(v) => setKind(v as Kind)}>
        <TabsList className="h-auto flex-wrap">
          {kinds.map((k) => (
            <TabsTrigger key={k} value={k}>
              {lists[k].title} <span className="ml-1 text-muted-foreground tabular-nums">{lists[k].items.length}</span>
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>
      {current.items.length === 0 ? (
        <p className="mt-3 text-sm text-muted-foreground">{current.empty}</p>
      ) : (
        <ul className="mt-3 divide-y">
          {current.items.map((i) => (
            <li key={i.id}>
              <button type="button" onClick={() => onSelect(i.id)} className="flex w-full items-start gap-3 py-2 text-left hover:bg-muted/50">
                <span className="flex-1 text-sm">
                  <span className="text-muted-foreground">{i.groupName} · </span>
                  {i.text}
                </span>
                <span className="shrink-0 text-right text-xs text-muted-foreground tabular-nums">
                  окружение {formatScore(i.score)}
                  <br />
                  самооценка {formatScore(i.selfScore)}
                </span>
                {kind === 'blind' && (
                  <Badge variant="outline" className="shrink-0">
                    {i.blindSpot === 'Overestimated' ? 'переоценивает' : 'недооценивает'}
                  </Badge>
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
