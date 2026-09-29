import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { gradesQuery, tracksQuery } from '@/features/matrix/queries'

/** Направления и названия грейдов. Коды (backend, E1…E8) неизменны: по ним работают импорт и шаблоны. */
export function ReferenceEditor() {
  const tracks = useQuery(tracksQuery)
  const grades = useQuery(gradesQuery)

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Направления</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3">
          {tracks.isPending && <Skeleton className="h-24 w-full" />}
          {tracks.data?.map((t) => <TrackRow key={t.id} track={t} />)}
          <NewTrackForm />
        </CardContent>
      </Card>
      <Card className="h-fit">
        <CardHeader>
          <CardTitle>Грейды</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-2">
          {grades.isPending && <Skeleton className="h-64 w-full" />}
          {grades.data?.map((g) => <GradeRow key={g.id} grade={g} />)}
        </CardContent>
      </Card>
    </div>
  )
}

function TrackRow({ track }: { track: Schemas['TrackDto'] }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(track.name)
  const save = useMutation({
    mutationFn: (isActive: boolean) => unwrap(api.PUT('/api/tracks/{id}', { params: { path: { id: track.id } }, body: { name: name.trim(), isActive } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tracksQuery.queryKey })
      toast.success('Направление сохранено')
    },
    onError: (error) => toast.error(error.message),
  })

  return (
    <div className="flex flex-wrap items-center gap-2">
      <code className="w-24 truncate text-xs text-muted-foreground" title={track.code}>
        {track.code}
      </code>
      <Input aria-label={`Название направления ${track.code}`} className="h-8 min-w-40 flex-1" value={name} maxLength={200} onChange={(e) => setName(e.target.value)} />
      <label className="flex items-center gap-2 text-sm">
        <Switch checked={track.isActive} onCheckedChange={(checked) => save.mutate(checked)} disabled={save.isPending} />
        активно
      </label>
      <Button size="sm" variant="outline" disabled={!name.trim() || name.trim() === track.name || save.isPending} onClick={() => save.mutate(track.isActive)}>
        Сохранить
      </Button>
    </div>
  )
}

function NewTrackForm() {
  const queryClient = useQueryClient()
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const create = useMutation({
    mutationFn: () => unwrap(api.POST('/api/tracks', { body: { code: code.trim(), name: name.trim() } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tracksQuery.queryKey })
      toast.success('Направление создано — матрицу можно загрузить импортом')
      setCode('')
      setName('')
    },
  })

  return (
    <form
      className="grid gap-2 border-t pt-3"
      onSubmit={(e) => {
        e.preventDefault()
        create.mutate()
      }}
    >
      <p className="text-sm font-medium">Новое направление</p>
      <div className="flex flex-wrap items-end gap-2">
        <div className="grid w-32 gap-1">
          <Label htmlFor="track-code" className="text-xs">
            Код
          </Label>
          <Input id="track-code" className="h-8" placeholder="frontend" value={code} maxLength={64} pattern="[a-zA-Z0-9-]+" onChange={(e) => setCode(e.target.value)} />
        </div>
        <div className="grid min-w-40 flex-1 gap-1">
          <Label htmlFor="track-name" className="text-xs">
            Название
          </Label>
          <Input id="track-name" className="h-8" placeholder="Frontend" value={name} maxLength={200} onChange={(e) => setName(e.target.value)} />
        </div>
        <Button size="sm" type="submit" disabled={!code.trim() || !name.trim() || create.isPending}>
          <Plus /> Создать
        </Button>
      </div>
      <FormError message={create.error?.message} />
    </form>
  )
}

function GradeRow({ grade }: { grade: Schemas['GradeDto'] }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(grade.name)
  const save = useMutation({
    mutationFn: () => unwrap(api.PUT('/api/grades/{id}', { params: { path: { id: grade.id } }, body: { name: name.trim() } })),
    onSuccess: async () => {
      // Названия грейдов показываются в матрице, сессиях и отчётах
      await queryClient.invalidateQueries()
      toast.success(`Грейд ${grade.code} переименован`)
    },
    onError: (error) => toast.error(error.message),
  })

  return (
    <div className="flex items-center gap-2">
      <span className="w-8 font-semibold">{grade.code}</span>
      <Input aria-label={`Название грейда ${grade.code}`} className="h-8 flex-1" value={name} maxLength={100} onChange={(e) => setName(e.target.value)} />
      <Button size="sm" variant="outline" disabled={!name.trim() || name.trim() === grade.name || save.isPending} onClick={() => save.mutate()}>
        Сохранить
      </Button>
    </div>
  )
}
