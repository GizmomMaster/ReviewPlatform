import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { sessionsKey } from '@/features/sessions/queries'
import { endOfDayUtc, toDateInput } from '@/lib/dates'

/** Новый дедлайн идущего или просроченного опроса. Не отправившие анкету получают письмо с новой ссылкой. */
export function ExtendDeadlineDialog({ session, onClose }: { session: Schemas['SessionDetailsDto']; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [today] = useState(() => toDateInput(new Date()))
  // По умолчанию — неделя от текущего дедлайна или от сегодня, если он уже прошёл
  const [deadline, setDeadline] = useState(() => toDateInput(new Date(Math.max(new Date(session.deadlineAtUtc).getTime(), Date.now()) + 7 * 86_400_000)))
  const pending = session.participants.filter((p) => p.status === 'Pending' || p.status === 'InProgress').length

  const extend = useMutation({
    mutationFn: () =>
      unwrap(api.POST('/api/assessment-sessions/{id}/extend', { params: { path: { id: session.id } }, body: { newDeadlineAtUtc: endOfDayUtc(deadline) } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: sessionsKey })
      toast.success('Дедлайн изменён')
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{session.status === 'Overdue' ? 'Продлить опрос' : 'Изменить дедлайн'}</DialogTitle>
          <DialogDescription>
            {pending > 0
              ? `Респонденты, не отправившие анкету (${pending}), получат письмо с новым сроком и новой ссылкой — старые ссылки перестанут работать. Сохранённые ответы не потеряются.`
              : 'Все респонденты уже отправили анкеты.'}
          </DialogDescription>
        </DialogHeader>
        <div className="grid max-w-56 gap-1.5">
          <Label htmlFor="extend-deadline">Новый дедлайн</Label>
          <Input id="extend-deadline" type="date" min={today} value={deadline} onChange={(e) => setDeadline(e.target.value)} />
        </div>
        <FormError message={extend.error?.message} />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button onClick={() => extend.mutate()} disabled={!deadline || extend.isPending}>
            Сохранить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
