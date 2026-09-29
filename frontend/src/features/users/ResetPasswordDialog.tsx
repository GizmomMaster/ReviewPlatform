import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { api, unwrap, type Schemas } from '@/api/client'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { PasswordField } from '@/features/users/PasswordField'
import { generatePassword } from '@/lib/password'

interface ResetPasswordDialogProps {
  user: Schemas['UserDto']
  onClose: () => void
}

export function ResetPasswordDialog({ user, onClose }: ResetPasswordDialogProps) {
  const [password, setPassword] = useState(generatePassword)
  const reset = useMutation({
    mutationFn: () => unwrap(api.POST('/api/users/{id}/reset-password', { params: { path: { id: user.id } }, body: { newPassword: password } })),
    onSuccess: () => {
      toast.success('Пароль сброшен. Передайте пользователю временный пароль.')
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Сброс пароля</DialogTitle>
          <DialogDescription>
            {user.fullName} ({user.email}) войдёт с временным паролем и должен будет задать новый. Все текущие сеансы будут завершены.
          </DialogDescription>
        </DialogHeader>
        <PasswordField id="newPassword" label="Временный пароль" value={password} onChange={(e) => setPassword(e.target.value)} onGenerate={setPassword} />
        <FormError message={reset.error?.message} />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button onClick={() => reset.mutate()} disabled={reset.isPending || password.length === 0}>
            Сбросить пароль
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
