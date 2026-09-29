import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, ArrowDown, ArrowUp, Download, FileSpreadsheet, GripVertical, MoreHorizontal, Pencil, Plus, Trash2, Upload } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ImportDialog } from '@/features/matrix/ImportDialog'
import { matrixQuery } from '@/features/matrix/queries'
import { TextDialog } from '@/features/matrix/TextDialog'
import { shift, useSortable } from '@/features/matrix/useSortable'
import { saveDownload } from '@/lib/download'
import { cn } from '@/lib/utils'

type Matrix = Schemas['MatrixDto']
type Group = Schemas['CompetencyGroupDto']
type Indicator = Schemas['IndicatorDto']
type Grade = Schemas['GradeDto']

type Dialog =
  | { kind: 'addGroup' }
  | { kind: 'editGroup'; group: Group }
  | { kind: 'addIndicator'; group: Group; grade: Grade }
  | { kind: 'editIndicator'; indicator: Indicator }
  | { kind: 'import' }
type Confirm = { kind: 'deleteGroup'; group: Group } | { kind: 'archive'; indicator: Indicator } | { kind: 'deleteIndicator'; indicator: Indicator }

const ALL_GRADES = 'all'
const GROUP_NAME_MAX = 200
const INDICATOR_TEXT_MAX = 1000

