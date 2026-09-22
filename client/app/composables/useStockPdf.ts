/**
 * Server-rendered Our Inventory PDFs (Goods Receipt Note, stock report) shown in DocPreviewModal.
 * Pages render `<DocPreviewModal>` bound to `preview` exactly as they do for other documents.
 */
export function useStockPdf() {
  const api = useApi()
  const preview = useDocPreview()
  const loading = ref<'' | 'grn' | 'report'>('')
  const error = ref('')

  async function open(kind: 'grn' | 'report', path: string, fallbackName: string) {
    loading.value = kind
    error.value = ''
    try {
      const res = await $fetch.raw<Blob>(`${api.baseURL}${path}`, {
        headers: { Authorization: `Bearer ${useAuthStore().user?.token}` },
        responseType: 'blob',
      })
      // Content-Disposition is only readable when the API exposes it (same origin); otherwise use the fallback.
      const disposition = res.headers.get('content-disposition') || ''
      const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] || fallbackName
      preview.previewBlob(res._data as Blob, decodeURIComponent(name), 'application/pdf')
    } catch (e: any) {
      let message = ''
      try { message = JSON.parse(await (e?.data as Blob)?.text?.())?.message || '' } catch {}
      error.value = message || 'Could not generate the PDF.'
    } finally {
      loading.value = ''
    }
  }

  /** GRN for a Stock PO — all receipts so far, or only the given receipt movements. */
  function openGrn(poId: number | string, movementIds: number[] = [], poNumber = '') {
    const qs = movementIds.map(id => `movementIds=${id}`).join('&')
    return open('grn', `/pdf/stock-receipt/${poId}${qs ? `?${qs}` : ''}`, `GRN-${poNumber || poId}.pdf`)
  }

  /** Stock report / valuation for the same filters as the Our Stock list. */
  function openStockReport(params: URLSearchParams, filterLabel: string) {
    const qs = new URLSearchParams(params)
    if (filterLabel) qs.set('filterLabel', filterLabel)
    return open('report', `/pdf/stock-report?${qs}`, `Stock-Report-${new Date().toISOString().slice(0, 10)}.pdf`)
  }

  return { preview, loading, error, openGrn, openStockReport }
}
