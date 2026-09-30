import { ChevronDown, KeyRound, LogOut } from 'lucide-react'
import { Link, NavLink, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { logout } from '@/features/auth/logout'
import { useSessionState } from '@/features/auth/session'
import { roleLabels } from '@/lib/labels'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/admin', label: 'Сессии', end: true, adminOnly: false },
  { to: '/admin/employees', label: 'Сотрудники', end: false, adminOnly: false },
  { to: '/admin/matrix', label: 'Матрица', end: false, adminOnly: false },
  { to: '/admin/users', label: 'Пользователи', end: false, adminOnly: true },
]

export function AdminLayout() {
  const { session } = useSessionState()
  const user = session?.user
  const isAdmin = user?.role === 'Admin'

  return (
    <div className="min-h-svh bg-muted/40">
      <header className="border-b bg-background">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-1 px-4 py-2 md:h-14 md:flex-nowrap md:py-0">
          <Link to="/admin" className="mr-auto font-semibold whitespace-nowrap md:mr-0">
            Оценка компетенций
          </Link>
          <nav className="order-last flex w-full gap-4 overflow-x-auto pb-1 text-sm md:order-none md:w-auto md:flex-1 md:pb-0">
            {navItems
              .filter((item) => isAdmin || !item.adminOnly)
              .map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  className={({ isActive }) =>
                    cn('whitespace-nowrap text-muted-foreground hover:text-foreground', isActive && 'font-medium text-foreground')
                  }
                >
                  {item.label}
                </NavLink>
              ))}
          </nav>
          {user && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="sm">
                  <span className="max-w-40 truncate">{user.fullName}</span>
                  <ChevronDown />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel className="font-normal">
                  <div className="text-sm font-medium">{user.fullName}</div>
                  <div className="text-xs text-muted-foreground">
                    {user.email} · {roleLabels[user.role] ?? user.role}
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link to="/change-password">
                    <KeyRound /> Сменить пароль
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => void logout()}>
                  <LogOut /> Выйти
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
