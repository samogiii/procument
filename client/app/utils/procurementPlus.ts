import * as XLSX from 'xlsx'
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'

/** How a preferred supplier is asked for a quote (PreferredSupplierMethods on the API). */
/** How a preferred supplier is asked. WeChat is not one — it is a part track (PART_TRACKS). */
export type SourceMethod = 'Email' | 'Search' | 'ILS'

export const SOURCE_METHODS: { value: SourceMethod; title: string; icon: string; color: string; hint: string }[] = [
  { value: 'Email', title: 'Send Email', icon: 'mdi-email-fast-outline', color: 'blue', hint: 'Ask by email (request text ready to copy)' },
  { value: 'Search', title: 'Search', icon: 'mdi-web', color: 'orange', hint: 'Look the parts up on the web' },
  { value: 'ILS', title: 'ILS Quote', icon: 'mdi-airplane-search', color: 'deep-purple', hint: 'Request the quote on ILSmart' },
]

/** Where a part is sourced: uploaded ILS files, a Project (nobody lists it), or WeChat. */
export const PART_TRACKS = [
  { value: 'ILS', title: 'Parts', icon: 'mdi-airplane-search', color: 'deep-purple', hint: 'Sourced from uploaded ILS / Excel files' },
  { value: 'Project', title: 'Project', icon: 'mdi-briefcase-search-outline', color: 'teal', hint: 'Nobody lists it — worked on as a project' },
  { value: 'WeChat', title: 'WeChat', icon: 'mdi-wechat', color: 'green', hint: 'Sourced over WeChat' },
] as const

export const trackMeta = (track?: string | null) => PART_TRACKS.find(t => t.value === track) ?? PART_TRACKS[0]

/**
 * The tab a part on the Parts tab comes from (its track; "RFQ" = an item of a saved RFQ, not on the list yet).
 * Cards are tinted very lightly in that colour.
 */
export const PART_ORIGINS = [
  { value: 'parts', track: 'ILS', title: 'Parts', icon: 'mdi-airplane-search', color: 'deep-purple' },
  { value: 'project', track: 'Project', title: 'Project', icon: 'mdi-briefcase-search-outline', color: 'teal' },
  { value: 'wechat', track: 'WeChat', title: 'WeChat', icon: 'mdi-wechat', color: 'green' },
  { value: 'rfq', track: 'RFQ', title: 'RFQs', icon: 'mdi-file-document-outline', color: 'blue' },
] as const

export const originMeta = (track?: string | null) => PART_ORIGINS.find(o => o.track === track) ?? PART_ORIGINS[0]

/** Project and WeChat parts need a target price. */
export const needsTargetPrice = (track?: string | null) => track === 'Project' || track === 'WeChat'

export const CONDITIONS = ['NE', 'NS', 'OH', 'SV', 'AR', 'RP', 'FN', 'IN']

/** PpQuoteLineResponse — a supplier line under a part (the email-quote line + who/what it came from). */
export interface PpQuoteLine {
  line: any
  preferredPartId?: number
  /** Pending (being priced) or Accepted (Done). */
  status: string
  preferredId?: number
  preferredName?: string
  preferredEmails?: string[]
  sourceMethod?: string
  supplierStatus?: string
  supplierMainEmail?: string
  supplierPhone?: string
  createdByUserId?: number
  createdByName?: string
  writtenRfqs: { rfqId: number; rfqName: string; deadline: string; customerCode?: string; qty?: number; price?: number }[]
  /** Added from a row of an ILS / Excel file ("Available in your files"). */
  fromFile?: boolean
  /** An Email Quotes line (MailFlow) shown under the part, not a Procurement Plus line. */
  isEmail?: boolean
  /** The supplier also quoted this line by email (same part + condition) — the price came from that mail. */
  emailQuote?: { id: number; subject?: string; date?: string; supplierEmail?: string; price?: number; status: string } | null
  /** Copied from my own supplier quote on this RFQ (last 2 weeks) — Done puts it on the other RFQs, never back on this one. */
  fromRfq?: PpRfqRef | null
  fromRfqQuotedAt?: string | null
}

