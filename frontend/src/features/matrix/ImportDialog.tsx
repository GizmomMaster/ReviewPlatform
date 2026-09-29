import { useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

type Preview = Schemas['MatrixImportPreviewDto']
type ChangeKind = Schemas['MatrixChange']['kind']

const changeLabels: Record<ChangeKind, { label: string; tone: string }> = {
  GroupAdded: { label: 'Новая группа', tone: 'text-emerald-700' },
  GroupChanged: { label: 'Группа изменена', tone: 'text-blue-700' },
  IndicatorAdded: { label: 'Добавлен', tone: 'text-emerald-700' },
  IndicatorChanged: { label: 'Изменён', tone: 'text-blue-700' },
  IndicatorArchived: { label: 'В архив', tone: 'text-amber-700' },
}

/** Импорт в два шага (ТЗ, 8.1.4): загрузка и предпросмотр изменений → подтверждение. */
export function ImportDialog({ trackId, trackCode, onClose }: { trackId: string; trackCode: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<Preview | null>(null)

  const check = useMutation({
    mutationFn: (selected: File) => {
      const form = new FormData()
      form.append('file', selected)
      return unwrap(
        api.POST('/api/tracks/{trackId}/matrix/import/preview', {
          params: { path: { trackId } },
          body: form as never,
          bodySerializer: (body) => body as unknown as FormData,
        }),
      )
    },
    onSuccess: setPreview,
  })
  const apply = useMutation({
    mutationFn: (importId: string) => unwrap(api.POST('/api/tracks/{trackId}/matrix/import/{importId}/apply', { params: { path: { trackId, importId } } })),
    onSuccess: async (summary) => {
      await queryClient.invalidateQueries({ queryKey: ['tracks', trackId] })
      toast.success(`Матрица обновлена: добавлено ${summary.indicatorsAdded}, изменено ${summary.indicatorsChanged}, в архив ${summary.indicatorsArchived}`)
      onClose()
    },
  })

  const s = preview?.summary
  const hasChanges = preview && preview.changes.length > 0

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Импорт матрицы из Excel</DialogTitle>
          <DialogDescription>
            Файл заменит матрицу направления «{trackCode}» целиком: индикаторы, которых нет в файле, уйдут в архив. Запущенные сессии не изменятся. Перед
            применением вы увидите все изменения.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-wrap items-end gap-3">
          <div className="grid min-w-64 flex-1 gap-1.5">
            <Label htmlFor="matrix-file">Файл .xlsx по шаблону</Label>
            <Input
              id="matrix-file"
              type="file"
              accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              onChange={(e) => {
                setFile(e.target.files?.[0] ?? null)
                setPreview(null)
                check.reset()
              }}
            />
          </div>
          <Button variant="outline" disabled={!file || check.isPending} onClick={() => file && check.mutate(file)}>
            Проверить
          </Button>
        </div>

        <FormError message={check.error?.message ?? apply.error?.message} />

        {preview && preview.errors.length > 0 && (
          <div className="grid gap-2 rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-sm">
            <p className="flex items-center gap-2 font-medium text-destructive">
              <AlertTriangle className="size-4" /> Файл не импортирован — исправьте ошибки ({preview.errors.length}):
            </p>
            <ul className="max-h-60 list-disc space-y-1 overflow-y-auto pl-5">
              {preview.errors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          </div>
        )}

        {s && preview.errors.length === 0 && (
          <div className="grid gap-3">
            <div className="flex flex-wrap gap-x-5 gap-y-1 text-sm">
              <span>Добавлено: {s.indicatorsAdded}</span>
              <span>Изменено: {s.indicatorsChanged}</span>
              <span className={cn(s.indicatorsArchived > 0 && 'font-medium text-amber-700')}>В архив: {s.indicatorsArchived}</span>
              <span className="text-muted-foreground">Без изменений: {s.indicatorsUnchanged}</span>
              {(s.groupsAdded > 0 || s.groupsChanged > 0) && (
                <span>
                  Групп новых: {s.groupsAdded}, изменённых: {s.groupsChanged}
                </span>
              )}
            </div>
            {hasChanges ? (
              <ul className="max-h-80 divide-y overflow-y-auto rounded-lg border text-sm">
                {preview.changes.map((c, index) => (
                  <li key={index} className="grid grid-cols-[8rem_1fr] gap-3 px-3 py-2">
                    <span className={cn('font-medium', changeLabels[c.kind].tone)}>{changeLabels[c.kind].label}</span>
                    <span>
                      <span className="text-muted-foreground">
                        {c.groupName}
                        {c.gradeCode && ` · ${c.gradeCode}`}
                      </span>
                      {c.text && <span className="block">{c.text}</span>}
                      {c.details && <span className="block text-xs text-muted-foreground">{c.details}</span>}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">Файл совпадает с текущей матрицей — применять нечего.</p>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button disabled={!hasChanges || !preview.importId || apply.isPending} onClick={() => preview?.importId && apply.mutate(preview.importId)}>
            Применить изменения
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
