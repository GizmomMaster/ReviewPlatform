import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowLeft, ArrowRight, CheckCircle2, Cloud, CloudOff, Eye, Loader2, Lock } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { useParams } from 'react-router'
import { api, ApiError, unwrap, type Schemas } from '@/api/client'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/progress'
import { IndicatorCard } from '@/features/survey/IndicatorCard'
import { isAnswered, needsComment, scale, type Answer } from '@/features/survey/scale'
import { useSurveyAnswers, type SaveStatus } from '@/features/survey/useSurveyAnswers'
import { formatDate } from '@/lib/dates'
import { evaluatorRoleLabels } from '@/lib/labels'
import { cn } from '@/lib/utils'

type Survey = Schemas['SurveyDto']

export function SurveyPage() {
  const { token = '' } = useParams()
  const survey = useQuery({
    queryKey: ['survey', token],
    queryFn: ({ signal }) => unwrap(api.GET('/api/surveys/{token}', { params: { path: { token } }, signal })),
    retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
    staleTime: Infinity,
  })

  if (survey.isPending) {
    return (
      <Screen>
        <Loader2 className="mx-auto size-8 animate-spin text-muted-foreground" />
      </Screen>
    )
  }
  if (survey.isError) {
    const invalid = survey.error instanceof ApiError && survey.error.status === 404
    return (
      <Screen icon={<Lock className="size-10 text-muted-foreground" />} title={invalid ? 'Ссылка недействительна' : 'Не удалось загрузить анкету'}>
        {invalid
          ? 'Каждое новое письмо об опросе (напоминание, продление срока) содержит новую ссылку, а старые перестают работать. Откройте ссылку из последнего письма — сохранённые ответы на месте. Если письма нет, обратитесь к руководителю.'
          : 'Проверьте подключение к интернету и обновите страницу.'}
      </Screen>
    )
  }
  if (survey.data.state === 'Submitted') return <ThanksScreen already />
  if (survey.data.state === 'Closed') {
    return (
      <Screen icon={<Lock className="size-10 text-muted-foreground" />} title="Опрос завершён">
        Ответы больше не принимаются. Если вы не успели отправить анкету, обратитесь к руководителю.
      </Screen>
    )
  }
  return <SurveyFlow token={token} survey={survey.data} />
}

