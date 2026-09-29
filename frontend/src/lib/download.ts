/** Сохраняет файл из ответа API; имя — из Content-Disposition (filename*=UTF-8''…), иначе fallback. */
export function saveDownload(data: Blob, response: Response, fallbackName: string) {
  const name = /filename\*=UTF-8''([^;]+)/.exec(response.headers.get('content-disposition') ?? '')?.[1]
  const link = document.createElement('a')
  link.href = URL.createObjectURL(data)
  link.download = name ? decodeURIComponent(name) : fallbackName
  link.click()
  URL.revokeObjectURL(link.href)
}
