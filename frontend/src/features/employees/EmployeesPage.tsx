import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, MoreHorizontal, Pencil, Plus, Search } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useSessionState } from '@/features/auth/session'
import { EmployeeDialog } from '@/features/employees/EmployeeDialog'
import { employeesKey, employeesQuery } from '@/features/employees/queries'

type Employee = Schemas['EmployeeDto']

export function EmployeesPage() {
  const { session } = useSessionState()
  const isAdmin = session?.user.role === 'Admin'
  const [search, setSearch] = useState('')
  const [includeArchived, setIncludeArchived] = useState(false)
  const deferredSearch = useDeferredValue(search.trim())
  const employees = useQuery({ ...employeesQuery(deferredSearch, includeArchived), placeholderData: keepPreviousData })
  const [editing, setEditing] = useState<{ employee?: Employee } | null>(null)

  const queryClient = useQueryClient()
  const archive = useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE('/api/employees/{id}', { params: { path: { id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: employeesKey })
      toast.success('Сотрудник перенесён в архив')
    },
    onError: (error) => toast.error(error.message),
  })

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Сотрудники</h1>
        <Button onClick={() => setEditing({})}>
          <Plus /> Добавить
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div className="relative w-full max-w-xs">
          <Search className="absolute top-2.5 left-2.5 size-4 text-muted-foreground" />
          <Input className="pl-8" placeholder="Поиск по ФИО или email" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <div className="flex items-center gap-2">
          <Switch id="archived" checked={includeArchived} onCheckedChange={setIncludeArchived} />
          <Label htmlFor="archived">Показать архивных</Label>
        </div>
      </div>

      {employees.isPending && <Skeleton className="h-48 w-full" />}
      {employees.isError && <p className="text-destructive">Не удалось загрузить сотрудников.</p>}
      {employees.data?.length === 0 && (
        <p className="text-muted-foreground">{deferredSearch ? 'Никого не нашлось.' : 'Сотрудников пока нет — добавьте первого.'}</p>
      )}
      {employees.data && employees.data.length > 0 && (
        <div className="overflow-x-auto rounded-lg border bg-background">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ФИО</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Грейд</TableHead>
                {isAdmin && <TableHead>Руководитель</TableHead>}
                <TableHead className="w-12" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {employees.data.map((employee) => (
                <TableRow key={employee.id} className={employee.isActive ? undefined : 'text-muted-foreground'}>
                  <TableCell className="font-medium">
                    {employee.fullName}
                    {!employee.isActive && (
                      <Badge variant="outline" className="ml-2">
                        архив
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>{employee.email}</TableCell>
                  <TableCell>
                    <span className="font-medium">{employee.gradeCode}</span> <span className="text-muted-foreground">{employee.gradeName}</span>
                  </TableCell>
                  {isAdmin && <TableCell>{employee.managerName}</TableCell>}
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" aria-label="Действия">
                          <MoreHorizontal />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onSelect={() => setEditing({ employee })}>
                          <Pencil /> Редактировать
                        </DropdownMenuItem>
                        {employee.isActive && (
                          <DropdownMenuItem onSelect={() => archive.mutate(employee.id)}>
                            <Archive /> В архив
                          </DropdownMenuItem>
                        )}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {editing && <EmployeeDialog employee={editing.employee} onClose={() => setEditing(null)} />}
    </section>
  )
}
