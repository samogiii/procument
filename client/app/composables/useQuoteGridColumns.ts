import { QUOTE_DETAIL_FIELDS } from '~/utils/supplierQuotes'

/**
 * Supplier-quote grids on the RFQ page and RFQ Items (/procument): by default only Sam's main columns —
 * Supplier, Condition, Alternatives, Qty, Price, Cert Type, Tagged By, Buy LeadTime, Expert (SuperAdmin).
 * The "all columns" button above the actions column shows the rest; each user's choice is remembered.
 * Every field stays editable in SupplierQuoteEditDialog either way.
 */
const STORAGE_KEY = 'quoteGrid.allColumns'
const allQuoteColumns = ref(false)
let initialised = false

/** The detail fields besides Tagged By, which is one of the main columns. */
export const OTHER_QUOTE_DETAIL_FIELDS = QUOTE_DETAIL_FIELDS.filter(f => f.key !== 'taggedBy')

export function useQuoteGridColumns() {
  if (!initialised && import.meta.client) {
    initialised = true
    try { allQuoteColumns.value = localStorage.getItem(STORAGE_KEY) === '1' } catch { /* private mode */ }
    watch(allQuoteColumns, (on) => {
      try { localStorage.setItem(STORAGE_KEY, on ? '1' : '0') } catch { /* private mode */ }
    })
  }
  return { allQuoteColumns, OTHER_QUOTE_DETAIL_FIELDS }
}
