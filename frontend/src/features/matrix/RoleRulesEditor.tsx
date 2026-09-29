import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { gradesQuery } from '@/features/matrix/queries'
import { gradeRoleRulesQuery } from '@/features/sessions/queries'
import { evaluatorRoleLabels, type EvaluatorRole } from '@/lib/labels'

type Grade = Schemas['GradeDto']
type Rule = Schemas['GradeRoleRuleDto']
type Limits = { enabled: boolean; min: number; max: number }
type Draft = Record<string, Record<EvaluatorRole, Limits>>

const roles: EvaluatorRole[] = ['Self', 'Peer', 'TeamLead', 'Manager', 'Rck', 'ItLeader']

export function RoleRulesEditor() {
  const grades = useQuery(gradesQuery)
  const rules = useQuery(gradeRoleRulesQuery)
  if (grades.isPending || rules.isPending) return <Skeleton className="h-80 w-full" />
  if (grades.isError || rules.isError) return <p className="text-destructive">Не удалось загрузить правила.</p>
  return <RulesTable grades={grades.data} rules={rules.data} />
}

function toDraft(grades: Grade[], rules: Rule[]): Draft {
  return Object.fromEntries(
    grades.map((g) => [
      g.id,
      Object.fromEntries(
        roles.map((role) => {
          const rule = rules.find((r) => r.gradeId === g.id && r.role === role)
          return [role, rule ? { enabled: true, min: rule.minCount, max: rule.maxCount } : { enabled: false, min: 1, max: 1 }]
        }),
      ) as Record<EvaluatorRole, Limits>,
    ]),
  )
}

function RulesTable({ grades, rules }: { grades: Grade[]; rules: Rule[] }) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState(() => toDraft(grades, rules))
  const dirty = JSON.stringify(draft) !== JSON.stringify(toDraft(grades, rules))

  const save = useMutation({
    mutationFn: () =>
      unwrap(
        api.PUT('/api/grade-role-rules', {
          body: grades.flatMap((g) =>
            roles.filter((role) => draft[g.id]![role].enabled).map((role) => ({ gradeId: g.id, role, minCount: draft[g.id]![role].min, maxCount: draft[g.id]![role].max })),
          ),
        }),
      ),
    onSuccess: (saved) => {
      queryClient.setQueryData(gradeRoleRulesQuery.queryKey, saved)
      setDraft(toDraft(grades, saved))
      toast.success('Правила сохранены')
    },
  })

  const update = (gradeId: string, role: EvaluatorRole, patch: Partial<Limits>) =>
    setDraft((prev) => ({ ...prev, [gradeId]: { ...prev[gradeId]!, [role]: { ...prev[gradeId]![role], ...patch } } }))

  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Сколько оценщиков каждой роли нужно в сессии сотрудника с данным грейдом. Правила применяются к черновикам и при добавлении респондентов; уже
        запущенные опросы не пересматриваются. Самооценка всегда ровно одна.
      </p>
      <div className="overflow-x-auto rounded-lg border bg-background">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-32">Грейд</TableHead>
              {roles.map((role) => (
                <TableHead key={role}>{evaluatorRoleLabels[role]}</TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {grades.map((g) => (
              <TableRow key={g.id}>
                <TableCell>
                  <span className="font-semibold">{g.code}</span> <span className="text-muted-foreground">{g.name}</span>
                </TableCell>
                {roles.map((role) => {
                  const limits = draft[g.id]![role]
                  const fixed = role === 'Self'
                  const label = `${g.code}, ${evaluatorRoleLabels[role]}`
                  return (
                    <TableCell key={role}>
                      <div className="flex items-center gap-1.5">
                        <input
                          type="checkbox"
                          className="size-4 accent-primary"
                          aria-label={`${label}: роль допустима`}
                          checked={limits.enabled}
                          disabled={fixed}
                          onChange={(e) => update(g.id, role, { enabled: e.target.checked })}
                        />
                        {limits.enabled && (
                          <>
                            <LimitInput label={`${label}: минимум`} value={limits.min} min={0} disabled={fixed} onChange={(min) => update(g.id, role, { min })} />
                            <span className="text-muted-foreground">–</span>
                            <LimitInput label={`${label}: максимум`} value={limits.max} min={1} disabled={fixed} onChange={(max) => update(g.id, role, { max })} />
                          </>
                        )}
                      </div>
                    </TableCell>
                  )
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <FormError message={save.error?.message} />
      <div className="flex gap-2">
        <Button onClick={() => save.mutate()} disabled={!dirty || save.isPending}>
          Сохранить правила
        </Button>
        <Button variant="outline" onClick={() => setDraft(toDraft(grades, rules))} disabled={!dirty}>
          Сбросить
        </Button>
      </div>
    </div>
  )
}

function LimitInput({ label, value, min, disabled, onChange }: { label: string; value: number; min: number; disabled: boolean; onChange: (v: number) => void }) {
  return (
    <Input
      type="number"
      aria-label={label}
      className="h-7 w-14 px-1.5 text-center"
      min={min}
      max={20}
      value={value}
      disabled={disabled}
      onChange={(e) => onChange(Math.max(min, Math.trunc(Number(e.target.value) || 0)))}
    />
  )
}
