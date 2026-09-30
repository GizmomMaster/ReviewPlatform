import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Controller, useFieldArray, useForm, useWatch } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { z } from 'zod'
import { api, unwrap } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { employeesQuery } from '@/features/employees/queries'
import { gradesQuery } from '@/features/matrix/queries'
import { gradeRoleRulesQuery, sessionsKey } from '@/features/sessions/queries'
import { RoleRequirementsList, type RoleRequirementView } from '@/features/sessions/RoleRequirementsList'
import { endOfDayUtc, toDateInput } from '@/lib/dates'
import { applyServerErrors } from '@/lib/form-errors'
import { evaluatorRoleLabels, sessionTypeLabels, type EvaluatorRole } from '@/lib/labels'

const roles = ['Peer', 'TeamLead', 'Manager', 'Rck', 'ItLeader'] as const

const schema = z.object({
  employeeId: z.string().min(1, 'Выберите сотрудника'),
  type: z.enum(['Transition', 'Confirmation']),
  deadline: z.string().refine((d) => d > toDateInput(new Date()), 'Дедлайн должен быть позже сегодняшнего дня'),
  participants: z.array(
    z.object({
      fullName: z.string().trim().min(1, 'Укажите ФИО'),
      email: z.email('Некорректный email'),
      role: z.enum(roles),
    }),
  ),
})
type NewSessionForm = z.infer<typeof schema>