function SurveyFlow({ token, survey }: { token: string; survey: Survey }) {
  const { answers, update, flush, clear, status } = useSurveyAnswers(token, survey.answers)
  // -1 — вступление, 0..n-1 — группы, n — проверка и отправка
  const [step, setStep] = useState(survey.answers.length > 0 ? 0 : -1)
  const [showValidation, setShowValidation] = useState(false)
  const [serverErrors, setServerErrors] = useState<Record<string, string>>({})
  const [confirming, setConfirming] = useState(false)
  const [submitted, setSubmitted] = useState(false)

  const groups = survey.groups
  const indicators = groups.flatMap((g) => g.indicators)
  const answeredCount = indicators.filter((i) => isAnswered(answers[i.id])).length
  const problems = groups
    .map((g, index) => ({
      index,
      name: g.name,
      unanswered: g.indicators.filter((i) => !isAnswered(answers[i.id])).length,
      missingComments: g.indicators.filter((i) => needsComment(answers[i.id])).length,
    }))
    .filter((p) => p.unanswered > 0 || p.missingComments > 0)

  useEffect(() => window.scrollTo({ top: 0 }), [step])

  const submit = useMutation({
    mutationFn: async () => {
      await flush()
      return unwrap(api.POST('/api/surveys/{token}/submit', { params: { path: { token } }, body: { answers: Object.values(answers) } }))
    },
    onSuccess: () => {
      clear()
      setSubmitted(true)
    },
    onError: (error) => {
      if (error instanceof ApiError && error.problem?.errors) {
        setServerErrors(Object.fromEntries(Object.entries(error.problem.errors).map(([id, messages]) => [id, messages.join(' ')])))
        setShowValidation(true)
      }
    },
  })

  const onChange = (answer: Answer) => {
    update(answer)
    if (serverErrors[answer.indicatorId]) {
      setServerErrors((prev) => Object.fromEntries(Object.entries(prev).filter(([id]) => id !== answer.indicatorId)))
    }
  }

  if (submitted) return <ThanksScreen />

  if (step === -1) {
    return (
      <Shell>
        <div className="space-y-5 py-4">
          <div>
            <p className="text-sm text-muted-foreground">Оценка компетенций</p>
            <h1 className="text-2xl font-semibold">{survey.employeeName}</h1>
          </div>
          <p>
            {survey.role === 'Self'
              ? 'Вы оцениваете себя.'
              : `${survey.respondentName}, вас попросили оценить коллегу в роли «${evaluatorRoleLabels[survey.role]}».`}{' '}
            Заполните анкету до <b>{formatDate(survey.deadlineAtUtc)}</b>.
          </p>
          <div className="flex gap-3 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950">
            <Eye className="mt-0.5 size-4 shrink-0" />
            <p>Ваши оценки и комментарии будут видны руководителю сотрудника с указанием вашего имени.</p>
          </div>
          <div className="space-y-2">
            <p className="font-medium">
              Как оценивать: насколько устойчиво человек проявляет описанное поведение ({indicators.length} утверждений, {groups.length} разделов)
            </p>
            <ul className="space-y-1.5 text-sm">
              {scale.map((o) => (
                <li key={o.label} className="flex gap-2">
                  <span className="w-7 shrink-0 text-center font-semibold">{o.notApplicable ? '—' : o.score}</span>
                  <span>
                    <b className="font-medium">{o.label}.</b> <span className="text-muted-foreground">{o.hint}</span>
                  </span>
                </li>
              ))}
            </ul>
            <p className="text-sm text-muted-foreground">
              Оценивайте объективно, не округляйте в большую сторону. Для оценок 0 и 3 приведите пример в комментарии. Ответы сохраняются
              автоматически — можно закрыть страницу и вернуться по той же ссылке.
            </p>
          </div>
          <Button size="lg" className="w-full" onClick={() => setStep(0)}>
            Начать
          </Button>
        </div>
      </Shell>
    )
  }

  const onReview = step === groups.length
  const group = groups[step]
  const numberOffset = groups.slice(0, step).reduce((sum, g) => sum + g.indicators.length, 0)

  return (
    <Shell
      header={
        <div className="space-y-2">
          <div className="flex items-center justify-between gap-2 text-sm">
            <span className="truncate font-medium">{survey.employeeName}</span>
            <SaveIndicator status={status} />
          </div>
          <Progress value={(answeredCount / indicators.length) * 100} />
          <p className="text-xs text-muted-foreground">
            Отвечено {answeredCount} из {indicators.length}
          </p>
        </div>
      }
      footer={
        <div className="flex gap-2">
          <Button variant="outline" className="flex-1" onClick={() => setStep(step - 1)}>
            <ArrowLeft /> Назад
          </Button>
          {onReview ? (
            <Button className="flex-1" disabled={submit.isPending} onClick={() => (problems.length > 0 ? setShowValidation(true) : setConfirming(true))}>
              {submit.isPending ? 'Отправка…' : 'Отправить оценку'}
            </Button>
          ) : (
            <Button className="flex-1" onClick={() => setStep(step + 1)}>
              {step === groups.length - 1 ? 'К отправке' : 'Далее'} <ArrowRight />
            </Button>
          )}
        </div>
      }
    >
      {group && (
        <div className="space-y-3">
          <div>
            <p className="text-xs text-muted-foreground">
              Раздел {step + 1} из {groups.length}
            </p>
            <h2 className="text-xl font-semibold">{group.name}</h2>
          </div>
          {group.indicators.map((indicator, i) => (
            <IndicatorCard
              key={indicator.id}
              number={numberOffset + i + 1}
              indicatorId={indicator.id}
              text={indicator.text}
              answer={answers[indicator.id]}
              error={serverErrors[indicator.id]}
              showValidation={showValidation}
              onChange={onChange}
            />
          ))}
        </div>
      )}

      {onReview && (
        <div className="space-y-4">
          <h2 className="text-xl font-semibold">Проверка и отправка</h2>
          {problems.length === 0 ? (
            <div className="flex gap-3 rounded-lg border border-emerald-300 bg-emerald-50 p-3 text-emerald-950">
              <CheckCircle2 className="mt-0.5 size-5 shrink-0" />
              <p>Все утверждения оценены. После отправки изменить ответы будет нельзя.</p>
            </div>
          ) : (
            <div className="space-y-2">
              <p className={cn(showValidation && 'text-destructive')}>Перед отправкой заполните пропущенное:</p>
              {problems.map((p) => (
                <button
                  key={p.index}
                  type="button"
                  onClick={() => {
                    setShowValidation(true)
                    setStep(p.index)
                  }}
                  className="flex w-full items-center justify-between gap-2 rounded-lg border bg-background p-3 text-left hover:bg-muted"
                >
                  <span className="font-medium">{p.name}</span>
                  <span className="text-sm text-muted-foreground">
                    {[p.unanswered > 0 && `без оценки: ${p.unanswered}`, p.missingComments > 0 && `нужен пример: ${p.missingComments}`]
                      .filter(Boolean)
                      .join(', ')}
                  </span>
                </button>
              ))}
            </div>
          )}
          {submit.isError && !(submit.error instanceof ApiError && submit.error.problem?.errors) && (
            <p className="text-destructive">{submit.error.message}</p>
          )}
        </div>
      )}

      <ConfirmDialog
        open={confirming}
        title="Отправить оценку?"
        description="После отправки изменить ответы будет нельзя."
        confirmLabel="Отправить"
        onCancel={() => setConfirming(false)}
        onConfirm={() => {
          setConfirming(false)
          submit.mutate()
        }}
      />
    </Shell>
  )
}

