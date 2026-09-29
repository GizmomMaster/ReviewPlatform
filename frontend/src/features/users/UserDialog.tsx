import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { TextField } from '@/components/form/TextField'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { passwordSchema } from '@/features/auth/password'
import { PasswordField } from '@/features/users/PasswordField'
import { usersQuery } from '@/features/users/queries'
import { generatePassword } from '@/lib/password'
import { applyServerErrors } from '@/lib/form-errors'
import { roleLabels, userRoles } from '@/lib/labels'

const schema = z.object({
  email: z.email('Некорректный email'),
  fullName: z.string().trim().min(1, 'Укажите ФИО').max(200),
  role: z.enum(userRoles),
  password: z.string(),
  isActive: z.boolean(),
})
type UserForm = z.infer<typeof schema>

interface UserDialogProps {
  /** undefined — создание нового пользователя */
  user?: Schemas['UserDto'] | undefined
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function UserDialog({ user, open, onOpenChange }: UserDialogProps) {
  const queryClient = useQueryClient()
  const isNew = !user
  const [initialPassword] = useState(generatePassword)
  const form = useForm<UserForm>({
    resolver: zodResolver(isNew ? schema.extend({ password: passwordSchema }) : schema),
    values: {
      email: user?.email ?? '',
      fullName: user?.fullName ?? '',
      role: user?.role === 'Admin' ? 'Admin' : 'Manager',
      password: isNew ? initialPassword : '',
      isActive: user?.isActive ?? true,
    },
  })
  const { errors, isSubmitting } = form.formState

  const save = useMutation({
    mutationFn: (values: UserForm) =>
      user
        ? unwrap(api.PUT('/api/users/{id}', { params: { path: { id: user.id } }, body: { fullName: values.fullName, role: values.role, isActive: values.isActive } }))
        : unwrap(api.POST('/api/users', { body: { email: values.email, fullName: values.fullName, role: values.role, password: values.password } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries(usersQuery)
      toast.success(isNew ? 'Пользователь создан. Передайте ему email и временный пароль.' : 'Изменения сохранены')
      onOpenChange(false)
    },
    onError: (error) => applyServerErrors(error, form.setError, ['email', 'fullName', 'role', 'password']),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isNew ? 'Новый пользователь' : 'Редактирование пользователя'}</DialogTitle>
          <DialogDescription>
            {isNew ? 'При первом входе пользователь задаст собственный пароль.' : user.email}
          </DialogDescription>
        </DialogHeader>
        <form id="user-form" className="grid gap-4" noValidate onSubmit={form.handleSubmit((v) => save.mutateAsync(v).catch(() => undefined))}>
          {isNew && <TextField id="email" label="Email" type="email" error={errors.email?.message} {...form.register('email')} />}
          <TextField id="fullName" label="ФИО" error={errors.fullName?.message} {...form.register('fullName')} />
          <div className="grid gap-1.5">
            <Label>Роль</Label>
            <Controller
              control={form.control}
              name="role"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {userRoles.map((role) => (
                      <SelectItem key={role} value={role}>
                        {roleLabels[role]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>
          {isNew && (
            <Controller
              control={form.control}
              name="password"
              render={({ field }) => (
                <PasswordField id="password" label="Временный пароль" error={errors.password?.message} {...field} onGenerate={field.onChange} />
              )}
            />
          )}
          {!isNew && (
            <Controller
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <div className="flex items-center gap-3">
                  <Switch id="isActive" checked={field.value} onCheckedChange={field.onChange} />
                  <Label htmlFor="isActive">Активен (может входить в систему)</Label>
                </div>
              )}
            />
          )}
          <FormError message={errors.root?.message} />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button type="submit" form="user-form" disabled={isSubmitting}>
            {isSubmitting ? 'Сохранение…' : 'Сохранить'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
