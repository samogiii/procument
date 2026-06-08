export function useDocPreview() {
  const authStore = useAuthStore()
  const { baseURL: apiBase } = useApi()

  const open = ref(false)
  const loading = ref(false)
  const blobUrl = ref<string | null>(null)
  const fileName = ref('')
  const mimeType = ref('')
  const downloadPath = ref('')

  function extOf(name: string) {
    return name.split('.').pop()?.toLowerCase() ?? ''
  }

  function isPreviewable(name: string, mime?: string) {
    const ext = extOf(name)
    const m = mime ?? ''
    return (
      m.startsWith('image/') ||
      m === 'application/pdf' ||
      ['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg', 'bmp', 'pdf'].includes(ext)
    )
  }

  function isPdf(name: string, mime?: string) {
    return (mime === 'application/pdf') || extOf(name) === 'pdf'
  }

  function guessType(name: string): string {
    const ext = extOf(name)
    if (ext === 'pdf') return 'application/pdf'
    if (['jpg', 'jpeg'].includes(ext)) return 'image/jpeg'
    if (ext === 'png') return 'image/png'
    if (ext === 'gif') return 'image/gif'
    if (ext === 'webp') return 'image/webp'
    if (ext === 'svg') return 'image/svg+xml'
    return 'application/octet-stream'
  }

  async function preview(apiPath: string, name: string, mime?: string) {
    if (!isPreviewable(name, mime)) {
      // Not previewable — just open as download
      downloadPath.value = apiPath
      return
    }

    loading.value = true
    fileName.value = name
    mimeType.value = mime || guessType(name)
    downloadPath.value = apiPath

    try {
      const blob = await $fetch<Blob>(`${apiBase}${apiPath}`, {
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
        responseType: 'blob',
      })
      if (blobUrl.value) URL.revokeObjectURL(blobUrl.value)
      // Force the correct MIME type so the browser renders inline
      const typed = new Blob([blob], { type: mimeType.value })
      blobUrl.value = URL.createObjectURL(typed)
      open.value = true
    } catch {
      // Fallback: open as a new tab (may trigger download)
      window.open(`${apiBase}${apiPath}`, '_blank')
    } finally {
      loading.value = false
    }
  }

  function close() {
    open.value = false
    if (blobUrl.value) {
      URL.revokeObjectURL(blobUrl.value)
      blobUrl.value = null
    }
  }

  function download() {
    if (!blobUrl.value) return
    const a = document.createElement('a')
    a.href = blobUrl.value
    a.download = fileName.value
    a.click()
  }

  // Open preview from an already-fetched Blob (for pages with custom query params)
  function previewBlob(blob: Blob, name: string, mime?: string) {
    if (blobUrl.value) URL.revokeObjectURL(blobUrl.value)
    fileName.value = name
    mimeType.value = mime || blob.type || guessType(name)
    const typed = new Blob([blob], { type: mimeType.value })
    blobUrl.value = URL.createObjectURL(typed)
    open.value = true
  }

  return { open, loading, blobUrl, fileName, mimeType, isPreviewable, isPdf, preview, previewBlob, close, download }
}
