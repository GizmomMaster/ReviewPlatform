import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router'
import { z } from 'zod'
import { api, unwrap } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { TextField } from '@/components/form/TextField'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { passwordSchema } from '@/features/auth/password'
import { sessionStore, useSessionState } from '@/features/auth/session'
import { applyServerErrors } from '@/lib/form-errors'

const schema = z
  .object({
    currentPassword: z.string().min(1, 'Введите текущий пароль'),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: 'Пароли не совпадают' })
type ChangePasswordForm = z.infer<typeof schema>

export function ChangePasswordPage() {
  const { session } = useSessionState()
  const navigate = useNavigate()
  const form = useForm<ChangePasswordForm>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })
  const { errors, isSubmitting } = form.formState
  const forced = session?.user.mustChangePassword ?? false

  const onSubmit = form.handleSubmit(async ({ currentPassword, newPassword }) => {
    try {
      sessionStore.set(await unwrap(api.POST('/api/auth/change-password', { body: { currentPassword, newPassword } })))
      await navigate('/admin', { replace: true })
    } catch (error) {
      applyServerErrors(error, form.setError, ['currentPassword', 'newPassword'])
    }
  })

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-xl">Смена пароля</CardTitle>
          <CardDescription>
            {forced ? 'Вы вошли с временным паролем. Задайте свой, чтобы продолжить.' : 'Задайте новый пароль.'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={onSubmit} noValidate>
            <TextField
              id="currentPassword"
              label="Текущий пароль"
              type="password"
              autoComplete="current-password"
              error={errors.currentPassword?.message}
              {...form.register('currentPassword')}
            />
            <TextField
              id="newPassword"
              label="Новый пароль"
              type="password"
              autoComplete="new-password"
              error={errors.newPassword?.message}
              {...form.register('newPassword')}
            />
            <TextField
              id="confirmPassword"
              label="Повторите пароль"
              type="password"
              autoComplete="new-password"
              error={errors.confirmPassword?.message}
              {...form.register('confirmPassword')}
            />
            <p className="text-xs text-muted-foreground">Минимум 8 символов, цифра, строчная и заглавная буквы.</p>
            <FormError message={errors.root?.message} />
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Сохранение…' : 'Сохранить пароль'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
