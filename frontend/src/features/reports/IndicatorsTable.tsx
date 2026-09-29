import { ChevronDown, MessageSquare } from 'lucide-react'
import { Fragment } from 'react'
import type { Schemas } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatScore } from '@/features/reports/format'
import { evaluatorRoleLabels } from '@/lib/labels'
import { cn } from '@/lib/utils'

type Report = Schemas['SessionReportDto']

interface IndicatorsTableProps {
  report: Report
  expanded: ReadonlySet<string>
  onToggle: (id: string) => void
}

const shortName = (fullName: string) => {
  const [first, last] = fullName.split(' ')
  return last ? `${first} ${last[0]}.` : fullName
}

/** Детализация: итог, самооценка и оценка каждого респондента поимённо; по клику — комментарии. */
export function IndicatorsTable({ report, expanded, onToggle }: IndicatorsTableProps) {
  const raters = report.raters.filter((r) => r.status === 'Submitted' && r.role !== 'Self')
  const self = report.raters.find((r) => r.role === 'Self')
  const byId = new Map(report.raters.map((r) => [r.id, r]))
  const groups = [...new Set(report.indicators.map((i) => i.groupName))]
  const columns = 4 + raters.length

  return (
    <div className="overflow-x-auto rounded-xl border bg-background">
      <Table className="text-sm">
        <TableHeader>
          <TableRow>
            <TableHead className="min-w-80">Индикатор</TableHead>
            <TableHead className="text-right">Итог</TableHead>
            <TableHead className="text-right">Самооценка</TableHead>
            {raters.map((r) => (
              <TableHead key={r.id} className="text-right" title={r.fullName}>
                <div className="leading-tight">{shortName(r.fullName)}</div>
                <div className="text-[11px] font-normal text-muted-foreground">{evaluatorRoleLabels[r.role]}</div>
              </TableHead>
            ))}
            <TableHead className="w-8" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {groups.map((group) => (
            <Fragment key={group}>
              <TableRow className="bg-muted/50 hover:bg-muted/50">
                <TableCell colSpan={columns} className="font-medium">
                  {group}
                </TableCell>
              </TableRow>
              {report.indicators
                .filter((i) => i.groupName === group)
                .map((i) => {
                  const ratings = new Map(i.ratings.map((r) => [r.raterId, r]))
                  const selfRating = self ? ratings.get(self.id) : undefined
                  const comments = i.ratings.filter((r) => r.comment)
                  const open = expanded.has(i.id)
                  return (
                    <Fragment key={i.id}>
                      <TableRow id={`row-${i.id}`} className={cn('cursor-pointer align-top', open && 'bg-muted/30')} onClick={() => onToggle(i.id)}>
                        <TableCell className="whitespace-normal">
                          <Badge variant="outline" className="mr-1.5 align-middle text-[10px]">
                            {i.gradeCode}
                          </Badge>
                          {i.text}
                          <div className="mt-1 flex flex-wrap gap-1">
                            {i.insufficientData && <Badge variant="secondary">мало данных</Badge>}
                            {i.isDisputed && <Badge variant="secondary">спорный</Badge>}
                            {i.blindSpot !== 'None' && <Badge variant="secondary">{i.blindSpot === 'Overestimated' ? 'переоценка' : 'недооценка'}</Badge>}
                          </div>
                        </TableCell>
                        <TableCell className={cn('text-right font-medium tabular-nums', i.isMet === false && 'text-amber-700')}>{formatScore(i.score)}</TableCell>
                        <TableCell className="text-right tabular-nums">{selfRating ? ratingText(selfRating) : '—'}</TableCell>
                        {raters.map((r) => {
                          const rating = ratings.get(r.id)
                          return (
                            <TableCell key={r.id} className="text-right tabular-nums">
                              {rating ? ratingText(rating) : '—'}
                            </TableCell>
                          )
                        })}
                        <TableCell className="text-muted-foreground">
                          {comments.length > 0 ? (
                            <span className="flex items-center gap-0.5 text-xs" title="Комментарии">
                              <MessageSquare className="size-3.5" />
                              {comments.length}
                            </span>
                          ) : (
                            <ChevronDown className={cn('size-4 opacity-40 transition-transform', open && 'rotate-180')} />
                          )}
                        </TableCell>
                      </TableRow>
                      {open && (
                        <TableRow className="hover:bg-transparent">
                          <TableCell colSpan={columns} className="bg-muted/30 whitespace-normal">
                            {comments.length === 0 ? (
                              <p className="text-muted-foreground">Комментариев нет.</p>
                            ) : (
                              <ul className="space-y-2">
                                {comments.map((c) => {
                                  const rater = byId.get(c.raterId)
                                  return (
                                    <li key={c.raterId}>
                                      <span className="font-medium">{rater?.fullName}</span>{' '}
                                      <span className="text-muted-foreground">
                                        ({rater ? evaluatorRoleLabels[rater.role] : ''}, {ratingText(c)})
                                      </span>
                                      : {c.comment}
                                    </li>
                                  )
                                })}
                              </ul>
                            )}
                          </TableCell>
                        </TableRow>
                      )}
                    </Fragment>
                  )
                })}
            </Fragment>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function ratingText(r: { score: number | null; notApplicable: boolean }) {
  return r.notApplicable ? 'н/о' : r.score == null ? '—' : String(r.score)
}
