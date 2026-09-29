import { createBrowserRouter, Navigate } from 'react-router'
import { AdminLayout } from '@/components/layout/AdminLayout'
import { NotFoundPage } from '@/components/layout/NotFoundPage'
import { DashboardPage } from '@/features/admin/DashboardPage'
import { LoginPage } from '@/features/auth/LoginPage'
import { MatrixPage } from '@/features/matrix/MatrixPage'
import { SurveyPage } from '@/features/survey/SurveyPage'

export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/admin" replace /> },
  { path: '/login', element: <LoginPage /> },
  {
    path: '/admin',
    element: <AdminLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'matrix', element: <MatrixPage /> },
    ],
  },
  { path: '/survey/:token', element: <SurveyPage /> },
  { path: '*', element: <NotFoundPage /> },
])
