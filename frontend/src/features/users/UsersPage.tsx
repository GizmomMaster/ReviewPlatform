import { useQuery } from '@tanstack/react-query'
import { KeyRound, MoreHorizontal, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import type { Schemas } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { usersQuery } from '@/features/users/queries'
import { ResetPasswordDialog } from '@/features/users/ResetPasswordDialog'
import { UserDialog } from '@/features/users/UserDialog'
import { roleLabels } from '@/lib/labels'

type User = Schemas['UserDto']

export function UsersPage() {
  const users = useQuery(usersQuery)
  const [editing, setEditing] = useState<{ user?: User } | null>(null)
  const [resetting, setResetting] = useState<User | null>(null)

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Пользователи</h1>
        <Button onClick={() => setEditing({})}>
          <Plus /> Добавить
        </Button>
      </div>

      {users.isPending && <Skeleton className="h-48 w-full" />}
      {users.isError && <p className="text-destructive">Не удалось загрузить пользователей.</p>}
      {users.data && (
        <div className="overflow-x-auto rounded-lg border bg-background">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ФИО</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Роль</TableHead>
                <TableHead>Статус</TableHead>
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.data.map((user) => (
                <TableRow key={user.id} className={user.isActive ? undefined : 'text-muted-foreground'}>
                  <TableCell className="font-medium">{user.fullName}</TableCell>
                  <TableCell>{user.email}</TableCell>
                  <TableCell>{roleLabels[user.role] ?? user.role}</TableCell>
                  <TableCell className="space-x-1">
                    {user.isActive ? <Badge variant="secondary">Активен</Badge> : <Badge variant="outline">Заблокирован</Badge>}
                    {user.isActive && user.mustChangePassword && <Badge variant="outline">Временный пароль</Badge>}
                  </TableCell>
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" aria-label="Действия">
                          <MoreHorizontal />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onSelect={() => setEditing({ user })}>
                          <Pencil /> Редактировать
                        </DropdownMenuItem>
                        <DropdownMenuItem onSelect={() => setResetting(user)}>
                          <KeyRound /> Сбросить пароль
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {editing && <UserDialog user={editing.user} open onOpenChange={(open) => !open && setEditing(null)} />}
      {resetting && <ResetPasswordDialog user={resetting} onClose={() => setResetting(null)} />}
    </section>
  )
}
