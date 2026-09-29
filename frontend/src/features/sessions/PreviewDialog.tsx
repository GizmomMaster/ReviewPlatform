import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { surveyPreviewQuery } from '@/features/sessions/queries'

export function PreviewDialog({ sessionId, onClose }: { sessionId: string; onClose: () => void }) {
  const preview = useQuery(surveyPreviewQuery(sessionId))

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Предпросмотр анкеты</DialogTitle>
          <DialogDescription>
            {preview.data ? `${preview.data.indicatorCount} индикаторов в ${preview.data.groups.length} группах. ` : ''}
            Респонденты не видят, к какому грейду относится индикатор.
          </DialogDescription>
        </DialogHeader>
        {preview.isPending && <Skeleton className="h-64 w-full" />}
        {preview.data && (
          <div className="max-h-[60vh] space-y-4 overflow-y-auto pr-1">
            {preview.data.groups.map((group) => (
              <div key={group.name}>
                <h3 className="mb-1.5 font-medium">{group.name}</h3>
                <ol className="list-decimal space-y-1 pl-5 text-sm">
                  {group.indicators.map((i, index) => (
                    <li key={index}>
                      {i.text}{' '}
                      <Badge variant="outline" className="ml-1 align-middle text-[10px]">
                        {i.gradeCode}
                      </Badge>
                    </li>
                  ))}
                </ol>
              </div>
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
