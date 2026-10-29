/**
 * New Procurement Plus page (api/ppe): parts assigned to a user (the PPE — Procurement Part Expert) from Email RFQs,
 * each across every RFQ asking for it whose deadline is at most PPE_DEADLINE_DAYS ago.
 */
import { copyText } from '~/utils/procurementPlus'
import type { PpPriceStat, PpQuoteLine } from '~/utils/procurementPlus'
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import type { PpPreferredSupplier } from '~/components/ProcurementPlusPreferredDialog.vue'

export const PPE_DEADLINE_DAYS = 14

export interface PpePartRow {
  id: number
  partNumber: string
  description?: string | null
  alternatives: string[]
  qty?: number | null
  condition?: string | null
  conditions: string[]
  avgPrice?: number | null
  /** The condition the average is for (the asked one); null = every condition. */
  avgPriceCondition?: string | null
  priceStats: PpPriceStat[]
  rfqCount: number
  nearestDeadline?: string | null
  draftCount: number
  /** Suppliers with a price (on the RFQs, or priced here and not written yet). */
  pricedCount: number
  quoteCount: number
  assignedAt: string
  old: boolean
  /** My folders the part is in (none = only the public folder). */
  folderIds: number[]
}

/** A user's own folder of parts; the public folder (every part) is not one. */
export interface PpeFolder { id: number; name: string; partCount: number }

export interface PpePartPage {
  items: PpePartRow[]; total: number; page: number; pageSize: number; oldCount: number
  /** My folders with part counts, and the public folder's count (Show older applies, search does not). */
  folders: PpeFolder[]; publicCount: number
}

/** Add part dialog: a part from the database. */
export interface PpeCatalogPart { id: number; partNumber: string; description?: string | null; alternatives: string[]; alreadyAdded: boolean }
export interface PpeAddPartResponse { id: number; partNumber: string; alreadyListed: boolean; restored: boolean }
export interface PpeBulkAddPartsResponse { added: number; restored: number; alreadyListed: number }

export interface PpeRfqRef {
  rfqId: number
  rfqName: string
  rfqItemId: number
  deadline: string
  customerId: number
  customerCode?: string | null
  customerName?: string | null
  customerBase?: number | null
  /** Customer's commission minimum with Trusted suppliers (null = at least COMMISSION_DEFAULT_MIN). */
  minCoef?: number | null
  partNumber: string
  qty: number
  condition?: string | null
  quoteId?: number | null
  /** Quote rows: that RFQ's supplier quote — its own price / qty / shipping / Coef 1 and commission. */
  quotePrice?: number | null
  quoteQty?: number | null
  quoteShippingCost?: number | null
  coef1?: number | null
  /** Selling unit price saved on the quote (typed by hand), if any. */
  quoteUnitPrice?: number | null
  commissionAt?: string | null
}

export type PpeRowKind = 'Draft' | 'Quote' | 'Email'

/** A supplier row under a part — field names follow the RFQ supplier quote, so SupplierQuoteEditDialog edits it. */
export interface PpeQuoteRow {
  key: string
  kind: PpeRowKind
  readOnly: boolean
  lineId?: number | null
  /** Email rows on no RFQ: the accepted Email Quotes line (edits go to it). */
  emailLineId?: number | null
  quoteIds: number[]
  rfqs: PpeRfqRef[]
  reason?: string | null
  filePrice?: string | null
  fileName?: string | null
  methods: string[]
  updatedAt: string
  supplierId?: number | null
  supplierName: string
  supplierStatus?: string | null
  supplierTrusted: boolean
  supplierEmail?: string | null
  price?: number | null
  qty?: number | null
  condition?: string | null
  alt?: string | null
  unit?: string | null
  leadTime?: string | null
  sellLeadTime?: string | null
  certName?: string | null
  tagDate?: string | null
  shippingCost?: number | null
  shippingPoint?: string | null
  note?: string | null
  myNotes?: string | null
  customCertReferences: string[]
  serialNumber?: string | null
  traceTo?: string | null
  tagType?: string | null
  priceType?: string | null
  taggedBy?: string | null
  moQ?: string | null
  warranty?: string | null
  cr?: string | null
}

export interface PpePartDetail { part: PpePartRow; rfqs: PpeRfqRef[]; rows: PpeQuoteRow[] }

export interface PpeSaveResult { lineId: number; written: boolean; quotesWritten: number; message?: string | null; quoteId?: number | null; rfqId?: number | null }

/** A line already on a part — enough for "Added" on file rows. */
export interface PpeLineHead {
  id: number
  partId: number
  supplierId?: number | null
  preferredId?: number | null
  supplierNameRaw?: string | null
  condition?: string | null
  alt?: string | null
  status: string
  price?: number | null
}

export interface PpeUpload { id: number; fileName: string; uploadedAt: string; rowCount: number; matchedRows: number }

