import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { sessionsKey } from '@/features/sessions/queries'
import { endOfDayUtc, toDateInput } from '@/lib/dates'
import { sessionTypeLabels, type SessionType } from '@/lib/labels'

export function EditSessionDialog({ session, onClose }: { session: Schemas['SessionDetailsDto']; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [type, setType] = useState<SessionType>(session.type)
  const [deadline, setDeadline] = useState(toDateInput(new Date(session.deadlineAtUtc)))

  const save = useMutation({
    mutationFn: () =>
      unwrap(api.PUT('/api/assessment-sessions/{id}', { params: { path: { id: session.id } }, body: { type, deadlineAtUtc: endOfDayUtc(deadline) } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: sessionsKey })
      toast.success('Сессия обновлена')
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Параметры сессии</DialogTitle>
        </DialogHeader>
        <RadioGroup value={type} onValueChange={(v) => setType(v as SessionType)} className="gap-3">
          {(['Transition', 'Confirmation'] as const).map((t) => (
            <label key={t} className="flex items-center gap-3">
              <RadioGroupItem value={t} disabled={t === 'Transition' && session.currentGrade.code === 'E8'} />
              {sessionTypeLabels[t]}
            </label>
          ))}
        </RadioGroup>
        <div className="grid max-w-56 gap-1.5">
          <Label htmlFor="edit-deadline">Дедлайн</Label>
          <Input id="edit-deadline" type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} />
        </div>
        <FormError message={save.error?.message} />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button onClick={() => save.mutate()} disabled={save.isPending}>
            Сохранить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