export interface PpRfqRef { rfqId: number; rfqName: string; deadline: string; customerCode?: string; qty?: number; price?: number }

export interface PpPart {
  id: number; partNumberId?: number; partNumber: string; description?: string; alternatives: string[]; ownAlternatives: string[]
  addedAt: string; hidden: boolean; track: string; qty?: number; condition?: string; targetPrice?: number
  ownerUserId?: number; ownerName?: string
  openRfqs: PpRfqRef[]; offerCount: number; preferredSupplierCount: number; otherSupplierCount: number; sourcingCount: number
  liveQuotes: { quoteId: number; source: string; supplierName?: string; price?: number; condition?: string; autoFillUntil?: string; rfqs: PpRfqRef[] }[]
  /** Average price per condition: supplier quotes on older RFQs + prices typed here. */
  priceStats: PpPriceStat[]
  /** Parts tab, not on the list yet (track "RFQ", id = -rfqItemId): the saved-RFQ item and the RFQs asking for it. */
  rfqItemId?: number
  fromRfqs: PpRfqRef[]
}

export interface PpPriceStat { condition: string; average: number; min: number; max: number; count: number; lastDate: string }

export interface PpBoardCounts {
  all: number; noSupplier: number; sourcing: number; ready: number; done: number; notInFiles: number
  /** Parts tab: parts per origin tab (parts / project / wechat / rfq). */
  origins?: Record<string, number>
}
export interface PpBoardPage { items: PpBoardPart[]; total: number; page: number; pageSize: number; counts: PpBoardCounts }

export interface PpSupplierGroup {
  key: string; name: string; preferredId?: number; supplierId?: number; sourceMethod?: string
  pendingCount: number; pricedCount: number; doneCount: number; lines: PpQuoteLine[]
}

export interface PpRfqQuote {
  supplierId?: number; supplierName: string; price: number; condition?: string; qty: number; leadTime?: string; date: string
  /** Older quotes: the RFQ it is on, and the P/N it was for (can be an alternative). */
  rfqId?: number; rfqName?: string; customerCode?: string; partNumber?: string; alt?: string; certName?: string; tagDate?: string
}
export interface PpRfqCoverageItem {
  rfqItemId: number; partNumber: string; description?: string; qty: number; condition?: string; alt?: string
  rfqQuotes: PpRfqQuote[]; olderQuotes: PpRfqQuote[]; olderQuoteCount: number; board: PpBoardPart | null; hasSupplier: boolean
  /** Not on the list: its rows in the current files (on the list they are on board.offers). */
  offers: PpOfferRow[]
}

/** Part status on a board: no supplier / being priced / ready for Done / only Done lines. */
export function partStatus(b: PpBoardPart): 'none' | 'sourcing' | 'ready' | 'done' {
  if (!b.lines.length) return 'none'
  const pending = b.lines.filter(l => l.status !== 'Accepted')
  if (!pending.length) return 'done'
  return pending.every(l => l.line.price != null && l.line.supplierId) ? 'ready' : 'sourcing'
}

export const PART_STATUS = {
  none: { title: 'No supplier', color: 'error', icon: 'mdi-help-circle-outline' },
  sourcing: { title: 'Sourcing', color: 'info', icon: 'mdi-progress-clock' },
  ready: { title: 'Ready for Done', color: 'success', icon: 'mdi-check-circle-outline' },
  done: { title: 'Done', color: 'success', icon: 'mdi-check-decagram' },
} as const

export interface PpEvent { id: number; kind: string; text: string; createdAt: string; userName?: string }
export interface PpBoardPart {
  part: PpPart; lines: PpQuoteLine[]; events: PpEvent[]
  /** Rows for the part in the owner's current files (Suppliers available). */
  offers: PpOfferRow[]
  /** Email Quotes lines (MailFlow) for the part — editable rows under it. */
  emailLines?: PpQuoteLine[]
}