/** A listed part — description, qty and condition asked (the supplier quotation mail). */
export interface PpePartBrief {
  id: number; partNumber: string; description?: string | null; qty?: number | null; condition?: string | null
  /** Every condition asked (the part's and its RFQs'), upper case. */
  conditions: string[]
}

/** preferred = my preferred suppliers (same list as ProMax); rows carry the one they matched (preferredId). */
export interface PpeIls { freshDays: number; uploads: PpeUpload[]; rows: PpOfferRow[]; preferred: PpPreferredSupplier[]; lines: PpeLineHead[]; parts: PpePartBrief[] }

/**
 * Line heads in the shape ProcurementPlusFileOffers reads (offerAdded). A supplier added from the files is one line
 * without condition / alt until its quote mail is accepted — it marks every listing of that supplier as added (and
 * so does a line still waiting for its price). Same rule as AddOffersAsync on the server.
 */
export function asOfferLines(lines: PpeLineHead[]): PpQuoteLine[] {
  return lines.map(l => ({
    line: { supplierId: l.supplierId, supplierNameRaw: l.supplierNameRaw, condition: l.condition, alt: l.alt },
    preferredPartId: l.partId,
    preferredId: l.preferredId ?? undefined,
    status: l.status,
    writtenRfqs: [],
    coversSupplier: (l.status === 'Pending' && !l.price) || (!l.condition?.trim() && !l.alt?.trim()),
  }))
}

// ── Quotation request mail (ILS tab mail dialog, Available suppliers copy button) ──

export interface PpeMailLine { key: string; partNumber: string; ourPart: string; isAlt: boolean; description: string; qty: number | null; condition: string; selected: boolean }

const mailKey = (s?: string | null) => (s || '').replace(/\s+/g, '').toUpperCase()

/** One line per P/N the supplier lists; description / qty / condition from my part (what I need), else the file. */
export function buildMailLines(rows: PpOfferRow[], parts: PpePartBrief[]): PpeMailLine[] {
  const byId = new Map(parts.map(p => [p.id, p]))
  const seen = new Set<string>()
  const list: PpeMailLine[] = []
  for (const r of rows) {
    const key = mailKey(r.filePartNo)
    if (!key || seen.has(key)) continue
    seen.add(key)
    const part = byId.get(r.partId)
    list.push({
      key,
      partNumber: r.filePartNo.trim(),
      ourPart: r.partNumber,
      isAlt: key !== mailKey(r.partNumber),
      description: part?.description || r.description || '',
      qty: part?.qty ?? 1,
      condition: part?.condition || r.condition || '',
      selected: true,
    })
  }
  return list
}

const MAIL_INTRO = ['Hi Dear', 'Hope you are well,', 'Could you please provide a quotation for the following parts:']
const MAIL_OUTRO = 'Thanks for your support!'
const MAIL_HEADERS = ['Part number', 'Description', 'Qty', 'Condition']
const mailCells = (l: PpeMailLine) => [l.partNumber, l.description, l.qty == null ? '' : String(l.qty), l.condition]
const escapeHtml = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

/** The mail as HTML (a real table, for pasting into a mail app) — every value escaped; only selected lines. */
export function mailHtml(lines: PpeMailLine[]) {
  const td = 'border:1px solid #999;padding:4px 10px;text-align:left;'
  const head = MAIL_HEADERS.map(h => `<th style="${td}background:#f0f0f0;">${h}</th>`).join('')
  const body = lines.filter(l => l.selected)
    .map(l => `<tr>${mailCells(l).map(c => `<td style="${td}">${escapeHtml(c)}</td>`).join('')}</tr>`).join('')
  return `${MAIL_INTRO.map(p => `<p style="margin:0 0 6px;">${p}</p>`).join('')}`
    + `<table style="border-collapse:collapse;margin:8px 0 12px;font-family:Arial,sans-serif;font-size:13px;"><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table>`
    + `<p style="margin:0;">${MAIL_OUTRO}</p>`
}

/** Same mail as plain text (tab-separated table) for apps that drop the HTML. */
export function mailText(lines: PpeMailLine[]) {
  return [
    ...MAIL_INTRO,
    '',
    MAIL_HEADERS.join('\t'),
    ...lines.filter(l => l.selected).map(l => mailCells(l).join('\t')),
    '',
    MAIL_OUTRO,
  ].join('\n')
}

/** Copies rich HTML (table kept) — works on plain http too, where navigator.clipboard is missing. */
export function copyHtml(markup: string, plain: string) {
  const el = document.createElement('div')
  el.contentEditable = 'true'
  el.innerHTML = markup
  el.style.position = 'fixed'
  el.style.left = '-9999px'
  el.style.background = '#fff'
  el.style.color = '#000'
  document.body.appendChild(el)
  const range = document.createRange()
  range.selectNodeContents(el)
  const sel = window.getSelection()
  sel?.removeAllRanges()
  sel?.addRange(range)
  let ok = false
  try { ok = document.execCommand('copy') } catch { ok = false }
  sel?.removeAllRanges()
  document.body.removeChild(el)
  return ok || copyText(plain)
}

