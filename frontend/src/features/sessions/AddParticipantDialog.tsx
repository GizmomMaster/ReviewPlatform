import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { TextField } from '@/components/form/TextField'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { sessionsKey } from '@/features/sessions/queries'
import { applyServerErrors } from '@/lib/form-errors'
import { evaluatorRoleLabels, type EvaluatorRole } from '@/lib/labels'

const schema = z.object({
  fullName: z.string().trim().min(1, 'Укажите ФИО'),
  email: z.email('Некорректный email'),
  role: z.enum(['Peer', 'TeamLead', 'Manager', 'Rck', 'ItLeader']),
})
type Form = z.infer<typeof schema>

interface AddParticipantDialogProps {
  sessionId: string
  allowedRoles: Form['role'][]
  defaultRole: Form['role']
  onClose: () => void
  onLink: (link: Schemas['ParticipantLinkDto']) => void
}

export function AddParticipantDialog({ sessionId, allowedRoles, defaultRole, onClose, onLink }: AddParticipantDialogProps) {
  const queryClient = useQueryClient()
  const form = useForm<Form>({ resolver: zodResolver(schema), defaultValues: { fullName: '', email: '', role: defaultRole } })
  const { errors, isSubmitting } = form.formState

  const add = useMutation({
    mutationFn: (body: Form) => unwrap(api.POST('/api/assessment-sessions/{id}/participants', { params: { path: { id: sessionId } }, body })),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: sessionsKey })
      toast.success('Респондент добавлен')
      onClose()
      if (result.link) onLink(result.link)
    },
    onError: (error) => applyServerErrors(error, form.setError, ['fullName', 'email', 'role']),
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Добавить респондента</DialogTitle>
        </DialogHeader>
        <form id="participant-form" className="grid gap-4" noValidate onSubmit={form.handleSubmit((v) => add.mutateAsync(v).catch(() => undefined))}>
          <TextField id="p-fullName" label="ФИО" error={errors.fullName?.message} {...form.register('fullName')} />
          <TextField id="p-email" label="Email" type="email" error={errors.email?.message} {...form.register('email')} />
          <div className="grid gap-1.5">
            <Label>Роль</Label>
            <Controller
              control={form.control}
              name="role"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {allowedRoles.map((role) => (
                      <SelectItem key={role} value={role}>
                        {evaluatorRoleLabels[role as EvaluatorRole]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>
          <FormError message={errors.root?.message} />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button type="submit" form="participant-form" disabled={isSubmitting}>
            Добавить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
