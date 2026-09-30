import { Badge } from '@/components/ui/badge'
import { sessionStatusLabels, type SessionStatus } from '@/lib/labels'
import { cn } from '@/lib/utils'

const tone: Record<SessionStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  InProgress: 'bg-blue-100 text-blue-800',
  Overdue: 'bg-amber-100 text-amber-900',
  AwaitingDecision: 'bg-violet-100 text-violet-800',
  Closed: 'bg-emerald-100 text-emerald-800',
  Cancelled: 'bg-muted text-muted-foreground line-through',
}

export function SessionStatusBadge({ status }: { status: SessionStatus }) {
  return <Badge className={cn('border-transparent', tone[status])}>{sessionStatusLabels[status]}</Badge>
}
