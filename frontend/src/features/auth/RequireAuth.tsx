import { useEffect, type ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { bootstrapSession, useSessionState } from '@/features/auth/session'

interface RequireAuthProps {
  children: ReactNode
  /** Страница доступна с временным паролем (смена пароля). */
  allowPasswordChange?: boolean
  adminOnly?: boolean
}

export function RequireAuth({ children, allowPasswordChange = false, adminOnly = false }: RequireAuthProps) {
  const { ready, session } = useSessionState()
  const location = useLocation()

  useEffect(() => {
    void bootstrapSession()
  }, [])

  if (!ready) return <FullPageSpinner />
  if (!session) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (session.user.mustChangePassword && !allowPasswordChange) return <Navigate to="/change-password" replace />
  if (adminOnly && session.user.role !== 'Admin') return <Navigate to="/admin" replace />
  return children
}

export function FullPageSpinner() {
  return (
    <div className="flex min-h-svh items-center justify-center">
      <div className="size-8 animate-spin rounded-full border-2 border-muted border-t-foreground" aria-label="Загрузка" />
    </div>
  )
}
