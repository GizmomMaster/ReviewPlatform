import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Plus } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router'
import { api, unwrap } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { employeesKey } from '@/features/employees/queries'
import { SessionStatusBadge } from '@/features/sessions/SessionStatusBadge'
import { formatDate } from '@/lib/dates'
import { decisionOutcomeLabels } from '@/lib/labels'

export function EmployeePage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const employee = useQuery({
    queryKey: [...employeesKey, id],
    queryFn: ({ signal }) => unwrap(api.GET('/api/employees/{id}', { params: { path: { id } }, signal })),
  })
  const history = useQuery({
    queryKey: [...employeesKey, id, 'history'],
    queryFn: ({ signal }) => unwrap(api.GET('/api/employees/{id}/history', { params: { path: { id } }, signal })),
  })

  if (employee.isPending) return <Skeleton className="h-64 w-full" />
  if (employee.isError) return <p className="text-destructive">Сотрудник не найден или недоступен.</p>

  const e = employee.data
  const hasActive = history.data?.some((h) => ['Draft', 'InProgress', 'Overdue', 'AwaitingDecision'].includes(h.status))

  return (
    <section className="space-y-4">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/admin/employees">
          <ArrowLeft /> К списку сотрудников
        </Link>
      </Button>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold">{e.fullName}</h1>
            {!e.isActive && <Badge variant="outline">архив</Badge>}
          </div>
          <p className="text-muted-foreground">
            {e.gradeCode} {e.gradeName} · {e.trackName} · {e.email} · руководитель {e.managerName}
          </p>
        </div>
        {e.isActive && !hasActive && (
          <Button asChild>
            <Link to={`/admin/sessions/new?employeeId=${e.id}`}>
              <Plus /> Новая сессия
            </Link>
          </Button>
        )}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>История оценок</CardTitle>
        </CardHeader>
        <CardContent className="overflow-x-auto">
          {history.data?.length === 0 && <p className="text-muted-foreground">Оценок ещё не было.</p>}
          {history.data && history.data.length > 0 && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Дата</TableHead>
                  <TableHead>Оценка</TableHead>
                  <TableHead>Статус</TableHead>
                  <TableHead>Решение</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data.map((h) => (
                  <TableRow key={h.sessionId} className="cursor-pointer" onClick={() => void navigate(`/admin/sessions/${h.sessionId}`)}>
                    <TableCell>{formatDate(h.closedAtUtc ?? h.createdAtUtc)}</TableCell>
                    <TableCell>{h.targetGradeCode ? `${h.currentGradeCode} → ${h.targetGradeCode}` : `подтверждение ${h.currentGradeCode}`}</TableCell>
                    <TableCell>
                      <SessionStatusBadge status={h.status} />
                    </TableCell>
                    <TableCell className="whitespace-normal">
                      {h.outcome ? (
                        <>
                          {decisionOutcomeLabels[h.outcome]}
                          {h.newGradeCode && h.newGradeCode !== h.currentGradeCode && ` → ${h.newGradeCode}`}
                        </>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </section>
  )
}
