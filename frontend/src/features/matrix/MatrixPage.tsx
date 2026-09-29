import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import type { Schemas } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { matrixQuery, tracksQuery } from '@/features/matrix/queries'

const ALL_GRADES = 'all'

export function MatrixPage() {
  const tracks = useQuery(tracksQuery)
  const [trackId, setTrackId] = useState<string>()
  const selectedTrackId = trackId ?? tracks.data?.[0]?.id

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Матрица компетенций</h1>
        {tracks.data && tracks.data.length > 1 && (
          <Select value={selectedTrackId} onValueChange={setTrackId}>
            <SelectTrigger className="w-48">
              <SelectValue placeholder="Направление" />
            </SelectTrigger>
            <SelectContent>
              {tracks.data.map((t) => (
                <SelectItem key={t.id} value={t.id}>
                  {t.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>

      {tracks.isError && <ErrorMessage />}
      {tracks.data?.length === 0 && <p className="text-muted-foreground">Направления не заведены.</p>}
      {selectedTrackId && <MatrixView trackId={selectedTrackId} />}
    </section>
  )
}

function MatrixView({ trackId }: { trackId: string }) {
  const matrix = useQuery(matrixQuery(trackId))
  const [gradeFilter, setGradeFilter] = useState(ALL_GRADES)

  if (matrix.isPending) return <Skeleton className="h-96 w-full" />
  if (matrix.isError) return <ErrorMessage />

  const { grades, groups } = matrix.data
  const visibleGrades = gradeFilter === ALL_GRADES ? grades : grades.filter((g) => g.id === gradeFilter)
  const total = groups.reduce((sum, g) => sum + g.indicators.length, 0)

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
      </div>

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
            {groups.map((group) => (
              <TableRow key={group.id}>
                <TableCell className="sticky left-0 z-10 bg-background align-top font-medium whitespace-normal">{group.name}</TableCell>
                {visibleGrades.map((grade) => (
                  <TableCell key={grade.id} className="align-top whitespace-normal">
                    <IndicatorList indicators={group.indicators.filter((i) => i.gradeId === grade.id)} />
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}

function IndicatorList({ indicators }: { indicators: Schemas['IndicatorDto'][] }) {
  if (indicators.length === 0) {
    return (
      <Badge variant="outline" className="text-muted-foreground">
        нет индикаторов
      </Badge>
    )
  }

  return (
    <ol className="list-decimal space-y-1.5 pl-4 text-xs leading-snug">
      {indicators.map((i) => (
        <li key={i.id}>{i.text}</li>
      ))}
    </ol>
  )
}

function ErrorMessage() {
  return <p className="text-destructive">Не удалось загрузить данные. Обновите страницу.</p>
}
