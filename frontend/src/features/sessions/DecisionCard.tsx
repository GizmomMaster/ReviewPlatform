import type { Schemas } from '@/api/client'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDate, formatDateTime } from '@/lib/dates'
import { decisionOutcomeLabels } from '@/lib/labels'

export function DecisionCard({ decision, currentGradeCode }: { decision: Schemas['DecisionDto']; currentGradeCode: string }) {
  const outcome = decision.outcome ?? 'GradeConfirmed'
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          Решение: {decisionOutcomeLabels[outcome]}
          {decision.newGrade.code !== currentGradeCode && ` · ${currentGradeCode} → ${decision.newGrade.code} ${decision.newGrade.name}`}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {decision.decidedByName}, {formatDateTime(decision.decidedAtUtc)}
        </p>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="whitespace-pre-line">{decision.comment}</p>
        {decision.planItems.length > 0 && (
          <div>
            <p className="mb-1.5 font-medium">План развития</p>
            <ol className="list-decimal space-y-1 pl-5 text-sm">
              {decision.planItems.map((item, index) => (
                <li key={index}>
                  {item.text}
                  {item.dueDate && <span className="text-muted-foreground"> — до {formatDate(item.dueDate)}</span>}
                </li>
              ))}
            </ol>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
