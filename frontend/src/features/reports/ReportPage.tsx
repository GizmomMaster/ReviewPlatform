import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Download, Info } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { GroupsChart } from '@/features/reports/GroupsChart'
import { Highlights } from '@/features/reports/Highlights'
import { IndicatorsTable } from '@/features/reports/IndicatorsTable'
import { MetricTile } from '@/features/reports/MetricTile'
import { reportQuery } from '@/features/reports/queries'
import { SessionStatusBadge } from '@/features/sessions/SessionStatusBadge'
import { saveDownload } from '@/lib/download'

export function ReportPage() {
  const { id = '' } = useParams()
  const report = useQuery(reportQuery(id))
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set())
  const [downloading, setDownloading] = useState(false)

  if (report.isPending) return <Skeleton className="h-96 w-full" />
  if (report.isError) return <p className="text-destructive">{report.error.message}</p>

  const r = report.data
  const toggle = (indicatorId: string) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(indicatorId)) next.delete(indicatorId)
      else next.add(indicatorId)
      return next
    })
  const select = (indicatorId: string) => {
    setExpanded((prev) => new Set(prev).add(indicatorId))
    requestAnimationFrame(() => document.getElementById(`row-${indicatorId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' }))
  }

  const download = async () => {
    setDownloading(true)
    try {
      const { data, response } = await api.GET('/api/assessment-sessions/{id}/report/export', { params: { path: { id } }, parseAs: 'blob' })
      if (!response.ok || !data) throw new Error()
      saveDownload(data, response, 'report.xlsx')
    } catch {
      toast.error('Не удалось выгрузить отчёт')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <section className="space-y-4">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to={`/admin/sessions/${id}`}>
          <ArrowLeft /> К сессии
        </Link>
      </Button>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold">Отчёт: {r.employeeName}</h1>
            <SessionStatusBadge status={r.status} />
          </div>
          <p className="text-muted-foreground">
            {r.targetGrade
              ? `Переход ${r.currentGrade.code} ${r.currentGrade.name} → ${r.targetGrade.code} ${r.targetGrade.name}`
              : `Подтверждение ${r.currentGrade.code} ${r.currentGrade.name}`}
          </p>
        </div>
        <Button variant="outline" onClick={() => void download()} disabled={downloading}>
          <Download /> Excel
        </Button>
      </div>

      {r.isPreliminary && (
        <div className="flex gap-3 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950">
          <Info className="mt-0.5 size-4 shrink-0" />
          <p>
            Промежуточные результаты: анкеты отправили {r.submittedCount} из {r.participantCount}. Учитываются только отправленные анкеты.
          </p>
        </div>
      )}

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <MetricTile label={`Подтверждение ${r.currentGrade.code}`} value={r.currentConfirmation ?? null} hint={r.policy.currentConfirmationHint} />
        {r.targetGrade && <MetricTile label={`Готовность к ${r.targetGrade.code}`} value={r.targetReadiness ?? null} hint={r.policy.targetReadinessHint} />}
        <div className="rounded-xl border bg-background p-4">
          <p className="text-sm text-muted-foreground">Анкеты</p>
          <p className="mt-1 text-3xl font-semibold tabular-nums">
            {r.submittedCount}
            <span className="text-lg text-muted-foreground"> / {r.participantCount}</span>
          </p>
          <p className="mt-1 text-xs text-muted-foreground">отправлено</p>
        </div>
        <div className="rounded-xl border bg-background p-4">
          <p className="text-sm text-muted-foreground">Мало данных</p>
          <p className="mt-1 text-3xl font-semibold tabular-nums">{r.insufficientCount}</p>
          <p className="mt-1 text-xs text-muted-foreground">индикаторов с менее чем {r.policy.quorum} оценками окружения</p>
        </div>
      </div>

      <p className="text-xs text-muted-foreground">
        Итог индикатора — среднее оценок окружения (без самооценки и «не могу оценить»), все роли с равным весом. Индикатор выполнен при итоге от{' '}
        {r.policy.metThreshold.toLocaleString('ru-RU')}. Ориентиры — справочные, решение принимает руководитель.
      </p>

      <GroupsChart report={r} />
      <Highlights report={r} onSelect={select} />

      <h2 className="pt-2 text-lg font-semibold">Оценки по индикаторам</h2>
      <IndicatorsTable report={r} expanded={expanded} onToggle={toggle} />
    </section>
  )
}
