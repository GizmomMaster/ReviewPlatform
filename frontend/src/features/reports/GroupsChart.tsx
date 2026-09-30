import { useState } from 'react'
import { Legend, PolarAngleAxis, PolarGrid, PolarRadiusAxis, Radar, RadarChart, ResponsiveContainer, Tooltip } from 'recharts'
import type { Schemas } from '@/api/client'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatPercent, formatScore, seriesColors } from '@/features/reports/format'
import { cn } from '@/lib/utils'

type Report = Schemas['SessionReportDto']
type Level = 'current' | 'target'

interface ChartRow {
  group: string
  others: number
  self: number
  threshold: number
  othersRaw: number | null
  selfRaw: number | null
}

/**
 * Паутинная диаграмма по группам (как в исходной матрице) + табличный вид с теми же числами.
 * Шкала 0–3, пунктир — порог «как правило». Группа без данных рисуется в центре и помечена в таблице.
 */
export function GroupsChart({ report }: { report: Report }) {
  const [level, setLevel] = useState<Level>('current')
  const hasTarget = report.targetGrade != null
  const stats = (g: Report['groups'][number]) => (level === 'target' && g.target ? g.target : g.current)

  const rows: ChartRow[] = report.groups.map((g) => {
    const s = stats(g)
    return { group: g.name, others: s.score ?? 0, self: s.self ?? 0, threshold: report.policy.metThreshold, othersRaw: s.score, selfRaw: s.self }
  })

  return (
    <div className="rounded-xl border bg-background p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-semibold">Профиль по группам</h2>
        {hasTarget && (
          <Tabs value={level} onValueChange={(v) => setLevel(v as Level)}>
            <TabsList>
              <TabsTrigger value="current">Текущий {report.currentGrade.code}</TabsTrigger>
              <TabsTrigger value="target">Следующий {report.targetGrade?.code}</TabsTrigger>
            </TabsList>
          </Tabs>
        )}
      </div>

      <div className="mt-2 grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div className="h-[380px]" role="img" aria-label="Паутинная диаграмма средних оценок по группам компетенций">
          <ResponsiveContainer width="100%" height="100%">
            <RadarChart data={rows} outerRadius="70%" margin={{ top: 8, right: 24, bottom: 8, left: 24 }}>
              <PolarGrid stroke="var(--border)" />
              <PolarAngleAxis dataKey="group" tick={{ fontSize: 11, fill: 'var(--muted-foreground)' }} />
              <PolarRadiusAxis domain={[0, 3]} tickCount={4} angle={90 - 180 / Math.max(rows.length, 1)} tick={{ fontSize: 10, fill: 'var(--muted-foreground)' }} axisLine={false} />
              <Radar name="Окружение" dataKey="others" stroke={seriesColors.others} strokeWidth={2} fill={seriesColors.others} fillOpacity={0.15} dot={{ r: 3 }} />
              <Radar name="Самооценка" dataKey="self" stroke={seriesColors.self} strokeWidth={2} fill={seriesColors.self} fillOpacity={0.08} dot={{ r: 3 }} />
              <Radar
                name={`Порог «как правило» (${formatScore(report.policy.metThreshold)})`}
                dataKey="threshold"
                stroke={seriesColors.threshold}
                strokeDasharray="4 4"
                strokeWidth={1.5}
                fill="none"
                dot={false}
                isAnimationActive={false}
              />
              <Tooltip content={<ChartTooltip />} />
              <Legend wrapperStyle={{ fontSize: 12 }} formatter={(value: string) => <span className="text-foreground">{value}</span>} />
            </RadarChart>
          </ResponsiveContainer>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Группа</TableHead>
              <TableHead className="text-right">Окружение</TableHead>
              <TableHead className="text-right">Самооценка</TableHead>
              <TableHead className="text-right">{level === 'target' ? 'Готовность' : 'Выполнено'}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {report.groups.map((g) => {
              const s = stats(g)
              const lowReadiness = level === 'target' && s.metShare != null && s.metShare < report.policy.groupReadinessHint
              return (
                <TableRow key={g.name}>
                  <TableCell className="whitespace-normal">{g.name}</TableCell>
                  <TableCell className="text-right tabular-nums">{formatScore(s.score)}</TableCell>
                  <TableCell className="text-right tabular-nums">{formatScore(s.self)}</TableCell>
                  <TableCell className={cn('text-right tabular-nums', lowReadiness && 'font-medium text-amber-700')}>
                    {s.total === 0 ? 'нет индикаторов' : s.included === 0 ? 'мало данных' : formatPercent(s.metShare)}
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </div>
      {level === 'target' && (
        <p className="mt-2 text-xs text-muted-foreground">Ориентир готовности по каждой группе — {formatPercent(report.policy.groupReadinessHint)}; ниже выделено.</p>
      )}
    </div>
  )
}

interface TooltipPayload {
  payload?: ChartRow
}

function ChartTooltip({ active, payload }: { active?: boolean; payload?: TooltipPayload[] }) {
  const row = payload?.[0]?.payload
  if (!active || !row) return null
  return (
    <div className="rounded-md border bg-background px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium">{row.group}</p>
      <p className="flex items-center gap-2">
        <span className="size-2 rounded-full" style={{ background: seriesColors.others }} />
        Окружение: <span className="tabular-nums">{row.othersRaw == null ? 'нет данных' : formatScore(row.othersRaw)}</span>
      </p>
      <p className="flex items-center gap-2">
        <span className="size-2 rounded-full" style={{ background: seriesColors.self }} />
        Самооценка: <span className="tabular-nums">{row.selfRaw == null ? 'нет данных' : formatScore(row.selfRaw)}</span>
      </p>
    </div>
  )
}
