/**
 * Shared helpers for supplier quotes (SupplierPartQuote) on the RFQ page, /procument and
 * Email Quotes.
 */

export const QUOTE_CONDITIONS = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
export const SHOP_CONDITIONS = ['IN', 'RP', 'OH']
export const QUOTE_UNITS = [
  { value: 'EA', title: 'EA' },
  { value: 'Meter', title: 'METER' },
  { value: 'Kg', title: 'KG' },
]
export const CERT_TYPES = ['FAA', 'EASA', 'CAAC', 'Dual', 'MFG COC', 'Vendor COC', 'No Cert']

/** Optional free-text quote details (Serial Number … Warranty). Keys match the API's camelCase. */
export const QUOTE_DETAIL_FIELDS = [
  { key: 'serialNumber', label: 'Serial Number', placeholder: 'S/N', suggestions: [] as string[] },
  { key: 'traceTo', label: 'Trace To', placeholder: 'e.g. OEM, Part 121 operator', suggestions: [] as string[] },
  { key: 'tagType', label: 'Tag Type', placeholder: 'Pick or type…', suggestions: ['FAA 8130-3', 'EASA Form 1', 'Dual Release', 'CAAC AAC-038', 'MFG COC', 'Vendor COC'] },
  { key: 'priceType', label: 'Price Type', placeholder: 'Pick or type…', suggestions: ['Outright', 'Exchange', 'Repair', 'Overhaul', 'Loan', 'Flat Rate'] },
  { key: 'taggedBy', label: 'Tagged By', placeholder: 'Shop / agency', suggestions: [] as string[] },
  { key: 'moQ', label: 'MoQ', placeholder: 'Minimum order qty', suggestions: [] as string[] },
  { key: 'warranty', label: 'Warranty', placeholder: 'e.g. 6 months', suggestions: [] as string[] },
] as const

export type QuoteDetailKey = typeof QUOTE_DETAIL_FIELDS[number]['key']

/** The detail fields of a row, '' / null-safe, for a save body. `blank` is what an empty value becomes. */
export function quoteDetailsOf(q: any, blank: '' | null) {
  const out: Record<string, string | null> = {}
  for (const f of QUOTE_DETAIL_FIELDS) {
    const v = typeof q?.[f.key] === 'string' ? q[f.key].trim() : q?.[f.key]
    out[f.key] = v ? String(v) : blank
  }
  return out
}

/** Empty detail fields for a new row. */
export function emptyQuoteDetails() {
  return quoteDetailsOf(null, '')
}

/**
 * POST /rfqs/{id}/supplier-quotes body for one grid row. Shop rows (repair shops under an AR
 * quote) need their saved parent's id.
 */
export function toSupplierQuotePayload(q: any, opts: { shop?: boolean; parentProcumentId?: number | null } = {}) {
  const base = {
    id: q.id || null,
    rfqItemId: q.rfqItemId,
    supplierName: q.supplierName,
    qty: q.qty,
    price: q.price,
    alt: q.alt,
    certName: q.certName || null,
    tagDate: q.tagDate || null,
    shippingCost: q.shippingCost ?? null,
    shippingPoint: q.shippingPoint || null,
    unit: q.unit || null,
    leadTime: q.leadTime || null,
    sellLeadTime: q.sellLeadTime || '', // '' = derive from the buy lead time
    note: q.note || null,
    myNotes: q.myNotes || null,
    isCertificated: q.isCertificated || false,
    customCertReferences: q.customCertReferences || [],
    supplierEmail: q.supplierEmail || '', // '' = none
    ...quoteDetailsOf(q, ''), // '' = clear
  }
  if (opts.shop) {
    return {
      ...base,
      fixPrice: q.fixPrice ?? null,
      condition: q.condition || 'IN',
      type: 'Shop',
      parentProcumentId: opts.parentProcumentId ?? null,
    }
  }
  return {
    ...base,
    condition: q.condition,
    type: q.type || 'Procument',
    sourceStockItemId: q.sourceStockItemId ?? null,
    companyPresetId: q.companyPresetId ?? 0, // 0 = no company
  }
}

/** Grid cell text for an optional value. */
export function cellText(v: any) {
  return v === null || v === undefined || v === '' ? '—' : String(v)
}