/** A supplier's addresses from its file rows (and catalog email), lower-case, each once. */
export const supplierMailAddresses = (rows: PpOfferRow[]) =>
  [...new Set(rows.flatMap(r => [r.email, r.supplierEmail]).map(e => e?.trim().toLowerCase()).filter((e): e is string => !!e))]

// ── Add Commission (Procurement Plus only — the Add Quote page has no such rule) ──

/** Selling total from which a Trusted supplier may give the customer exactly its MinCoef (switched off for now). */
export const COMMISSION_THRESHOLD = 10_000
/** Lowest coef without a Trusted supplier + customer MinCoef (switched off for now). */
export const COMMISSION_DEFAULT_MIN = 1.13
/** Below this selling total no commission can be saved at all (switched off for now). */
export const COMMISSION_MIN_TOTAL = 5_000
/** A saved commission is active this long: Coef 1 locked, and new RFQs for the part + supplier + customer get it. */
export const COMMISSION_DAYS = 7

/** Saved within the last 7 days (still locked / still reaching new RFQs). */
export const commissionActive = (at?: string | null) => !!at && Date.now() - new Date(at).getTime() < COMMISSION_DAYS * 86_400_000

/** When an active commission ends. */
export const commissionEnds = (at: string) => new Date(new Date(at).getTime() + COMMISSION_DAYS * 86_400_000)

/** Unit price before the coef, as on the Add Quote page: buy price + shipping per piece. */
export function commissionBase(ref: PpeRfqRef) {
  const qty = Number(ref.quoteQty) || 1
  return (Number(ref.quotePrice) || 0) + (Number(ref.quoteShippingCost) || 0) / qty
}

/** Total Price (Unit Price × Qty) of one supplier's quotes on a customer's RFQs, added up; unitPrice = typed by hand. */
export function commissionTotal(refs: PpeRfqRef[], coef: number, unitPrice?: number | null) {
  return refs.reduce((sum, r) => sum + (unitPrice ?? commissionBase(r) * coef) * (Number(r.quoteQty) || 1), 0)
}

/**
 * Why this coef is not allowed for one RFQ quote ('' = allowed): for now only an empty coef. Mirrors the server check.
 * Switched off for now (Sam 2026-10-28), kept commented to roll back: the 5K$ / 10K$ conditions on the RFQ's own
 * Total Price (Unit Price × Qty), the Trusted supplier + MinCoef rule and the 1.13 minimum — under 5K$ → no commission;
 * Trusted + MinCoef: ≥ 10K$ → coef ≥ MinCoef, below → coef > MinCoef; otherwise coef ≥ 1.13.
 */
export function commissionError(ref: PpeRfqRef, supplierTrusted: boolean, coef: number | null | undefined) {
  if (coef == null || !(coef > 0)) return 'Fill the coef'
  // const total = commissionTotal([ref], coef)
  // const eps = 1e-9
  // if (total < COMMISSION_MIN_TOTAL) return `Total Price (Unit Price × Qty) ${total.toLocaleString(undefined, { maximumFractionDigits: 2 })}$ is under ${COMMISSION_MIN_TOTAL.toLocaleString()}$ — no commission`
  // if (supplierTrusted && ref.minCoef != null) {
  //   const min = Number(ref.minCoef)
  //   if (total >= COMMISSION_THRESHOLD) return coef + eps < min ? `At least the customer's minimum ${min}` : ''
  //   return coef <= min + eps ? `Under ${COMMISSION_THRESHOLD.toLocaleString()}$ — must be bigger than the customer's minimum ${min}` : ''
  // }
  // return coef + eps < COMMISSION_DEFAULT_MIN
  //   ? `At least ${COMMISSION_DEFAULT_MIN} (${supplierTrusted ? 'customer has no MinCoef' : 'supplier is not Trusted'})`
  //   : ''
  // return coef + eps < COMMISSION_DEFAULT_MIN ? `At least ${COMMISSION_DEFAULT_MIN}` : ''
  return ''
}

/** Same colours as the /procument deadline chip. */
export function deadlineColor(deadline?: string | null) {
  if (!deadline) return 'grey'
  const day = 86_400_000
  const d = Math.round((new Date(deadline).setHours(0, 0, 0, 0) - new Date().setHours(0, 0, 0, 0)) / day)
  if (d < 0) return 'error'
  if (d <= 3) return 'warning'
  if (d <= 7) return 'orange'
  return 'info'
}