const pnKey = (s?: string | null) => (s || '').replace(/\s+/g, '').toUpperCase()

/** The P/N a file row offers when it is not the part itself (an alternative), else ''. */
export const offerAlt = (row: PpOfferRow) => (pnKey(row.filePartNo) === pnKey(row.partNumber) ? '' : pnKey(row.filePartNo))

/**
 * A file row already has a supplier line under the part: same preferred / catalog supplier / company name,
 * same condition and the same P/N (each alternative a supplier lists is its own line).
 */
export function offerAdded(row: PpOfferRow, lines: PpQuoteLine[]) {
  const key = (s?: string | null) => (s || '').replace(/[^a-z0-9]/gi, '').toUpperCase()
  const cond = (s?: string | null) => (s || '').trim().toUpperCase()
  return lines.some(l => ((row.preferredId && l.preferredId === row.preferredId)
    || (row.supplierId && l.line.supplierId === row.supplierId)
    || (key(l.line.supplierNameRaw) && key(l.line.supplierNameRaw) === key(row.companyRaw)))
    && cond(l.line.condition) === cond(row.condition)
    && pnKey(l.line.alt) === offerAlt(row))
}

export function methodMeta(method?: string | null) {
  return SOURCE_METHODS.find(m => m.value === method) ?? SOURCE_METHODS[0]!
}

/** How a supplier line under a part is searched — one or more, picked when it is added (ILS first). */
export const LINE_METHODS = [
  { value: 'ILS', title: 'ILS', icon: 'mdi-airplane-search', color: 'deep-purple' },
  { value: 'Email', title: 'Send Email', icon: 'mdi-email-fast-outline', color: 'blue' },
  { value: 'Search', title: 'Search', icon: 'mdi-web', color: 'orange' },
] as const

/** A line's methods ("ILS,Email" on the API) — only the known ones, in LINE_METHODS order. */
export const lineMethods = (sourceMethod?: string | null) => {
  const set = new Set((sourceMethod || '').split(',').map(s => s.trim().toLowerCase()))
  return LINE_METHODS.filter(m => set.has(m.value.toLowerCase()))
}

/** Days uploaded files stay in the Suppliers Available tabs (ProcurementPlusService.FreshDays). */
export const PP_FRESH_DAYS = 7

export interface PpMessagePart {
  partNumber: string
  alt?: string | null
  qty?: number | null
  condition?: string | null
  description?: string | null
}

/** The request text for Send Email / WeChat — one numbered line per part. */
export function buildRequestText(supplierName: string, parts: PpMessagePart[]) {
  const lines = parts.map((p, i) => {
    const bits = [`P/N ${p.partNumber}`]
    if (p.alt) bits.push(`(alt ${p.alt})`)
    if (p.qty) bits.push(`Qty ${p.qty}`)
    if (p.condition) bits.push(`Cond ${p.condition}`)
    if (p.description) bits.push(`— ${p.description}`)
    return `${i + 1}. ${bits.join('  ')}`
  })
  return [
    `Hello ${supplierName || ''},`.trim(),
    '',
    'Could you please quote the following part(s)? Price, condition, lead time, cert and tag date please.',
    '',
    ...lines,
    '',
    'Thank you.',
  ].join('\n')
}

export const searchHref = (partNumber: string, supplierName?: string | null) =>
  `https://www.google.com/search?q=${encodeURIComponent(`"${partNumber}"${supplierName ? ` ${supplierName}` : ''}`)}`

export const ILS_HREF = 'https://www.ilsmart.com/'

/**
 * Copies text without the Clipboard API (the app runs over plain HTTP, where navigator.clipboard
 * is unavailable): a hidden textarea + execCommand('copy'). Returns false when the browser refuses.
 */
