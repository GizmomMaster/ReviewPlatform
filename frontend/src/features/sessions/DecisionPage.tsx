import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { api, ApiError, unwrap, type Schemas } from '@/api/client'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { gradesQuery } from '@/features/matrix/queries'
import { formatPercent, formatScore } from '@/features/reports/format'
import { reportQuery } from '@/features/reports/queries'
import { sessionsKey } from '@/features/sessions/queries'
import { employeesKey } from '@/features/employees/queries'
import { decisionOutcomeLabels, type DecisionOutcome } from '@/lib/labels'

type Report = Schemas['SessionReportDto']
type Grade = Schemas['GradeDto']
type ReportIndicator = Report['indicators'][number]

const SUGGESTIONS_SHOWN = 5

interface PlanItem {
  key: string
  text: string
  sessionIndicatorId: string | null
  dueDate: string
}

let planKey = 0
const nextKey = () => `plan-${++planKey}`

export function DecisionPage() {
  const { id = '' } = useParams()
  const report = useQuery(reportQuery(id))
  const grades = useQuery(gradesQuery)

  if (report.isPending || grades.isPending) return <Skeleton className="h-96 w-full" />
  if (report.isError) return <p className="text-destructive">{report.error.message}</p>
  if (grades.isError) return <p className="text-destructive">Не удалось загрузить грейды.</p>
  if (report.data.status !== 'AwaitingDecision') {
    return (
      <div className="space-y-3">
        <p>Решение принимается, когда опрос завершён (статус «Ждёт решения»).</p>
        <Button asChild variant="outline">
          <Link to={`/admin/sessions/${id}`}>К сессии</Link>
        </Button>
      </div>
    )
  }
  return <DecisionForm report={report.data} grades={grades.data} />
}

