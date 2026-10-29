/** BI page — shapes returned by /api/bi (BIController) and shared formatters. */

export interface BiCustomerOption {
    id: number
    name: string
    code?: string | null
    base?: number | null
}

export interface BiBaseOption {
    base: number
    customerCount: number
}

export interface BiMetrics {
    rfqCount: number
    sentQuoteCount: number
    sentQuoteSum: number
    piCount: number
    /** Proformas with at least one line in a live PO. */
    boughtPiCount: number
    /** Proformas with only some of their lines in a PO. */
    partialPoCount: number
    /** Sell value of the proforma lines that are in a PO — counted whether or not the customer paid yet. */
    boughtValue: number
    /** PO line cost of those lines. */
    supplierCost: number
    /** Shipping, wire fee, tax, processing and "Bank Fee and Others" from the wallet withdraws. */
    fees: number
    /** Part of `fees` still taken from unpaid POs' written amounts. */
    feesEstimated: number
    /** boughtValue − supplierCost − fees */
    profit: number
    /** POs behind these proformas not fully paid yet. */
    unpaidPoCount: number
    /** Fee withdraws left out because their currency could not be converted (totals only). */
    unconvertedFeeCount: number
    finalizedCount: number
    avgDaysToFinal: number | null
    minDaysToFinal: number | null
    maxDaysToFinal: number | null
}

export interface BiCustomerRow extends BiMetrics {
    customerId: number
    customerName: string
    customerCode?: string | null
    base?: number | null
}

export interface BiTrendPoint {
    start: string
    rfqs: number
    sentQuotes: number
    sentQuoteSum: number
    pis: number
    /** Bought value of the proformas created in this bucket. */
    boughtValue: number
}

export type BiBucket = 'day' | 'week' | 'month'

export interface BiSummary {
    from: string
    to: string
    days: number
    bucket: BiBucket
    totals: BiMetrics
    customers: BiCustomerRow[]
    trend: BiTrendPoint[]
}

export interface BiProformaRow {
    id: number
    invoiceNumber: string
    b1ProformaInvoiceNumber?: string | null
    status: string
    createdAt: string
    customerId: number
    customerName: string
    customerCode?: string | null
    quoteNumber?: string | null
    /** Whole proforma total, for context only. */
    totalAmount: number
    lineCount: number
    boughtLineCount: number
    poCount: number
    unpaidPoCount: number
    /** Null when nothing on the proforma is in a PO yet. */
    boughtValue: number | null
    supplierCost: number | null
    fees: number | null
    feesEstimated: number | null
    profit: number | null
    marginPercent: number | null
    firstFinalAt: string | null
    lastFinalAt: string | null
    finalCount: number
    daysToFinal: number | null
}

export interface BiProformaPage {
    total: number
    items: BiProformaRow[]
}

export type BiProformaSort = 'createdAt' | 'boughtValue' | 'supplierCost' | 'fees' | 'profit' | 'daysToFinal' | 'customer'

export const BI_DAY_PRESETS = [7, 30, 90, 180, 365] as const
export const BI_MAX_DAYS = 3650

/** "$12,345" below a million, "$1.2M" above — for KPI tiles and chart axes. */
export function biMoney(value: number | null | undefined, compact = false): string {
    const n = Number(value ?? 0)
    if (compact && Math.abs(n) >= 10_000) {
        return '$' + new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(n)
    }
    return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: compact ? 0 : 2 })
}

export function biCount(value: number | null | undefined): string {
    return Number(value ?? 0).toLocaleString('en-US')
}

/** Ratio as "42%" or "—" when the base is zero. */
export function biPercent(part: number, whole: number): string {
    if (!whole) return '—'
    return `${Math.round((part / whole) * 100)}%`
}

export function biMargin(profit: number, revenue: number): number | null {
    return revenue ? Math.round((profit / revenue) * 1000) / 10 : null
}

export function biDays(value: number | null | undefined): string {
    if (value == null) return '—'
    return `${value.toLocaleString('en-US', { maximumFractionDigits: 1 })} d`
}

export function biDate(value: string | null | undefined): string {
    return value ? new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—'
}

export function biCustomerLabel(c: { name?: string; customerName?: string; code?: string | null; customerCode?: string | null }): string {
    const name = c.name ?? c.customerName ?? ''
    const code = c.code ?? c.customerCode
    return code ? `${code} · ${name}` : name
}