export function copyText(text: string): boolean {
  const el = document.createElement('textarea')
  el.value = text
  el.setAttribute('readonly', '')
  el.style.position = 'fixed'
  el.style.opacity = '0'
  document.body.appendChild(el)
  el.select()
  let ok = false
  try { ok = document.execCommand('copy') } catch { ok = false }
  document.body.removeChild(el)
  return ok
}

/**
 * Availability files go to the API as CSV: .csv/.txt as they are, Excel (.xlsx/.xls) converted
 * here — every sheet, one after another, so the header row is found wherever it is.
 */
export async function toCsvFile(file: File): Promise<File> {
  if (!/\.(xlsx|xlsm|xls)$/i.test(file.name)) return file
  const wb = XLSX.read(await file.arrayBuffer(), { type: 'array' })
  const csv = wb.SheetNames
    .map(name => XLSX.utils.sheet_to_csv(wb.Sheets[name]!, { blankrows: false }))
    .join('\n')
  return new File([csv], file.name.replace(/\.(xlsx|xlsm|xls)$/i, '.csv'), { type: 'text/csv' })
}

export const fmtMoney = (v: number | null | undefined) =>
  v == null ? '' : `$${Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '')

/** "3d left", "today", "2d late" against an RFQ deadline. */
export function deadlineLabel(deadline: string) {
  const day = 86_400_000
  const d = Math.round((new Date(deadline).setHours(0, 0, 0, 0) - new Date().setHours(0, 0, 0, 0)) / day)
  if (d === 0) return 'today'
  return d > 0 ? `${d}d left` : `${-d}d late`
}

// ── Custom parts pasted from Excel (Add parts dialog → Custom part → Paste from Excel) ──

export interface ImportedPart {
  partNumber: string
  description: string
  qty: number | null
  condition: string | null
  targetPrice: number | null
  alternatives: string[]
}

/** Header names per field, compared lower-cased with punctuation squeezed out. */
const PART_HEADERS: Record<keyof ImportedPart, string[]> = {
  partNumber: ['part number', 'part no', 'part num', 'partnumber', 'part #', 'p/n', 'pn', 'p n', 'mpn', 'part', 'item', 'item no', 'material'],
  description: ['description', 'desc', 'part name', 'name', 'item description'],
  qty: ['qty', 'quantity', 'qnty', 'q ty', 'quantity requested', 'req qty'],
  condition: ['condition', 'cond', 'cd', 'req condition', 'requested condition'],
  targetPrice: ['target price', 'target', 'target $', 'price', 'unit price', 'tp'],
  alternatives: ['alternatives', 'alternative', 'alternates', 'alternate', 'alt', 'alts', 'alt pn', 'alt p/n', 'alt part', 'interchangeable'],
}

/** "ALT1, ALT2; ALT3 | ALT4" → the P/Ns, without the part's own P/N and repeats. */
export function splitAlternatives(raw: string, ownPartNumber = ''): string[] {
  const key = (v: string) => v.replace(/\s+/g, '').toUpperCase()
  const seen = new Set([key(ownPartNumber)])
  return raw.split(/[,;|\n]+/).map(a => a.trim()).filter((a) => {
    const k = key(a)
    if (!k || seen.has(k)) return false
    seen.add(k)
    return true
  })
}

const normHeader = (v: unknown) => String(v ?? '').toLowerCase().replace(/[^a-z0-9#/$]+/g, ' ').trim()

function fieldOf(header: string): keyof ImportedPart | null {
  if (!header) return null
  for (const [field, names] of Object.entries(PART_HEADERS) as [keyof ImportedPart, string[]][])
    if (names.includes(header)) return field
  // "Part Number (P/N)", "Part Description", "Qty Required", "Target Price USD" … — the specific words first,
  // so "Part Description" is a description, not a part number.
  if (header.includes('desc')) return 'description'
  if (/^alt\b/.test(header) || header.includes('alternat') || header.includes('interchang')) return 'alternatives'
  if (header.includes('target')) return 'targetPrice'
  if (header.includes('cond')) return 'condition'
  if (header.includes('qty') || header.includes('quantity')) return 'qty'
  if (/^(part|p\/n|pn\b|mpn)/.test(header)) return 'partNumber'
  if (header.includes('price')) return 'targetPrice'
  return null
}

const firstNumber = (v: unknown) => {
  const m = String(v ?? '').replace(/,/g, '').match(/\d+(?:\.\d+)?/)
  return m ? Number(m[0]) : null
}

/** Splits pasted text into cells: Excel's tabs; else commas / semicolons; else 2+ spaces; else the whole line. */
function splitPasted(text: string): string[][] {
  const lines = text.split(/\r?\n/).filter(l => l.trim())
  const tabs = lines.some(l => l.includes('\t'))
  const sep = tabs ? '\t'
    : lines.filter(l => /[;,]/.test(l)).length > lines.length / 2 ? /[;,]/
      : / {2,}/
  // Tabs: keep empty leading cells (a blank first column is still a column); otherwise trim the line first.
  return lines.map(l => (tabs ? l : l.trim()).split(sep).map(c => c.trim()))
}

/**
 * Reads custom parts from rows copied out of Excel and pasted (like the RFQs page bulk import). The
 * header row is found by its names — Part Number / P/N, Description, Qty, Condition, Target price,
 * Alternatives, in any order, title lines above it skipped; without one the columns are read as Part
 * Number | Description | Qty | Condition | [Target price, when `withTarget`] | Alternatives (a single
 * column = just part numbers). Alternatives in one cell are split on , ; |. Rows without a part number
 * and repeated header rows are skipped; the same part twice is kept once.
 */
export function readPartsFromText(text: string, withTarget = true): { parts: ImportedPart[]; skipped: number; hasHeader: boolean } {
  const rows = splitPasted(text)
  if (!rows.length) return { parts: [], skipped: 0, hasHeader: false }

  // Header row: the first of the top 20 rows that names a part-number column.
  let headerIndex = -1
  let columns: Partial<Record<keyof ImportedPart, number>> = {}
  for (let i = 0; i < Math.min(rows.length, 20); i++) {
    const found: Partial<Record<keyof ImportedPart, number>> = {}
    rows[i]!.forEach((cell, c) => {
      const f = fieldOf(normHeader(cell))
      if (f && found[f] == null) found[f] = c
    })
    // A data row whose part number starts with "PN" / "PART" is not a header: the row must also name another
    // column, or say exactly "Part Number" / "P/N" …
    const exactPn = rows[i]!.some(cell => PART_HEADERS.partNumber.includes(normHeader(cell)))
    if (found.partNumber != null && (exactPn || Object.keys(found).length >= 2)) {
      headerIndex = i
      columns = found
      break
    }
  }
  if (headerIndex < 0) {
    columns = withTarget
      ? { partNumber: 0, description: 1, qty: 2, condition: 3, targetPrice: 4, alternatives: 5 }
      : { partNumber: 0, description: 1, qty: 2, condition: 3, alternatives: 4 }
  }

  const cell = (r: string[], f: keyof ImportedPart) => (columns[f] == null ? '' : (r[columns[f]!] ?? '').trim())
  const seen = new Set<string>()
  const parts: ImportedPart[] = []
  let skipped = 0
  for (const r of rows.slice(headerIndex + 1)) {
    const partNumber = cell(r, 'partNumber')
    const key = partNumber.replace(/\s+/g, '').toUpperCase()
    if (!partNumber || PART_HEADERS.partNumber.includes(normHeader(partNumber)) || seen.has(key)) {
      skipped++
      continue
    }
    seen.add(key)
    parts.push({
      partNumber,
      description: cell(r, 'description'),
      qty: firstNumber(cell(r, 'qty')),
      condition: cell(r, 'condition').toUpperCase() || null,
      targetPrice: firstNumber(cell(r, 'targetPrice')),
      alternatives: splitAlternatives(cell(r, 'alternatives'), partNumber),
    })
  }
  return { parts, skipped, hasHeader: headerIndex >= 0 }
}