function SaveIndicator({ status }: { status: SaveStatus }) {
  if (status === 'idle') return null
  const content: Record<Exclude<SaveStatus, 'idle'>, ReactNode> = {
    pending: <span className="text-muted-foreground">Изменения…</span>,
    saving: (
      <span className="flex items-center gap-1 text-muted-foreground">
        <Loader2 className="size-3.5 animate-spin" /> Сохранение
      </span>
    ),
    saved: (
      <span className="flex items-center gap-1 text-muted-foreground">
        <Cloud className="size-3.5" /> Сохранено
      </span>
    ),
    error: (
      <span className="flex items-center gap-1 text-amber-700">
        <CloudOff className="size-3.5" /> Нет связи, сохранено на устройстве
      </span>
    ),
  }
  return <span className="shrink-0 text-xs">{content[status]}</span>
}

function Shell({ header, footer, children }: { header?: ReactNode; footer?: ReactNode; children: ReactNode }) {
  return (
    <div className="min-h-svh bg-muted/40">
      {header && <div className="sticky top-0 z-10 border-b bg-background/95 px-4 py-3 backdrop-blur">{<div className="mx-auto max-w-2xl">{header}</div>}</div>}
      <main className="mx-auto max-w-2xl px-4 py-4 pb-28">{children}</main>
      {footer && (
        <div className="fixed inset-x-0 bottom-0 border-t bg-background/95 px-4 py-3 backdrop-blur">
          <div className="mx-auto max-w-2xl">{footer}</div>
        </div>
      )}
    </div>
  )
}

function Screen({ icon, title, children }: { icon?: ReactNode; title?: string; children?: ReactNode }) {
  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/40 p-6">
      <div className="max-w-sm space-y-3 text-center">
        {icon && <div className="flex justify-center">{icon}</div>}
        {title && <h1 className="text-xl font-semibold">{title}</h1>}
        {children && <div className="text-muted-foreground">{children}</div>}
      </div>
    </div>
  )
}

function ThanksScreen({ already = false }: { already?: boolean }) {
  return (
    <Screen icon={<CheckCircle2 className="size-12 text-emerald-600" />} title={already ? 'Вы уже прошли этот опрос' : 'Спасибо! Оценка отправлена'}>
      {already ? 'Ваши ответы получены. Изменить их нельзя.' : 'Ваши ответы сохранены. Страницу можно закрыть.'}
    </Screen>
  )
}

