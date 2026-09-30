import { createBrowserRouter, Navigate } from 'react-router'
import { NotFoundPage } from '@/components/layout/NotFoundPage'
import { FullPageSpinner, RequireAuth } from '@/features/auth/RequireAuth'

// Страницы грузятся по требованию: анкета респондента (часто с телефона) не тянет админку и графики.
export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/admin" replace /> },
  { path: '/login', lazy: async () => ({ Component: (await import('@/features/auth/LoginPage')).LoginPage }) },
  {
    path: '/change-password',
    lazy: async () => {
      const { ChangePasswordPage } = await import('@/features/auth/ChangePasswordPage')
      return {
        Component: () => (
          <RequireAuth allowPasswordChange>
            <ChangePasswordPage />
          </RequireAuth>
        ),
      }
    },
  },
  {
    path: '/admin',
    HydrateFallback: FullPageSpinner,
    lazy: async () => {
      const { AdminLayout } = await import('@/components/layout/AdminLayout')
      return {
        Component: () => (
          <RequireAuth>
            <AdminLayout />
          </RequireAuth>
        ),
      }
    },
    children: [
      { index: true, lazy: async () => ({ Component: (await import('@/features/sessions/SessionsPage')).SessionsPage }) },
      { path: 'sessions/new', lazy: async () => ({ Component: (await import('@/features/sessions/NewSessionPage')).NewSessionPage }) },
      { path: 'sessions/:id', lazy: async () => ({ Component: (await import('@/features/sessions/SessionPage')).SessionPage }) },
      { path: 'sessions/:id/report', lazy: async () => ({ Component: (await import('@/features/reports/ReportPage')).ReportPage }) },
      { path: 'sessions/:id/decision', lazy: async () => ({ Component: (await import('@/features/sessions/DecisionPage')).DecisionPage }) },
      { path: 'employees', lazy: async () => ({ Component: (await import('@/features/employees/EmployeesPage')).EmployeesPage }) },
      { path: 'employees/:id', lazy: async () => ({ Component: (await import('@/features/employees/EmployeePage')).EmployeePage }) },
      { path: 'matrix', lazy: async () => ({ Component: (await import('@/features/matrix/MatrixPage')).MatrixPage }) },
      {
        path: 'users',
        lazy: async () => {
          const { UsersPage } = await import('@/features/users/UsersPage')
          return {
            Component: () => (
              <RequireAuth adminOnly>
                <UsersPage />
              </RequireAuth>
            ),
          }
        },
      },
    ],
  },
  { path: '/survey/:token', lazy: async () => ({ Component: (await import('@/features/survey/SurveyPage')).SurveyPage }) },
  { path: '*', element: <NotFoundPage /> },
])