export function MatrixView({ trackId, editable }: { trackId: string; editable: boolean }) {
  const matrix = useQuery(matrixQuery(trackId))
  const actions = useMatrixActions(trackId)
  const [gradeFilter, setGradeFilter] = useState(ALL_GRADES)
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const [confirm, setConfirm] = useState<Confirm | null>(null)
  const groupSort = useSortable(matrix.data?.groups ?? [], actions.reorderGroups)

  if (matrix.isPending) return <Skeleton className="h-96 w-full" />
  if (matrix.isError) return <p className="text-destructive">Не удалось загрузить данные. Обновите страницу.</p>

  const { grades, groups } = matrix.data
  const visibleGrades = gradeFilter === ALL_GRADES ? grades : grades.filter((g) => g.id === gradeFilter)
  const total = groups.reduce((sum, g) => sum + g.indicators.length, 0)
  const groupIds = groups.map((g) => g.id)
  const rows = editable ? groupSort.sorted : groups

  const runConfirm = () => {
    if (confirm?.kind === 'deleteGroup') actions.deleteGroup(confirm.group.id)
    else if (confirm?.kind === 'archive') actions.archiveIndicator(confirm.indicator.id)
    else if (confirm?.kind === 'deleteIndicator') actions.deleteIndicator(confirm.indicator.id)
    setConfirm(null)
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <Select value={gradeFilter} onValueChange={setGradeFilter}>
          <SelectTrigger className="w-56">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_GRADES}>Все грейды</SelectItem>
            {grades.map((g) => (
              <SelectItem key={g.id} value={g.id}>
                {g.code} · {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <span className="text-sm text-muted-foreground">
          {groups.length} групп · {total} индикаторов
        </span>
        {editable && (
          <div className="ml-auto flex flex-wrap gap-2">
            <Button size="sm" variant="outline" onClick={() => setDialog({ kind: 'addGroup' })}>
              <Plus /> Группа
            </Button>
            <Button size="sm" variant="outline" onClick={() => setDialog({ kind: 'import' })}>
              <Upload /> Импорт
            </Button>
            <Button size="sm" variant="outline" onClick={() => void actions.exportMatrix()}>
              <Download /> Экспорт
            </Button>
            <Button size="sm" variant="ghost" onClick={() => void actions.downloadTemplate()}>
              <FileSpreadsheet /> Шаблон
            </Button>
          </div>
        )}
      </div>
      {editable && (
        <p className="text-xs text-muted-foreground">
          Перетаскивайте группы и индикаторы, чтобы изменить порядок. Правки не затрагивают уже запущенные сессии — у них своя копия анкеты.
        </p>
      )}

      <div className="overflow-x-auto rounded-lg border bg-background">
        <Table className="table-fixed" style={{ minWidth: 200 + visibleGrades.length * 260 }}>
          <TableHeader>
            <TableRow>
              <TableHead className="sticky left-0 z-10 w-48 bg-background">Группа</TableHead>
              {visibleGrades.map((g) => (
                <TableHead key={g.id}>
                  <span className="font-semibold">{g.code}</span> <span className="text-muted-foreground">{g.name}</span>
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((group) => (
              <TableRow key={group.id} {...(editable ? groupSort.itemProps(group.id) : {})} className="data-dragging:opacity-50">
                <TableCell className="sticky left-0 z-10 bg-background align-top font-medium whitespace-normal">
                  <div className="flex items-start gap-1">
                    {editable && <GripVertical className="mt-0.5 size-4 shrink-0 cursor-grab text-muted-foreground" aria-hidden />}
                    <span className="flex-1">{group.name}</span>
                    {editable && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="-mt-1 size-7" aria-label={`Действия с группой ${group.name}`}>
                            <MoreHorizontal />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="start">
                          <DropdownMenuItem onSelect={() => setDialog({ kind: 'editGroup', group })}>
                            <Pencil /> Переименовать
                          </DropdownMenuItem>
                          <MoveItems ids={groupIds} id={group.id} onMove={actions.reorderGroups} />
                          <DropdownMenuSeparator />
                          <DropdownMenuItem variant="destructive" onSelect={() => setConfirm({ kind: 'deleteGroup', group })}>
                            <Trash2 /> Удалить
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </div>
                  {group.description && <p className="mt-1 text-xs font-normal text-muted-foreground">{group.description}</p>}
                </TableCell>
                {visibleGrades.map((grade) => (
                  <TableCell key={grade.id} className="align-top whitespace-normal">
                    <IndicatorCell
                      indicators={group.indicators.filter((i) => i.gradeId === grade.id)}
                      editable={editable}
                      onReorder={(ids) => actions.reorderIndicators(group.id, grade.id, ids)}
                      onAdd={() => setDialog({ kind: 'addIndicator', group, grade })}
                      onEdit={(indicator) => setDialog({ kind: 'editIndicator', indicator })}
                      onArchive={(indicator) => setConfirm({ kind: 'archive', indicator })}
                      onDelete={(indicator) => setConfirm({ kind: 'deleteIndicator', indicator })}
                    />
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {dialog?.kind === 'addGroup' && (
        <TextDialog
          title="Новая группа"
          fields={[{ label: 'Название', maxLength: GROUP_NAME_MAX }, { label: 'Описание', multiline: true, optional: true, maxLength: 2000 }]}
          submitLabel="Создать"
          onSubmit={([name, description]) => actions.createGroup(name!, description || null)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'editGroup' && (
        <TextDialog
          title="Группа"
          fields={[
            { label: 'Название', initial: dialog.group.name, maxLength: GROUP_NAME_MAX },
            { label: 'Описание', initial: dialog.group.description, multiline: true, optional: true, maxLength: 2000 },
          ]}
          submitLabel="Сохранить"
          onSubmit={([name, description]) => actions.updateGroup(dialog.group.id, name!, description || null)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'addIndicator' && (
        <TextDialog
          title={`Новый индикатор · ${dialog.grade.code}`}
          description={`Группа «${dialog.group.name}». Опишите наблюдаемое поведение — это вопрос анкеты.`}
          fields={[{ label: 'Текст', multiline: true, maxLength: INDICATOR_TEXT_MAX }]}
          submitLabel="Добавить"
          onSubmit={([text]) => actions.createIndicator(dialog.group.id, dialog.grade.id, text!)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'editIndicator' && (
        <TextDialog
          title="Индикатор"
          description="Изменение текста не затронет уже запущенные сессии."
          fields={[{ label: 'Текст', initial: dialog.indicator.text, multiline: true, maxLength: INDICATOR_TEXT_MAX }]}
          submitLabel="Сохранить"
          onSubmit={([text]) => actions.updateIndicator(dialog.indicator.id, text!)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'import' && <ImportDialog trackId={trackId} trackCode={matrix.data.track.code} onClose={() => setDialog(null)} />}

      <ConfirmDialog open={confirm !== null} onCancel={() => setConfirm(null)} onConfirm={runConfirm} {...confirmTexts(confirm)} />
    </div>
  )
}

interface IndicatorCellProps {
  indicators: Indicator[]
  editable: boolean
  onReorder: (ids: string[]) => void
  onAdd: () => void
  onEdit: (indicator: Indicator) => void
  onArchive: (indicator: Indicator) => void
  onDelete: (indicator: Indicator) => void
}

function IndicatorCell({ indicators, editable, onReorder, onAdd, onEdit, onArchive, onDelete }: IndicatorCellProps) {
  const sort = useSortable(indicators, onReorder)
  const ids = indicators.map((i) => i.id)

  if (!editable && indicators.length === 0) {
    return (
      <Badge variant="outline" className="text-muted-foreground">
        нет индикаторов
      </Badge>
    )
  }

  return (
    <div className="space-y-1.5">
      <ol className="list-decimal space-y-1.5 pl-4 text-xs leading-snug">
        {(editable ? sort.sorted : indicators).map((indicator) => (
          <li
            key={indicator.id}
            {...(editable ? sort.itemProps(indicator.id) : {})}
            className={cn(editable && 'group/indicator -mx-1 cursor-grab rounded px-1 hover:bg-muted data-dragging:opacity-50')}
          >
            <div className="flex items-start gap-1">
              <span className="flex-1">{indicator.text}</span>
              {editable && (
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="size-5 opacity-0 group-hover/indicator:opacity-100 focus-visible:opacity-100 data-[state=open]:opacity-100"
                      aria-label="Действия с индикатором"
                    >
                      <MoreHorizontal className="size-3.5" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onSelect={() => onEdit(indicator)}>
                      <Pencil /> Изменить текст
                    </DropdownMenuItem>
                    <MoveItems ids={ids} id={indicator.id} onMove={onReorder} />
                    <DropdownMenuSeparator />
                    <DropdownMenuItem onSelect={() => onArchive(indicator)}>
                      <Archive /> В архив
                    </DropdownMenuItem>
                    <DropdownMenuItem variant="destructive" onSelect={() => onDelete(indicator)}>
                      <Trash2 /> Удалить
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              )}
            </div>
          </li>
        ))}
      </ol>
      {editable && (
        <Button variant="ghost" size="sm" className="h-6 px-1.5 text-xs text-muted-foreground" onClick={onAdd}>
          <Plus className="size-3" /> Добавить
        </Button>
      )}
    </div>
  )
}

/** «Выше» / «Ниже» в меню — сортировка без мыши. */
function MoveItems({ ids, id, onMove }: { ids: string[]; id: string; onMove: (ids: string[]) => void }) {
  const up = shift(ids, id, -1)
  const down = shift(ids, id, 1)
  return (
    <>
      <DropdownMenuItem disabled={!up} onSelect={() => up && onMove(up)}>
        <ArrowUp /> Выше
      </DropdownMenuItem>
      <DropdownMenuItem disabled={!down} onSelect={() => down && onMove(down)}>
        <ArrowDown /> Ниже
      </DropdownMenuItem>
    </>
  )
}

function confirmTexts(confirm: Confirm | null) {
  if (confirm?.kind === 'deleteGroup')
    return {
      title: `Удалить группу «${confirm.group.name}»?`,
      description: 'Удалить можно только пустую группу, индикаторы которой не использовались в сессиях.',
      confirmLabel: 'Удалить',
      destructive: true,
    }
  if (confirm?.kind === 'archive')
    return {
      title: 'Отправить индикатор в архив?',
      description: 'Индикатор исчезнет из матрицы и новых анкет. Запущенные сессии и отчёты его сохранят.',
      confirmLabel: 'В архив',
    }
  if (confirm?.kind === 'deleteIndicator')
    return {
      title: 'Удалить индикатор?',
      description: 'Удалить можно только индикатор, который ещё не попадал в сессии оценки. Иначе его можно отправить в архив.',
      confirmLabel: 'Удалить',
      destructive: true,
    }
  return { title: '', description: '', confirmLabel: '' }
}

function useMatrixActions(trackId: string) {
  const queryClient = useQueryClient()
  const { queryKey } = matrixQuery(trackId)
  const invalidate = () => queryClient.invalidateQueries({ queryKey })
  const onError = (error: Error) => toast.error(error.message)
  const updateMatrix = (update: (m: Matrix) => Matrix) => queryClient.setQueryData(queryKey, (m) => (m ? update(m) : m))

  const reorderGroups = useMutation({
    mutationFn: (groupIds: string[]) => unwrap(api.PUT('/api/competency-groups/reorder', { body: { trackId, groupIds } })),
    onMutate: (ids) => updateMatrix((m) => ({ ...m, groups: ids.flatMap((id) => m.groups.find((g) => g.id === id) ?? []) })),
    onError,
    onSettled: invalidate,
  })
  const reorderIndicators = useMutation({
    mutationFn: (body: { groupId: string; gradeId: string; indicatorIds: string[] }) => unwrap(api.PUT('/api/indicators/reorder', { body })),
    onMutate: ({ groupId, gradeId, indicatorIds }) =>
      updateMatrix((m) => ({
        ...m,
        groups: m.groups.map((g) => {
          if (g.id !== groupId) return g
          const moved = indicatorIds.flatMap((id, index) => {
            const indicator = g.indicators.find((i) => i.id === id)
            return indicator ? [{ ...indicator, order: index + 1 }] : []
          })
          return { ...g, indicators: [...g.indicators.filter((i) => i.gradeId !== gradeId), ...moved] }
        }),
      })),
    onError,
    onSettled: invalidate,
  })
  const simple = useMutation({ mutationFn: (run: () => Promise<unknown>) => run(), onSuccess: invalidate, onError })

  const download = async (request: Promise<{ data?: Blob; response: Response }>, fallback: string) => {
    const { data, response } = await request
    if (!response.ok || !data) toast.error('Не удалось скачать файл')
    else saveDownload(data, response, fallback)
  }

  return {
    reorderGroups: (ids: string[]) => reorderGroups.mutate(ids),
    reorderIndicators: (groupId: string, gradeId: string, indicatorIds: string[]) => reorderIndicators.mutate({ groupId, gradeId, indicatorIds }),
    // Диалоги показывают ошибку у себя, поэтому здесь исключение пробрасывается
    createGroup: async (name: string, description: string | null) => {
      await unwrap(api.POST('/api/competency-groups', { body: { trackId, name, description } }))
      await invalidate()
      toast.success('Группа создана')
    },
    updateGroup: async (id: string, name: string, description: string | null) => {
      await unwrap(api.PUT('/api/competency-groups/{id}', { params: { path: { id } }, body: { name, description } }))
      await invalidate()
    },
    createIndicator: async (groupId: string, gradeId: string, text: string) => {
      await unwrap(api.POST('/api/indicators', { body: { groupId, gradeId, text } }))
      await invalidate()
    },
    updateIndicator: async (id: string, text: string) => {
      await unwrap(api.PUT('/api/indicators/{id}', { params: { path: { id } }, body: { text } }))
      await invalidate()
    },
    deleteGroup: (id: string) =>
      simple.mutate(() => unwrap(api.DELETE('/api/competency-groups/{id}', { params: { path: { id } } })).then(() => toast.success('Группа удалена'))),
    archiveIndicator: (id: string) =>
      simple.mutate(() => unwrap(api.POST('/api/indicators/{id}/archive', { params: { path: { id } } })).then(() => toast.success('Индикатор в архиве'))),
    deleteIndicator: (id: string) =>
      simple.mutate(() => unwrap(api.DELETE('/api/indicators/{id}', { params: { path: { id } } })).then(() => toast.success('Индикатор удалён'))),
    exportMatrix: () => download(api.GET('/api/tracks/{trackId}/matrix/export', { params: { path: { trackId } }, parseAs: 'blob' }), 'matrix.xlsx'),
    downloadTemplate: () => download(api.GET('/api/matrix/import-template', { parseAs: 'blob' }), 'template.xlsx'),
  }
}
