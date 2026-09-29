import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, ArrowLeft, BarChart3, CalendarClock, CheckCheck, Eye, Gavel, Link2, Mail, MoreHorizontal, Pencil, Play, Plus, Trash2, XCircle } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { AddParticipantDialog } from '@/features/sessions/AddParticipantDialog'
import { AuditCard } from '@/features/sessions/AuditCard'
import { DecisionCard } from '@/features/sessions/DecisionCard'
import { EditSessionDialog } from '@/features/sessions/EditSessionDialog'
import { ExtendDeadlineDialog } from '@/features/sessions/ExtendDeadlineDialog'
import { LinksDialog } from '@/features/sessions/LinksDialog'
import { PreviewDialog } from '@/features/sessions/PreviewDialog'
import { gradeRoleRulesQuery, sessionQuery, sessionsKey } from '@/features/sessions/queries'
import { RoleRequirementsList } from '@/features/sessions/RoleRequirementsList'
import { SessionStatusBadge } from '@/features/sessions/SessionStatusBadge'
import { formatDate, formatDateTime } from '@/lib/dates'
import { evaluatorRoleLabels, participantStatusLabels, sessionTypeLabels, type ParticipantStatus } from '@/lib/labels'
import { cn } from '@/lib/utils'

type Participant = Schemas['ParticipantDto']
type ParticipantLink = Schemas['ParticipantLinkDto']
type Confirm = 'launch' | 'cancel' | 'delete' | 'closeEarly' | { reissue: Participant } | { resend: Participant } | { remove: Participant }
type AssignableRole = 'Peer' | 'TeamLead' | 'Manager' | 'Rck' | 'ItLeader'

const participantTone: Record<ParticipantStatus, string> = {
  Pending: 'text-muted-foreground',
  InProgress: 'text-blue-700',
  Submitted: 'text-emerald-700',
  Removed: 'text-muted-foreground line-through',
}

