export function buildQuotePdfPayload(quote: any, preset: any) {
  const items: any[] = [...(quote?.items || [])].sort((a: any, b: any) => {
    const aRef = typeof a.rfqReference === 'string' ? a.rfqReference : (typeof a.rfqItemId === 'number' ? a.rfqItemId.toString() : '999')
    const bRef = typeof b.rfqReference === 'string' ? b.rfqReference : (typeof b.rfqItemId === 'number' ? b.rfqItemId.toString() : '999')
    if (aRef !== bRef) return aRef.localeCompare(bRef, undefined, { numeric: true, sensitivity: 'base' })
    const aProcSo = typeof a.procumentRecordSortOrder === 'number' ? a.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
    const bProcSo = typeof b.procumentRecordSortOrder === 'number' ? b.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
    return aProcSo - bProcSo
  })

  const currencyType = quote?.customerCurrencyType || 'Dollar'
  const isYuan = quote?.customerBase === 3 && currencyType === 'Yuan'
  const currency = isYuan ? 'China Yuan (CNY)' : 'Dollar (USD)'
  const rate = isYuan ? ((quote?.coefYuan ?? 1) * (quote?.exchangeRateYuan ?? 7)) : 1
  const symbol = isYuan ? '¥' : '$'

  return {
    companyName: preset?.name || '',
    companyLocation: preset?.location || '',
    companyPhone: preset?.phone || '',
    companyWebsite: preset?.website || '',
    companyEmail: preset?.email || '',
    logoBase64: preset?.logoBase64 || null,
    primaryColor: preset?.primaryColor || '#1a2744',
    accentColor: preset?.accentColor || '#2563eb',
    quoteNumber: quote?.quoteNumber || '',
    quoteDate: quote?.createdAt ? new Date(quote.createdAt).toLocaleDateString() : '—',
    validUntil: quote?.validUntil ? new Date(quote.validUntil).toLocaleDateString() : '—',
    rfqName: quote?.rfqName || '—',
    currency,
    currencySymbol: symbol,
    exchangeRate: rate,
    customerName: quote?.customerName || '—',
    customerBillTo: quote?.customerBillTo || null,
    customerShipTo: quote?.customerShipTo || quote?.customerBillTo || null,
    items: items.map((it: any) => ({
      rfqReference: it.rfqReference || null,
      partNumberName: it.partNumberName || null,
      alt: it.alt || null,
      description: it.description || null,
      qty: it.qty || 0,
      condition: it.condition || null,
      leadTime: it.leadTime || null,
      unitPrice: Number(it.unitPrice) || 0,
      totalPrice: Number(it.totalPrice) || 0,
      certName: it.certName || null,
      tagDate: it.tagDate || null,
      note: it.note || null,
    })),
    subtotal: Number(quote?.totalAmount) || 0,
    tax: 0,
    shipping: 0,
    other: 0,
    comments: null,
    terms: quote?.customerTermsAndConditions?.trim() || preset?.termsAndConditions?.trim() || null,
    footerText: 'If you have any questions about this quotation, please contact',
  }
}

export async function fetchQuotePdfBlob(payload: any): Promise<Blob> {
  const api = useApi()
  const authStore = useAuthStore()
  return await $fetch<Blob>(`${api.baseURL}/pdf/generate`, {
    method: 'POST',
    body: payload,
    responseType: 'blob',
    headers: { Authorization: `Bearer ${authStore.user?.token}` },
  })
}

export function buildQuotePdfFileName(quote: any) {
  const safeName = (s: string) => (s || '').replace(/[/\\:*?"<>|]/g, '-').trim()
  return `${safeName(quote?.quoteNumber || 'QT')} - ${safeName(quote?.customerName || '')} - ${safeName(quote?.rfqName || '')}.pdf`
}
