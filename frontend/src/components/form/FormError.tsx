import { Alert, AlertDescription } from '@/components/ui/alert'

export function FormError({ message }: { message?: string | undefined }) {
  if (!message) return null
  return (
    <Alert variant="destructive">
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  )
}
