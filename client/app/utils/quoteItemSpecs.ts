/**
 * The small label/value pairs printed under each customer-quote item (PDF sub-row), empty ones left out.
 * Same order as the server's QuotePdfItem.Specs(): what the part is, then how it is sold.
 */
export function quoteItemSpecs(it: any): { label: string; value: string }[] {
  const tagYear = it?.tagDate ? String(it.tagDate).slice(0, 4) : ''
  return [
    { label: 'Cert', value: it?.certName },
    { label: 'Tag Date', value: tagYear },
    { label: 'Trace To', value: it?.traceTo },
    { label: 'Price Type', value: it?.priceType },
    { label: 'Warranty', value: it?.warranty },
    { label: 'CR', value: it?.cr },
  ]
    .map(s => ({ label: s.label, value: typeof s.value === 'string' ? s.value.trim() : '' }))
    .filter(s => s.value)
}

/** Detail columns for the quote Excel — only those at least one item fills, so the sheet stays tidy. */
export const QUOTE_EXCEL_DETAIL_COLUMNS = [
  { key: 'traceTo', header: 'Trace To', width: 20 },
  { key: 'priceType', header: 'Price Type', width: 14 },
  { key: 'warranty', header: 'Warranty', width: 14 },
] as const
