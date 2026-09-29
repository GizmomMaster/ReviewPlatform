import { createBrowserRouter, Navigate } from 'react-router'
import { AdminLayout } from '@/components/layout/AdminLayout'
import { NotFoundPage } from '@/components/layout/NotFoundPage'
import { DashboardPage } from '@/features/admin/DashboardPage'
import { ChangePasswordPage } from '@/features/auth/ChangePasswordPage'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { EmployeesPage } from '@/features/employees/EmployeesPage'
import { MatrixPage } from '@/features/matrix/MatrixPage'
import { SurveyPage } from '@/features/survey/SurveyPage'
import { UsersPage } from '@/features/users/UsersPage'

export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/admin" replace /> },
  { path: '/login', element: <LoginPage /> },
  {
    path: '/change-password',
    element: (
      <RequireAuth allowPasswordChange>
        <ChangePasswordPage />
      </RequireAuth>
    ),
  },
  {
    path: '/admin',
    element: (
      <RequireAuth>
        <AdminLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'employees', element: <EmployeesPage /> },
      { path: 'matrix', element: <MatrixPage /> },
      {
        path: 'users',
        element: (
          <RequireAuth adminOnly>
            <UsersPage />
          </RequireAuth>
        ),
      },
    ],
  },
  { path: '/survey/:token', element: <SurveyPage /> },
  { path: '*', element: <NotFoundPage /> },
])
