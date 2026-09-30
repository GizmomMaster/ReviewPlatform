import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useSessionState } from '@/features/auth/session'
import { sessionsQuery } from '@/features/sessions/queries'
import { SessionStatusBadge } from '@/features/sessions/SessionStatusBadge'
import { daysLeft, formatDate } from '@/lib/dates'
import { sessionStatusLabels, type SessionStatus } from '@/lib/labels'
import { cn } from '@/lib/utils'

const ALL = 'all'
const statuses = Object.keys(sessionStatusLabels) as SessionStatus[]

export function SessionsPage() {
  const { session: auth } = useSessionState()
  const isAdmin = auth?.user.role === 'Admin'
  const [status, setStatus] = useState<SessionStatus | typeof ALL>(ALL)
  const sessions = useQuery({ ...sessionsQuery(status === ALL ? undefined : status), placeholderData: keepPreviousData })
  const navigate = useNavigate()

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Сессии оценки</h1>
        <Button asChild>
          <Link to="/admin/sessions/new">
            <Plus /> Новая сессия
          </Link>
        </Button>
      </div>

      <Select value={status} onValueChange={(v) => setStatus(v as SessionStatus | typeof ALL)}>
        <SelectTrigger className="w-56">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>Все статусы</SelectItem>
          {statuses.map((s) => (
            <SelectItem key={s} value={s}>
              {sessionStatusLabels[s]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {sessions.isPending && <Skeleton className="h-48 w-full" />}
      {sessions.isError && <p className="text-destructive">Не удалось загрузить сессии.</p>}
      {sessions.data?.length === 0 && (
        <p className="text-muted-foreground">{status === ALL ? 'Сессий пока нет. Создайте первую.' : 'Нет сессий с таким статусом.'}</p>
      )}
      {sessions.data && sessions.data.length > 0 && (
        <div className="overflow-x-auto rounded-lg border bg-background">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Сотрудник</TableHead>
                <TableHead>Оценка</TableHead>
                <TableHead>Статус</TableHead>
                <TableHead>Прогресс</TableHead>
                <TableHead>Дедлайн</TableHead>
                {isAdmin && <TableHead>Руководитель</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {sessions.data.map((s) => {
                const active = s.status === 'InProgress' || s.status === 'Overdue'
                const left = daysLeft(s.deadlineAtUtc)
                return (
                  <TableRow key={s.id} className="cursor-pointer" onClick={() => void navigate(`/admin/sessions/${s.id}`)}>
                    <TableCell className="font-medium">{s.employeeName}</TableCell>
                    <TableCell>{s.targetGradeCode ? `${s.currentGradeCode} → ${s.targetGradeCode}` : `подтверждение ${s.currentGradeCode}`}</TableCell>
                    <TableCell>
                      <SessionStatusBadge status={s.status} />
                    </TableCell>
                    <TableCell>{s.status === 'Draft' ? '—' : `${s.participantsSubmitted} из ${s.participantsTotal}`}</TableCell>
                    <TableCell className={cn(active && left <= 3 && 'font-medium text-amber-700')}>
                      {formatDate(s.deadlineAtUtc)}
                      {active && left >= 0 && <span className="ml-1 text-xs text-muted-foreground">({left} дн.)</span>}
                    </TableCell>
                    {isAdmin && <TableCell>{s.ownerName}</TableCell>}
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </div>
      )}
    </section>
  )
}
