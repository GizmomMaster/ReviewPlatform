import { useState, type DragEvent } from 'react'

function move(ids: string[], from: string, to: string): string[] {
  const next = ids.filter((id) => id !== from)
  next.splice(ids.indexOf(to), 0, from)
  return next
}

/**
 * Сортировка перетаскиванием на нативном HTML5 drag-and-drop. Пока элемент тащат, порядок меняется локально;
 * по отпусканию вызывается onReorder с новым порядком (если он изменился). Вложенные списки не мешают друг другу:
 * каждый реагирует только на свой перетаскиваемый элемент.
 */
export function useSortable<T extends { id: string }>(items: readonly T[], onReorder: (ids: string[]) => void) {
  const [dragging, setDragging] = useState<{ id: string; order: string[] } | null>(null)
  const byId = new Map(items.map((item) => [item.id, item]))
  const sorted = dragging ? dragging.order.flatMap((id) => byId.get(id) ?? []) : items

  const itemProps = (id: string) => ({
    draggable: true,
    'data-dragging': dragging?.id === id || undefined,
    onDragStart: (e: DragEvent) => {
      e.stopPropagation()
      e.dataTransfer.effectAllowed = 'move'
      e.dataTransfer.setData('text/plain', id)
      setDragging({ id, order: items.map((item) => item.id) })
    },
    onDragOver: (e: DragEvent) => {
      if (!dragging) return
      e.preventDefault()
      e.stopPropagation()
      if (dragging.id !== id && dragging.order.indexOf(id) !== dragging.order.indexOf(dragging.id)) {
        setDragging({ id: dragging.id, order: move(dragging.order, dragging.id, id) })
      }
    },
    onDrop: (e: DragEvent) => {
      if (dragging) e.preventDefault()
    },
    onDragEnd: (e: DragEvent) => {
      e.stopPropagation()
      if (!dragging) return
      const changed = dragging.order.some((itemId, index) => items[index]?.id !== itemId)
      setDragging(null)
      if (changed) onReorder(dragging.order)
    },
  })

  return { sorted, itemProps }
}

/** Порядок после сдвига элемента на одну позицию — для кнопок «выше/ниже» (доступно без мыши). */
export function shift(ids: string[], id: string, delta: -1 | 1): string[] | null {
  const index = ids.indexOf(id)
  const target = index + delta
  if (index < 0 || target < 0 || target >= ids.length) return null
  const next = [...ids]
  ;[next[index], next[target]] = [next[target]!, next[index]!]
  return next
}