function DecisionForm({ report, grades }: { report: Report; grades: Grade[] }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const current = report.currentGrade
  const higher = grades.filter((g) => g.order > current.order)
  const lowerOrSame = grades.filter((g) => g.order <= current.order).reverse()

  const [outcome, setOutcome] = useState<DecisionOutcome>(report.targetGrade ? 'Promoted' : 'GradeConfirmed')
  const [newGradeId, setNewGradeId] = useState(report.targetGrade?.id ?? current.id)
  const [comment, setComment] = useState('')
  const [plan, setPlan] = useState<PlanItem[]>([])
  const [confirming, setConfirming] = useState(false)
  const [showErrors, setShowErrors] = useState(false)

  const suggestionGroups = [
    { title: `Зоны роста — ${report.targetGrade?.code ?? ''}`, items: report.indicators.filter((i) => i.level === 'Target' && i.isMet === false) },
    { title: `Пробелы текущего уровня — ${current.code}`, items: report.indicators.filter((i) => i.level === 'Current' && i.isMet === false) },
  ].filter((g) => g.items.length > 0)
  const inPlan = new Set(plan.map((p) => p.sessionIndicatorId))

  const changeOutcome = (value: DecisionOutcome) => {
    setOutcome(value)
    setNewGradeId(value === 'Promoted' ? (report.targetGrade?.id ?? higher[0]?.id ?? current.id) : current.id)
  }

  const decide = useMutation({
    mutationFn: () =>
      unwrap(
        api.POST('/api/assessment-sessions/{id}/decision', {
          params: { path: { id: report.sessionId } },
          body: {
            outcome,
            newGradeId,
            comment,
            planItems: plan.map((p) => ({ text: p.text, sessionIndicatorId: p.sessionIndicatorId, dueDate: p.dueDate || null })),
          },
        }),
      ),
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: sessionsKey }), queryClient.invalidateQueries({ queryKey: employeesKey })])
      toast.success('Решение сохранено, сессия закрыта')
      await navigate(`/admin/sessions/${report.sessionId}`)
    },
  })

  const commentMissing = comment.trim().length === 0
  const emptyPlanItem = plan.some((p) => !p.text.trim())
  const submit = () => {
    setShowErrors(true)
    if (!commentMissing && !emptyPlanItem) setConfirming(true)
  }
  const newGrade = grades.find((g) => g.id === newGradeId)

  return (
    <section className="mx-auto max-w-3xl space-y-4">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to={`/admin/sessions/${report.sessionId}`}>
          <ArrowLeft /> К сессии
        </Link>
      </Button>
      <div>
        <h1 className="text-2xl font-semibold">Решение: {report.employeeName}</h1>
        <p className="text-muted-foreground">
          {report.targetGrade ? `Оценка перехода ${current.code} → ${report.targetGrade.code}` : `Подтверждение ${current.code}`} · подтверждение{' '}
          {current.code}: {formatPercent(report.currentConfirmation)}
          {report.targetGrade && ` · готовность к ${report.targetGrade.code}: ${formatPercent(report.targetReadiness)}`} ·{' '}
          <Link className="underline" to={`/admin/sessions/${report.sessionId}/report`}>
            отчёт
          </Link>
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Итог</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4">
          <RadioGroup value={outcome} onValueChange={(v) => changeOutcome(v as DecisionOutcome)} className="gap-3">
            {(['Promoted', 'GradeConfirmed', 'NotConfirmed'] as const).map((o) => (
              <label key={o} className="flex items-start gap-3">
                <RadioGroupItem value={o} disabled={o === 'Promoted' && higher.length === 0} className="mt-0.5" />
                <span>
                  {decisionOutcomeLabels[o]}
                  <span className="block text-sm text-muted-foreground">
                    {o === 'Promoted' && 'Новый грейд выше текущего'}
                    {o === 'GradeConfirmed' && `Сотрудник остаётся на ${current.code}`}
                    {o === 'NotConfirmed' && `Остаётся на ${current.code} или понижается`}
                  </span>
                </span>
              </label>
            ))}
          </RadioGroup>

          {outcome !== 'GradeConfirmed' && (
            <div className="grid max-w-72 gap-1.5">
              <Label>Новый грейд</Label>
              <Select value={newGradeId} onValueChange={setNewGradeId}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(outcome === 'Promoted' ? higher : lowerOrSame).map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.code} · {g.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="grid gap-1.5">
            <Label htmlFor="comment">Обоснование</Label>
            <Textarea
              id="comment"
              className="min-h-28"
              placeholder="Почему принято такое решение: сильные стороны, что нужно подтянуть"
              value={comment}
              maxLength={4000}
              aria-invalid={showErrors && commentMissing ? true : undefined}
              onChange={(e) => setComment(e.target.value)}
            />
            {showErrors && commentMissing && <p className="text-sm text-destructive">Опишите обоснование решения.</p>}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>План развития</CardTitle>
          <CardDescription>Выберите индикаторы из зон роста или добавьте свои пункты. Срок — по желанию.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          {plan.length > 0 && (
            <ol className="grid gap-2">
              {plan.map((item, index) => (
                <li key={item.key} className="grid gap-2 rounded-md border p-2 sm:grid-cols-[1fr_10rem_auto] sm:items-start">
                  <Textarea
                    className="min-h-10 text-sm"
                    value={item.text}
                    aria-label={`Пункт ${index + 1}`}
                    aria-invalid={showErrors && !item.text.trim() ? true : undefined}
                    onChange={(e) => setPlan(plan.map((p) => (p.key === item.key ? { ...p, text: e.target.value } : p)))}
                  />
                  <Input
                    type="date"
                    aria-label="Срок"
                    value={item.dueDate}
                    onChange={(e) => setPlan(plan.map((p) => (p.key === item.key ? { ...p, dueDate: e.target.value } : p)))}
                  />
                  <Button variant="ghost" size="icon" aria-label="Убрать пункт" onClick={() => setPlan(plan.filter((p) => p.key !== item.key))}>
                    <Trash2 />
                  </Button>
                </li>
              ))}
            </ol>
          )}
          {showErrors && emptyPlanItem && <p className="text-sm text-destructive">Заполните или уберите пустые пункты плана.</p>}
          <div>
            <Button variant="outline" onClick={() => setPlan([...plan, { key: nextKey(), text: '', sessionIndicatorId: null, dueDate: '' }])}>
              <Plus /> Свой пункт
            </Button>
          </div>

          {suggestionGroups.map((group) => (
            <SuggestionList
              key={group.title}
              title={group.title}
              items={group.items}
              inPlan={inPlan}
              onAdd={(i) => setPlan([...plan, { key: nextKey(), text: i.text, sessionIndicatorId: i.id, dueDate: '' }])}
            />
          ))}
        </CardContent>
      </Card>

      <FormError message={decide.error instanceof ApiError ? decide.error.message : decide.error ? 'Не удалось сохранить решение' : undefined} />
      <div className="flex justify-end gap-2">
        <Button asChild variant="outline">
          <Link to={`/admin/sessions/${report.sessionId}`}>Отмена</Link>
        </Button>
        <Button onClick={submit} disabled={decide.isPending}>
          Сохранить решение
        </Button>
      </div>

      <ConfirmDialog
        open={confirming}
        title="Сохранить решение?"
        description={`${decisionOutcomeLabels[outcome]}${newGrade && newGrade.id !== current.id ? `: ${current.code} → ${newGrade.code}` : ''}. Сессия будет закрыта${newGrade && newGrade.id !== current.id ? ', грейд сотрудника обновится' : ''}. Изменить решение потом нельзя.`}
        confirmLabel="Сохранить"
        onCancel={() => setConfirming(false)}
        onConfirm={() => {
          setConfirming(false)
          decide.mutate()
        }}
      />
    </section>
  )
}

interface SuggestionListProps {
  title: string
  items: ReportIndicator[]
  inPlan: ReadonlySet<string | null>
  onAdd: (indicator: ReportIndicator) => void
}

/** Подсказки для плана: худшие по итогу — первыми, длинный список свёрнут. */
function SuggestionList({ title, items, inPlan, onAdd }: SuggestionListProps) {
  const [expanded, setExpanded] = useState(false)
  const sorted = [...items].sort((a, b) => (a.score ?? 0) - (b.score ?? 0))
  const shown = expanded ? sorted : sorted.slice(0, SUGGESTIONS_SHOWN)

  return (
    <div className="space-y-1">
      <p className="text-sm font-medium">
        {title} <span className="font-normal text-muted-foreground">· {items.length}</span>
      </p>
      <ul className="divide-y rounded-md border">
        {shown.map((i) => (
          <li key={i.id} className="flex items-start gap-3 p-2 text-sm">
            <span className="flex-1">
              <span className="text-muted-foreground">{i.groupName} · </span>
              {i.text}
              <span className="ml-1 text-xs text-muted-foreground">(итог {formatScore(i.score)})</span>
            </span>
            <Button size="sm" variant="ghost" disabled={inPlan.has(i.id)} onClick={() => onAdd(i)}>
              {inPlan.has(i.id) ? 'Добавлено' : (<><Plus /> В план</>)}
            </Button>
          </li>
        ))}
      </ul>
      {sorted.length > SUGGESTIONS_SHOWN && (
        <Button variant="link" size="sm" className="px-0" onClick={() => setExpanded(!expanded)}>
          {expanded ? 'Свернуть' : `Показать ещё ${sorted.length - SUGGESTIONS_SHOWN}`}
        </Button>
      )}
    </div>
  )
}