export function NewSessionPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const presetEmployeeId = searchParams.get('employeeId')
  const queryClient = useQueryClient()
  const employees = useQuery(employeesQuery('', false))
  const grades = useQuery(gradesQuery)
  const rules = useQuery(gradeRoleRulesQuery)
  const [dates] = useState(() => ({
    min: toDateInput(new Date(Date.now() + 86_400_000)),
    default: toDateInput(new Date(Date.now() + 14 * 86_400_000)),
  }))

  const form = useForm<NewSessionForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      employeeId: '',
      type: 'Transition',
      deadline: dates.default,
      participants: [],
    },
  })
  const { errors, isSubmitting } = form.formState
  const participants = useFieldArray({ control: form.control, name: 'participants' })
  const [employeeId, rows] = useWatch({ control: form.control, name: ['employeeId', 'participants'] })

  const employee = employees.data?.find((e) => e.id === employeeId)
  const grade = grades.data?.find((g) => g.id === employee?.gradeId)
  const nextGrade = grades.data?.find((g) => grade && g.order === grade.order + 1)
  const gradeRules = useMemo(() => (rules.data ?? []).filter((r) => r.gradeId === employee?.gradeId), [rules.data, employee?.gradeId])
  const allowedRoles = roles.filter((role) => gradeRules.some((r) => r.role === role))

  const requirements: RoleRequirementView[] = gradeRules.map((r) => ({
    role: r.role,
    minCount: r.minCount,
    maxCount: r.maxCount,
    count: r.role === 'Self' ? 1 : rows.filter((p) => p.role === r.role).length,
  }))

  const create = useMutation({
    mutationFn: (v: NewSessionForm) =>
      unwrap(
        api.POST('/api/assessment-sessions', {
          body: { employeeId: v.employeeId, type: v.type, deadlineAtUtc: endOfDayUtc(v.deadline), participants: v.participants },
        }),
      ),
    onSuccess: async (session) => {
      await queryClient.invalidateQueries({ queryKey: sessionsKey })
      await navigate(`/admin/sessions/${session.id}`)
    },
    onError: (error) => applyServerErrors(error, form.setError, ['employeeId', 'type']),
  })

  const onEmployeeChange = (id: string) => {
    form.setValue('employeeId', id, { shouldValidate: true })
    const selected = employees.data?.find((e) => e.id === id)
    const order = grades.data?.find((g) => g.id === selected?.gradeId)?.order ?? 0
    // E7 по умолчанию — подтверждение уровня (как в исходной матрице), E8 — только подтверждение
    form.setValue('type', order >= 7 ? 'Confirmation' : 'Transition')
    // Роли, недопустимые для грейда нового сотрудника, убираем
    const allowed = new Set((rules.data ?? []).filter((r) => r.gradeId === selected?.gradeId).map((r) => r.role))
    form.setValue('participants', form.getValues('participants').filter((p) => allowed.has(p.role)))
  }

  // Переход из карточки сотрудника: выбираем его, когда справочники загрузились
  const ready = !!employees.data && !!grades.data && !!rules.data
  useEffect(() => {
    if (ready && presetEmployeeId && !form.getValues('employeeId') && employees.data?.some((e) => e.id === presetEmployeeId)) {
      onEmployeeChange(presetEmployeeId)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- однократная предустановка
  }, [ready, presetEmployeeId])

  return (
    <section className="mx-auto max-w-3xl space-y-4">
      <Button asChild variant="ghost" size="sm" className="-ml-2">
        <Link to="/admin">
          <ArrowLeft /> К списку сессий
        </Link>
      </Button>
      <h1 className="text-2xl font-semibold">Новая сессия оценки</h1>

      <form className="space-y-4" noValidate onSubmit={form.handleSubmit((v) => create.mutateAsync(v).catch(() => undefined))}>
        <Card>
          <CardHeader>
            <CardTitle>Кого оцениваем</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <div className="grid gap-1.5">
              <Label>Сотрудник</Label>
              <Select value={employeeId} onValueChange={onEmployeeChange}>
                <SelectTrigger className="w-full" aria-invalid={errors.employeeId ? true : undefined}>
                  <SelectValue placeholder={employees.data?.length === 0 ? 'Сначала добавьте сотрудников' : 'Выберите сотрудника'} />
                </SelectTrigger>
                <SelectContent>
                  {employees.data?.map((e) => (
                    <SelectItem key={e.id} value={e.id}>
                      {e.fullName} · {e.gradeCode}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {errors.employeeId && <p className="text-sm text-destructive">{errors.employeeId.message}</p>}
            </div>

            {employee && grade && (
              <Controller
                control={form.control}
                name="type"
                render={({ field }) => (
                  <RadioGroup value={field.value} onValueChange={field.onChange} className="gap-3">
                    <label className="flex items-start gap-3">
                      <RadioGroupItem value="Transition" disabled={!nextGrade} className="mt-0.5" />
                      <span>
                        {sessionTypeLabels.Transition}
                        <span className="block text-sm text-muted-foreground">
                          {nextGrade ? `${grade.code} → ${nextGrade.code}: индикаторы обоих грейдов` : 'Недоступно для последнего грейда'}
                        </span>
                      </span>
                    </label>
                    <label className="flex items-start gap-3">
                      <RadioGroupItem value="Confirmation" className="mt-0.5" />
                      <span>
                        {sessionTypeLabels.Confirmation}
                        <span className="block text-sm text-muted-foreground">Только индикаторы {grade.code}</span>
                      </span>
                    </label>
                  </RadioGroup>
                )}
              />
            )}

            <div className="grid max-w-56 gap-1.5">
              <Label htmlFor="deadline">Дедлайн опроса</Label>
              <Input id="deadline" type="date" min={dates.min} {...form.register('deadline')} />
              {errors.deadline && <p className="text-sm text-destructive">{errors.deadline.message}</p>}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Респонденты</CardTitle>
            <CardDescription>Самооценка добавляется автоматически. Остальных можно добавить и позже — до запуска или во время опроса.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            {employee && (
              <div className="rounded-md bg-muted/60 px-3 py-2 text-sm">
                <span className="font-medium">{employee.fullName}</span> · {employee.email} · {evaluatorRoleLabels.Self}
              </div>
            )}

            {participants.fields.map((field, index) => (
              <div key={field.id} className="grid gap-2 sm:grid-cols-[1fr_1fr_10rem_auto] sm:items-start">
                <div>
                  <Input placeholder="ФИО" aria-invalid={errors.participants?.[index]?.fullName ? true : undefined} {...form.register(`participants.${index}.fullName`)} />
                  {errors.participants?.[index]?.fullName && <p className="text-sm text-destructive">{errors.participants[index].fullName.message}</p>}
                </div>
                <div>
                  <Input placeholder="Email" type="email" aria-invalid={errors.participants?.[index]?.email ? true : undefined} {...form.register(`participants.${index}.email`)} />
                  {errors.participants?.[index]?.email && <p className="text-sm text-destructive">{errors.participants[index].email.message}</p>}
                </div>
                <Controller
                  control={form.control}
                  name={`participants.${index}.role`}
                  render={({ field: roleField }) => (
                    <Select value={roleField.value} onValueChange={roleField.onChange}>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {allowedRoles.map((role) => (
                          <SelectItem key={role} value={role}>
                            {evaluatorRoleLabels[role]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
                <Button type="button" variant="ghost" size="icon" aria-label="Удалить" onClick={() => participants.remove(index)}>
                  <Trash2 />
                </Button>
              </div>
            ))}

            <div>
              <Button
                type="button"
                variant="outline"
                disabled={!employee}
                onClick={() => participants.append({ fullName: '', email: '', role: nextMissingRole(requirements) ?? allowedRoles[0] ?? 'Peer' })}
              >
                <Plus /> Добавить респондента
              </Button>
            </div>

            {requirements.length > 0 && (
              <div className="rounded-md border p-3">
                <p className="mb-2 text-sm font-medium">Требования для грейда {grade?.code}</p>
                <RoleRequirementsList requirements={requirements} />
                <p className="mt-2 text-xs text-muted-foreground">Черновик можно сохранить и с неполным составом — проверка выполняется при запуске.</p>
              </div>
            )}
          </CardContent>
        </Card>

        <FormError message={errors.root?.message} />
        <div className="flex justify-end gap-2">
          <Button asChild variant="outline">
            <Link to="/admin">Отмена</Link>
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Сохранение…' : 'Сохранить черновик'}
          </Button>
        </div>
      </form>
    </section>
  )
}

function nextMissingRole(requirements: RoleRequirementView[]): (typeof roles)[number] | undefined {
  const missing = requirements.find((r) => r.role !== 'Self' && r.count < r.minCount)
  return missing ? (missing.role as Exclude<EvaluatorRole, 'Self'>) : undefined
}
