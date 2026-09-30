import { useMutation } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { FormError } from '@/components/form/FormError'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

interface Field {
  label: string
  initial?: string | null | undefined
  multiline?: boolean
  optional?: boolean
  maxLength: number
}

interface TextDialogProps {
  title: string
  description?: ReactNode
  fields: Field[]
  submitLabel: string
  onSubmit: (values: string[]) => Promise<unknown>
  onClose: () => void
}

/** Диалог из одного-двух текстовых полей: названия групп, тексты индикаторов. Закрывается после успешного сохранения. */
export function TextDialog({ title, description, fields, submitLabel, onSubmit, onClose }: TextDialogProps) {
  const [values, setValues] = useState(() => fields.map((f) => f.initial ?? ''))
  const save = useMutation({ mutationFn: () => onSubmit(values.map((v) => v.trim())), onSuccess: onClose })
  const missing = fields.some((f, i) => !f.optional && values[i]!.trim() === '')

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        <form
          id="text-dialog-form"
          className="grid gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            if (!missing) save.mutate()
          }}
        >
          {fields.map((field, i) => {
            const id = `text-dialog-${i}`
            const props = {
              id,
              value: values[i],
              maxLength: field.maxLength,
              autoFocus: i === 0,
              onChange: (e: { target: { value: string } }) => setValues((prev) => prev.map((v, j) => (j === i ? e.target.value : v))),
            }
            return (
              <div key={field.label} className="grid gap-1.5">
                <Label htmlFor={id}>{field.label}</Label>
                {field.multiline ? <Textarea rows={4} {...props} /> : <Input {...props} />}
              </div>
            )
          })}
          <FormError message={save.error?.message} />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button type="submit" form="text-dialog-form" disabled={missing || save.isPending}>
            {submitLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
