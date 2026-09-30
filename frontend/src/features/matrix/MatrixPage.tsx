import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useSessionState } from '@/features/auth/session'
import { MatrixView } from '@/features/matrix/MatrixView'
import { tracksQuery } from '@/features/matrix/queries'
import { ReferenceEditor } from '@/features/matrix/ReferenceEditor'
import { RoleRulesEditor } from '@/features/matrix/RoleRulesEditor'

export function MatrixPage() {
  const { session } = useSessionState()
  const isAdmin = session?.user.role === 'Admin'
  const tracks = useQuery(tracksQuery)
  const [trackId, setTrackId] = useState<string>()
  const selectedTrackId = trackId ?? tracks.data?.find((t) => t.isActive)?.id ?? tracks.data?.[0]?.id

  const trackSelect = tracks.data && tracks.data.length > 1 && (
    <Select value={selectedTrackId} onValueChange={setTrackId}>
      <SelectTrigger className="w-48">
        <SelectValue placeholder="Направление" />
      </SelectTrigger>
      <SelectContent>
        {tracks.data.map((t) => (
          <SelectItem key={t.id} value={t.id}>
            {t.name}
            {!t.isActive && ' (неактивно)'}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )

  const matrix = (
    <>
      {tracks.isError && <p className="text-destructive">Не удалось загрузить данные. Обновите страницу.</p>}
      {tracks.data?.length === 0 && <p className="text-muted-foreground">Направления не заведены.</p>}
      {selectedTrackId && <MatrixView key={selectedTrackId} trackId={selectedTrackId} editable={isAdmin} />}
    </>
  )

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Матрица компетенций</h1>
        {trackSelect}
      </div>

      {isAdmin ? (
        <Tabs defaultValue="matrix" className="gap-4">
          <TabsList>
            <TabsTrigger value="matrix">Матрица</TabsTrigger>
            <TabsTrigger value="rules">Правила ролей</TabsTrigger>
            <TabsTrigger value="reference">Направления и грейды</TabsTrigger>
          </TabsList>
          <TabsContent value="matrix">{matrix}</TabsContent>
          <TabsContent value="rules">
            <RoleRulesEditor />
          </TabsContent>
          <TabsContent value="reference">
            <ReferenceEditor />
          </TabsContent>
        </Tabs>
      ) : (
        matrix
      )}
    </section>
  )
}