export function SessionPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const session = useQuery(sessionQuery(id))
  const rules = useQuery(gradeRoleRulesQuery)
  const [links, setLinks] = useState<ParticipantLink[] | null>(null)
  const [dialog, setDialog] = useState<'preview' | 'edit' | 'extend' | 'add' | null>(null)
  const [confirm, setConfirm] = useState<Confirm | null>(null)

  const invalidate = () => queryClient.invalidateQueries({ queryKey: sessionsKey })
  const onError = (error: Error) => toast.error(error.message)
  const path = { params: { path: { id } } }

  const launch = useMutation({
    mutationFn: () => unwrap(api.POST('/api/assessment-sessions/{id}/launch', path)),
    onSuccess: async (result) => {
      await invalidate()
      toast.success('Опрос запущен, приглашения отправлены на почту')
      setLinks(result)
    },
    onError,
  })
  const cancel = useMutation({
    mutationFn: () => unwrap(api.POST('/api/assessment-sessions/{id}/cancel', path)),
    onSuccess: async () => {
      await invalidate()
      toast.success('Сессия отменена')
    },
    onError,
  })
  const remove = useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/assessment-sessions/{id}', path)),
    onSuccess: async () => {
      await invalidate()
      toast.success('Черновик удалён')
      await navigate('/admin')
    },
    onError,
  })
  const closeEarly = useMutation({
    mutationFn: () => unwrap(api.POST('/api/assessment-sessions/{id}/close-early', path)),
    onSuccess: async () => {
      await invalidate()
      toast.success('Опрос завершён, можно принимать решение')
    },
    onError,
  })
  const removeParticipant = useMutation({
    mutationFn: (participantId: string) =>
      unwrap(api.DELETE('/api/assessment-sessions/{id}/participants/{participantId}', { params: { path: { id, participantId } } })),
    onSuccess: invalidate,
    onError,
  })
  const reissue = useMutation({
    mutationFn: (participantId: string) =>
      unwrap(api.POST('/api/assessment-sessions/{id}/participants/{participantId}/reissue-link', { params: { path: { id, participantId } } })),
    onSuccess: async (link) => {
      await invalidate()
      setLinks([link])
    },
    onError,
  })
  const resend = useMutation({
    mutationFn: (participant: Participant) =>
      unwrap(
        api.POST('/api/assessment-sessions/{id}/participants/{participantId}/resend-invite', { params: { path: { id, participantId: participant.id } } }),
      ),
    onSuccess: async (_, participant) => {
      await invalidate()
      toast.success(`Приглашение отправлено: ${participant.email}`)
    },
    onError,
  })

  if (session.isPending) return <Skeleton className="h-96 w-full" />
  if (session.isError) return <p className="text-destructive">Сессия не найдена или недоступна.</p>

  const s = session.data
  const isDraft = s.status === 'Draft'
  const isRunning = s.status === 'InProgress' || s.status === 'Overdue'
  const editable = isDraft || isRunning
  const canLaunch = s.roleRequirements.every((r) => r.isSatisfied)
  const allowedRoles = (rules.data ?? [])
    .filter((r) => r.gradeId === s.currentGrade.id && r.role !== 'Self')
    .map((r) => r.role as AssignableRole)
  const missing = s.roleRequirements.find((r) => r.role !== 'Self' && r.count < r.minCount)?.role as AssignableRole | undefined
  const active = s.participants.filter((p) => p.status !== 'Removed')
  const submitted = active.filter((p) => p.status === 'Submitted').length
  const canCloseEarly = isRunning && active.some((p) => p.role !== 'Self' && p.status === 'Submitted')

  const runConfirm = () => {
    if (confirm === 'launch') launch.mutate()
    else if (confirm === 'cancel') cancel.mutate()
    else if (confirm === 'delete') remove.mutate()
    else if (confirm === 'closeEarly') closeEarly.mutate()
    else if (confirm && 'reissue' in confirm) reissue.mutate(confirm.reissue.id)
    else if (confirm && 'resend' in confirm) resend.mutate(confirm.resend)
    else if (confirm && 'remove' in confirm) removeParticipant.mutate(confirm.remove.id)
    setConfirm(null)
  }

  return (
    <section className="space-y-4">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/admin">
          <ArrowLeft /> К списку сессий
        </Link>
      </Button>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold">{s.employeeName}</h1>
            <SessionStatusBadge status={s.status} />
          </div>
          <p className="text-muted-foreground">
            {s.targetGrade
              ? `${sessionTypeLabels.Transition}: ${s.currentGrade.code} ${s.currentGrade.name} → ${s.targetGrade.code} ${s.targetGrade.name}`
              : `${sessionTypeLabels.Confirmation}: ${s.currentGrade.code} ${s.currentGrade.name}`}
          </p>
          <p className="text-sm text-muted-foreground">
            Дедлайн {formatDate(s.deadlineAtUtc)} · {s.indicatorCount} индикаторов · руководитель {s.ownerName}
            {s.launchedAtUtc && ` · запущена ${formatDateTime(s.launchedAtUtc)}`}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setDialog('preview')}>
            <Eye /> Анкета
          </Button>
          {s.launchedAtUtc && (
            <Button variant="outline" asChild>
              <Link to={`/admin/sessions/${s.id}/report`}>
                <BarChart3 /> Отчёт
              </Link>
            </Button>
          )}
          {s.status === 'AwaitingDecision' && (
            <Button asChild>
              <Link to={`/admin/sessions/${s.id}/decision`}>
                <Gavel /> Принять решение
              </Link>
            </Button>
          )}
          {isDraft && (
            <>
              <Button variant="outline" onClick={() => setDialog('edit')}>
                <Pencil /> Параметры
              </Button>
              <Button onClick={() => setConfirm('launch')} disabled={!canLaunch || launch.isPending} title={canLaunch ? undefined : 'Состав респондентов не соответствует требованиям'}>
                <Play /> Запустить
              </Button>
            </>
          )}
          {(isDraft || isRunning) && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Ещё">
                  <MoreHorizontal />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {isDraft && (
                  <DropdownMenuItem variant="destructive" onSelect={() => setConfirm('delete')}>
                    <Trash2 /> Удалить черновик
                  </DropdownMenuItem>
                )}
                {isRunning && (
                  <DropdownMenuItem onSelect={() => setDialog('extend')}>
                    <CalendarClock /> {s.status === 'Overdue' ? 'Продлить опрос' : 'Изменить дедлайн'}
                  </DropdownMenuItem>
                )}
                {canCloseEarly && (
                  <DropdownMenuItem onSelect={() => setConfirm('closeEarly')}>
                    <CheckCheck /> Завершить опрос досрочно
                  </DropdownMenuItem>
                )}
                {isRunning && (
                  <DropdownMenuItem variant="destructive" onSelect={() => setConfirm('cancel')}>
                    <XCircle /> Отменить сессию
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </div>

      {s.status === 'Overdue' && (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-amber-300 bg-amber-50 p-4 text-amber-900">
          <AlertTriangle className="size-5 shrink-0" />
          <p className="min-w-60 flex-1 text-sm">
            Дедлайн прошёл, ответы больше не принимаются. Продлите опрос — не отправившие анкету получат новые ссылки{canCloseEarly ? ', — или завершите его с имеющимися ответами' : ''}.
          </p>
          <div className="flex gap-2">
            <Button size="sm" onClick={() => setDialog('extend')}>
              <CalendarClock /> Продлить
            </Button>
            {canCloseEarly && (
              <Button size="sm" variant="outline" onClick={() => setConfirm('closeEarly')}>
                <CheckCheck /> Завершить опрос
              </Button>
            )}
          </div>
        </div>
      )}

      {s.decision && <DecisionCard decision={s.decision} currentGradeCode={s.currentGrade.code} />}

      <div className="grid gap-4 lg:grid-cols-[1fr_20rem]">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>
              Респонденты{' '}
              {!isDraft && (
                <span className="font-normal text-muted-foreground">
                  · отправили {submitted} из {active.length}
                </span>
              )}
            </CardTitle>
            {editable && (
              <Button size="sm" variant="outline" onClick={() => setDialog('add')}>
                <Plus /> Добавить
              </Button>
            )}
          </CardHeader>
          <CardContent className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Респондент</TableHead>
                  <TableHead>Роль</TableHead>
                  {!isDraft && <TableHead>Статус</TableHead>}
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {s.participants.map((p) => {
                  const canReissue = isRunning && p.status !== 'Submitted' && p.status !== 'Removed'
                  const canRemove = editable && p.role !== 'Self' && p.status !== 'Submitted' && p.status !== 'Removed'
                  return (
                    <TableRow key={p.id}>
                      <TableCell>
                        <div className={cn('font-medium', p.status === 'Removed' && 'text-muted-foreground line-through')}>{p.fullName}</div>
                        <div className="text-xs text-muted-foreground">{p.email}</div>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{evaluatorRoleLabels[p.role]}</Badge>
                      </TableCell>
                      {!isDraft && (
                        <TableCell className={participantTone[p.status]}>
                          {participantStatusLabels[p.status]}
                          {p.submittedAtUtc && <div className="text-xs text-muted-foreground">{formatDateTime(p.submittedAtUtc)}</div>}
                        </TableCell>
                      )}
                      <TableCell>
                        {(canReissue || canRemove) && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" aria-label="Действия">
                                <MoreHorizontal />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              {canReissue && (
                                <>
                                  <DropdownMenuItem onSelect={() => setConfirm({ resend: p })}>
                                    <Mail /> Отправить приглашение повторно
                                  </DropdownMenuItem>
                                  <DropdownMenuItem onSelect={() => setConfirm({ reissue: p })}>
                                    <Link2 /> Перевыпустить ссылку
                                  </DropdownMenuItem>
                                </>
                              )}
                              {canRemove && (
                                <DropdownMenuItem variant="destructive" onSelect={() => setConfirm({ remove: p })}>
                                  <Trash2 /> Удалить из сессии
                                </DropdownMenuItem>
                              )}
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <div className="grid h-fit gap-4">
          {editable && (
            <Card className="h-fit">
              <CardHeader>
                <CardTitle>Требования к составу</CardTitle>
              </CardHeader>
              <CardContent>
                <RoleRequirementsList requirements={s.roleRequirements} />
                {isDraft && !canLaunch && <p className="mt-3 text-xs text-muted-foreground">Добавьте недостающих респондентов, чтобы запустить опрос.</p>}
              </CardContent>
            </Card>
          )}
          <AuditCard sessionId={s.id} />
        </div>
      </div>

      {dialog === 'preview' && <PreviewDialog sessionId={s.id} onClose={() => setDialog(null)} />}
      {dialog === 'edit' && <EditSessionDialog session={s} onClose={() => setDialog(null)} />}
      {dialog === 'extend' && <ExtendDeadlineDialog session={s} onClose={() => setDialog(null)} />}
      {dialog === 'add' && (
        <AddParticipantDialog
          sessionId={s.id}
          allowedRoles={allowedRoles}
          defaultRole={missing ?? allowedRoles[0] ?? 'Peer'}
          onClose={() => setDialog(null)}
          onLink={(link) => setLinks([link])}
        />
      )}
      {links && <LinksDialog links={links} onClose={() => setLinks(null)} />}

      <ConfirmDialog
        open={confirm !== null}
        onCancel={() => setConfirm(null)}
        onConfirm={runConfirm}
        {...confirmTexts(confirm)}
      />
    </section>
  )
}

function confirmTexts(confirm: Confirm | null) {
  if (confirm === 'launch')
    return {
      title: 'Запустить опрос?',
      description:
        'Анкета зафиксируется по текущей матрице, каждый респондент получит письмо с личной ссылкой. Тип оценки после запуска не меняется, дедлайн можно продлить.',
      confirmLabel: 'Запустить',
    }
  if (confirm === 'cancel')
    return { title: 'Отменить сессию?', description: 'Ссылки респондентов перестанут работать. Отмену нельзя откатить.', confirmLabel: 'Отменить сессию', destructive: true }
  if (confirm === 'closeEarly')
    return {
      title: 'Завершить опрос досрочно?',
      description: 'Не отправившие анкеты больше не смогут ответить. Отчёт построится по уже отправленным анкетам.',
      confirmLabel: 'Завершить опрос',
    }
  if (confirm === 'delete') return { title: 'Удалить черновик?', description: 'Черновик и список респондентов будут удалены.', confirmLabel: 'Удалить', destructive: true }
  if (confirm && 'reissue' in confirm)
    return {
      title: 'Перевыпустить ссылку?',
      description: `Старая ссылка ${confirm.reissue.fullName} перестанет работать, новая придёт на ${confirm.reissue.email} и будет показана здесь. Черновик ответов сохранится.`,
      confirmLabel: 'Перевыпустить',
    }
  if (confirm && 'resend' in confirm)
    return {
      title: 'Отправить приглашение повторно?',
      description: `На ${confirm.resend.email} придёт письмо с новой ссылкой, старая перестанет работать. Черновик ответов сохранится.`,
      confirmLabel: 'Отправить',
    }
  if (confirm && 'remove' in confirm)
    return { title: 'Удалить респондента?', description: `${confirm.remove.fullName} будет исключён из сессии, его ссылка перестанет работать.`, confirmLabel: 'Удалить', destructive: true }
  return { title: '', description: '', confirmLabel: '' }
}
