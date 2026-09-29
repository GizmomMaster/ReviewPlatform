import type { ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface TextFieldProps extends ComponentProps<typeof Input> {
  id: string
  label: string
  error?: string | undefined
}

export function TextField({ id, label, error, ...props }: TextFieldProps) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} aria-invalid={error ? true : undefined} {...props} />
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
