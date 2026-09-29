import { CircleCheck, CircleAlert } from 'lucide-react'
import { evaluatorRoleLabels, type EvaluatorRole } from '@/lib/labels'
import { cn } from '@/lib/utils'

export interface RoleRequirementView {
  role: EvaluatorRole
  minCount: number
  maxCount: number
  count: number
}

const range = (min: number, max: number) => (max === 0 ? 'не допускается' : min === max ? `${min}` : `${min}–${max}`)

export function RoleRequirementsList({ requirements }: { requirements: RoleRequirementView[] }) {
  return (
    <ul className="grid gap-1.5 text-sm">
      {requirements.map((r) => {
        const ok = r.count >= r.minCount && r.count <= r.maxCount
        return (
          <li key={r.role} className={cn('flex items-center gap-2', ok ? 'text-foreground' : 'text-destructive')}>
            {ok ? <CircleCheck className="size-4 text-emerald-600" /> : <CircleAlert className="size-4" />}
            <span className="w-28">{evaluatorRoleLabels[r.role]}</span>
            <span className="text-muted-foreground">
              {r.count} / нужно {range(r.minCount, r.maxCount)}
            </span>
          </li>
        )
      })}
    </ul>
  )
}
