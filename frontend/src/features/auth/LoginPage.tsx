import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation } from 'react-router'
import { z } from 'zod'
import { api, unwrap } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { TextField } from '@/components/form/TextField'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { FullPageSpinner } from '@/features/auth/RequireAuth'
import { bootstrapSession, sessionStore, useSessionState } from '@/features/auth/session'
import { applyServerErrors } from '@/lib/form-errors'

const schema = z.object({
  email: z.email('Некорректный email'),
  password: z.string().min(1, 'Введите пароль'),
})
type LoginForm = z.infer<typeof schema>

export function LoginPage() {
  const { ready, session } = useSessionState()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/admin'
  const form = useForm<LoginForm>({ resolver: zodResolver(schema), defaultValues: { email: '', password: '' } })
  const { errors, isSubmitting } = form.formState

  useEffect(() => {
    void bootstrapSession()
  }, [])

  if (!ready) return <FullPageSpinner />
  if (session) return <Navigate to={session.user.mustChangePassword ? '/change-password' : from} replace />

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      sessionStore.set(await unwrap(api.POST('/api/auth/login', { body: values })))
    } catch (error) {
      applyServerErrors(error, form.setError, [])
    }
  })

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-xl">Оценка компетенций</CardTitle>
          <CardDescription>Войдите, чтобы продолжить</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={onSubmit} noValidate>
            <TextField id="email" label="Email" type="email" autoComplete="username" error={errors.email?.message} {...form.register('email')} />
            <TextField
              id="password"
              label="Пароль"
              type="password"
              autoComplete="current-password"
              error={errors.password?.message}
              {...form.register('password')}
            />
            <FormError message={errors.root?.message} />
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Вход…' : 'Войти'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
