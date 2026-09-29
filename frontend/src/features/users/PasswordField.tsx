import { Copy, RefreshCw } from 'lucide-react'
import type { ComponentProps } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { generatePassword } from '@/lib/password'

interface PasswordFieldProps extends ComponentProps<typeof Input> {
  id: string
  label: string
  error?: string | undefined
  value: string
  onGenerate: (password: string) => void
}

/** Поле временного пароля с генерацией и копированием. */
export function PasswordField({ id, label, error, value, onGenerate, ...props }: PasswordFieldProps) {
  const copy = async () => {
    await navigator.clipboard.writeText(value)
    toast.success('Пароль скопирован')
  }

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex gap-2">
        <Input id={id} value={value} autoComplete="off" className="font-mono" aria-invalid={error ? true : undefined} {...props} />
        <Button type="button" variant="outline" size="icon" title="Сгенерировать" onClick={() => onGenerate(generatePassword())}>
          <RefreshCw />
        </Button>
        <Button type="button" variant="outline" size="icon" title="Скопировать" disabled={!value} onClick={() => void copy()}>
          <Copy />
        </Button>
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
