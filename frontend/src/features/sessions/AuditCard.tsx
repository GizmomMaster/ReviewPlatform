import { useQuery } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { sessionsKey } from '@/features/sessions/queries'
import { formatDateTime } from '@/lib/dates'
import { auditActionLabels } from '@/lib/labels'

export function AuditCard({ sessionId }: { sessionId: string }) {
  const audit = useQuery({
    queryKey: [...sessionsKey, sessionId, 'audit'],
    queryFn: ({ signal }) => unwrap(api.GET('/api/assessment-sessions/{id}/audit', { params: { path: { id: sessionId } }, signal })),
  })
  if (!audit.data || audit.data.length === 0) return null

  return (
    <Card className="h-fit">
      <CardHeader>
        <CardTitle>История действий</CardTitle>
      </CardHeader>
      <CardContent>
        <ol className="space-y-2 text-sm">
          {audit.data.map((e, index) => (
            <li key={index}>
              <div>
                {auditActionLabels[e.action] ?? e.action}
                {e.details && <span className="text-muted-foreground"> · {e.details}</span>}
              </div>
              <div className="text-xs text-muted-foreground">
                {formatDateTime(e.occurredAtUtc)}
                {e.userName && ` · ${e.userName}`}
              </div>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  )
}
