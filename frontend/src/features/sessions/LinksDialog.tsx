import { Copy } from 'lucide-react'
import { toast } from 'sonner'
import type { Schemas } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { evaluatorRoleLabels } from '@/lib/labels'

type Link = Schemas['ParticipantLinkDto']

async function copy(text: string, message: string) {
  await navigator.clipboard.writeText(text)
  toast.success(message)
}

/** Ссылки показываются один раз: в системе хранится только хеш токена. */
export function LinksDialog({ links, onClose }: { links: Link[]; onClose: () => void }) {
  const all = links.map((l) => `${l.fullName} (${evaluatorRoleLabels[l.role]}): ${l.url}`).join('\n')

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{links.length === 1 ? 'Ссылка на анкету' : 'Ссылки на анкеты'}</DialogTitle>
          <DialogDescription>
            Скопируйте и отправьте каждому респонденту его личную ссылку. После закрытия окна ссылку можно будет только перевыпустить — старая
            перестанет работать.
          </DialogDescription>
        </DialogHeader>
        <ul className="grid max-h-[50vh] gap-2 overflow-y-auto">
          {links.map((l) => (
            <li key={l.participantId} className="flex items-center gap-2 rounded-md border p-2">
              <div className="min-w-0 flex-1">
                <div className="text-sm font-medium">
                  {l.fullName} <span className="font-normal text-muted-foreground">· {evaluatorRoleLabels[l.role]}</span>
                </div>
                <div className="truncate font-mono text-xs text-muted-foreground">{l.url}</div>
              </div>
              <Button variant="outline" size="icon" aria-label="Скопировать ссылку" onClick={() => void copy(l.url, `Ссылка для ${l.fullName} скопирована`)}>
                <Copy />
              </Button>
            </li>
          ))}
        </ul>
        <DialogFooter>
          {links.length > 1 && (
            <Button variant="outline" onClick={() => void copy(all, 'Все ссылки скопированы')}>
              <Copy /> Скопировать все
            </Button>
          )}
          <Button onClick={onClose}>Готово</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
