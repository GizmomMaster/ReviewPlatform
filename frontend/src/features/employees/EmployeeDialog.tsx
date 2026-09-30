import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { TextField } from '@/components/form/TextField'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { useSessionState } from '@/features/auth/session'
import { employeesKey } from '@/features/employees/queries'
import { gradesQuery, tracksQuery } from '@/features/matrix/queries'
import { usersQuery } from '@/features/users/queries'
import { applyServerErrors } from '@/lib/form-errors'

const schema = z.object({
  fullName: z.string().trim().min(1, 'Укажите ФИО').max(200),
  email: z.email('Некорректный email'),
  trackId: z.string().min(1, 'Выберите направление'),
  gradeId: z.string().min(1, 'Выберите грейд'),
  managerUserId: z.string(),
  isActive: z.boolean(),
})
type EmployeeForm = z.infer<typeof schema>

interface EmployeeDialogProps {
  employee?: Schemas['EmployeeDto'] | undefined
  onClose: () => void
}

export function EmployeeDialog({ employee, onClose }: EmployeeDialogProps) {
  const { session } = useSessionState()
  const isAdmin = session?.user.role === 'Admin'
  const queryClient = useQueryClient()
  const tracks = useQuery(tracksQuery)
  const grades = useQuery(gradesQuery)
  const managers = useQuery({ ...usersQuery, enabled: isAdmin, select: (users) => users.filter((u) => u.isActive) })

  const form = useForm<EmployeeForm>({
    resolver: zodResolver(isAdmin ? schema.extend({ managerUserId: z.string().min(1, 'Выберите руководителя') }) : schema),
    values: {
      fullName: employee?.fullName ?? '',
      email: employee?.email ?? '',
      trackId: employee?.trackId ?? tracks.data?.[0]?.id ?? '',
      gradeId: employee?.gradeId ?? '',
      managerUserId: employee?.managerUserId ?? '',
      isActive: employee?.isActive ?? true,
    },
  })
  const { errors, isSubmitting } = form.formState

  const save = useMutation({
    mutationFn: (v: EmployeeForm) => {
      const body = { fullName: v.fullName, email: v.email, trackId: v.trackId, gradeId: v.gradeId, managerUserId: v.managerUserId || null }
      return employee
        ? unwrap(api.PUT('/api/employees/{id}', { params: { path: { id: employee.id } }, body: { ...body, isActive: v.isActive } }))
        : unwrap(api.POST('/api/employees', { body }))
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: employeesKey })
      toast.success(employee ? 'Изменения сохранены' : 'Сотрудник добавлен')
      onClose()
    },
    onError: (error) => applyServerErrors(error, form.setError, ['fullName', 'email', 'trackId', 'gradeId', 'managerUserId']),
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{employee ? 'Редактирование сотрудника' : 'Новый сотрудник'}</DialogTitle>
        </DialogHeader>
        <form id="employee-form" className="grid gap-4" noValidate onSubmit={form.handleSubmit((v) => save.mutateAsync(v).catch(() => undefined))}>
          <TextField id="fullName" label="ФИО" error={errors.fullName?.message} {...form.register('fullName')} />
          <TextField id="email" label="Email" type="email" error={errors.email?.message} {...form.register('email')} />

          {tracks.data && tracks.data.length > 1 && (
            <SelectField control={form.control} name="trackId" label="Направление" error={errors.trackId?.message}
              options={tracks.data.map((t) => ({ value: t.id, label: t.name }))} />
          )}
          <SelectField control={form.control} name="gradeId" label="Текущий грейд" error={errors.gradeId?.message}
            options={(grades.data ?? []).map((g) => ({ value: g.id, label: `${g.code} · ${g.name}` }))} />
          {isAdmin && (
            <SelectField control={form.control} name="managerUserId" label="Руководитель" error={errors.managerUserId?.message}
              options={(managers.data ?? []).map((u) => ({ value: u.id, label: u.fullName }))} />
          )}

          {employee && (
            <Controller
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <div className="flex items-center gap-3">
                  <Switch id="isActive" checked={field.value} onCheckedChange={field.onChange} />
                  <Label htmlFor="isActive">Активен (не в архиве)</Label>
                </div>
              )}
            />
          )}
          <FormError message={errors.root?.message} />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button type="submit" form="employee-form" disabled={isSubmitting}>
            {isSubmitting ? 'Сохранение…' : 'Сохранить'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface SelectFieldProps {
  control: ReturnType<typeof useForm<EmployeeForm>>['control']
  name: 'trackId' | 'gradeId' | 'managerUserId'
  label: string
  error?: string | undefined
  options: { value: string; label: string }[]
}

function SelectField({ control, name, label, error, options }: SelectFieldProps) {
  return (
    <div className="grid gap-1.5">
      <Label>{label}</Label>
      <Controller
        control={control}
        name={name}
        render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange}>
            <SelectTrigger className="w-full" aria-invalid={error ? true : undefined}>
              <SelectValue placeholder="Выберите…" />
            </SelectTrigger>
            <SelectContent>
              {options.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      />
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
