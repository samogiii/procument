/** Shapes of /api/supplier-credits (see FEATURE_PLAN_SUPPLIER_CREDITS.md §3.4). */
export type SupplierCreditSource = 'CreditNote' | 'WalletTopUp' | 'POApplication' | 'Adjustment' | 'Reversal' | 'CreditLimit' | 'Repayment'
/** Prepaid: credit the supplier holds for us. CreditLine: buy now, pay back later (balance = still available). */
export type SupplierCreditKind = 'Prepaid' | 'CreditLine'
export type SupplierCreditDialogMode = 'creditNote' | 'walletTopUp' | 'adjustment'

export interface SupplierCreditAccountRow {
  id: number
  supplierId: number
  supplierName: string
  companyPresetId: number
  companyPresetName: string
  currency: string
  balance: number
  totalIn: number
  totalOut: number
  lastEntryAt: string | null
  kind: SupplierCreditKind
  /** Credit lines: the granted limit (balance is what is still available). */
  creditLimit: number | null
  /** Credit lines: what we owe the supplier. 0 for prepaid. */
  owed: number
  /** Credit lines: first use since the line was last fully available. */
  oldestUnpaidAt: string | null
  /** Credit lines: repayment request waiting for its POP or final amount. */
  openRepaymentId: number | null
}

export interface SupplierCreditEntryRow {
  id: number
  accountId: number
  direction: 'In' | 'Out'
  source: SupplierCreditSource
  amount: number
  balanceAfter: number
  poId: number | null
  poNumber: string | null
  poLink: string | null
  paymentRequestId: number | null
  prNumber: number | null
  walletTransactionId: number | null
  walletName: string | null
  /** Where a credit note came from: one of our POs… */
  sourcePoId: number | null
  sourcePoNumber: string | null
  sourcePoLink: string | null
  /** …or free text. */
  sourcePoReference: string | null
  creditMemo: string | null
  notes: string | null
  documents: SupplierCreditDocumentRow[]
  reversesEntryId: number | null
  reversedByEntryId: number | null
  createdAt: string
  createdByName: string | null
}

export interface SupplierCreditWriteResult {
  entry: SupplierCreditEntryRow
  balance: number
}

export type SupplierCreditOption = { id: number; name: string }

export type SupplierCreditDocumentCategory = 'SupplierPI' | 'CreditMemo' | 'Other'

export interface SupplierCreditDocumentRow {
  id: number
  entryId: number
  category: SupplierCreditDocumentCategory
  originalFileName: string
  sizeBytes: number
  uploadedAt: string
  uploadedByName: string | null
}

/** GET /supplier-credits/lookup/purchase-orders — the supplier's POs, offered as a credit's origin. */
export interface SupplierCreditPoOption {
  id: number
  poNumber: string
  companyPresetName: string | null
  status: string
  totalAmount: number | null
  createdAt: string
}

export const SUPPLIER_CREDIT_DOCUMENT_LABELS: Record<SupplierCreditDocumentCategory, string> = {
  SupplierPI: 'Supplier PI',
  CreditMemo: 'Credit memo',
  Other: 'Document',
}

/** Same list the API accepts (SupplierCreditService.DocumentExtensions). */
export const SUPPLIER_CREDIT_FILE_ACCEPT = '.pdf,.png,.jpg,.jpeg,.webp,.doc,.docx,.xls,.xlsx,.eml,.msg'
export const SUPPLIER_CREDIT_MAX_FILES = 10

/** GET /supplier-credits/summary — tab badges. */
export interface SupplierCreditSummary {
  prepaidTotal: number
  creditLinesOwingCount: number
  creditLinesOwedTotal: number
}

/** The PO company's credit line with the supplier (suggestion). */
export interface SupplierCreditLineInfo {
  accountId: number
  creditLimit: number
  available: number
  owed: number
  suggestedAmount: number
}

export type SupplierCreditRepaymentStatus = 'READY FOR POP' | 'WAITING FOR FINAL AMOUNT' | 'PAID'

/** One pay-back of a credit line (a payment request without a PO). */
export interface SupplierCreditRepaymentRow {
  id: number
  prNumber: number | null
  status: SupplierCreditRepaymentStatus
  amount: number
  walletId: number | null
  walletName: string | null
  walletCurrency: string | null
  exchangeRate: number | null
  /** POP file: GET /supplier-payments/{popTransactionId}/file */
  popTransactionId: number | null
  popFileName: string | null
  walletAmount: number | null
  bankFee: number | null
  createdAt: string
}

export interface SupplierCreditCompanyBalance {
  companyPresetId: number
  companyPresetName: string
  balance: number
  /** Balance = prepaid credit, or what is still available on a credit line. */
  kind: SupplierCreditKind
}

/** GET /supplier-credits/po/{poId}/suggestion */
export interface SupplierCreditSuggestion {
  poId: number
  supplierId: number
  supplierName: string
  companyPresetId: number | null
  companyPresetName: string | null
  accountId: number | null
  usableBalance: number
  /** The PO's own company's credit line with this supplier, if any. */
  creditLine: SupplierCreditLineInfo | null
  poRemaining: number
  prOptions: { paymentRequestId: number; prNumber: number | null; remaining: number }[]
  suggestedAmount: number
  otherCompanies: SupplierCreditCompanyBalance[]
  canApply: boolean
  cannotApplyReason: string | null
  walletPaymentInProgress: boolean
  canChangeCompany: boolean
  changeCompanyBlockedReason: string | null
}

/** POST /supplier-credits/po/{poId}/apply */
export interface ApplySupplierCreditResult {
  entry: SupplierCreditEntryRow
  balance: number
  paidAmount: number
  remainingAmount: number
  prStatus: string | null
  poPaymentStatus: string
  complete: boolean
}

/**
 * Idempotency key for one submit of a credit dialog (plan rule 9). Generate it when the dialog
 * opens and reuse it for retries: the API returns the first result instead of writing twice.
 * crypto.randomUUID() needs a secure context and the app is served over plain HTTP on the LAN,
 * so the UUID is built from crypto.getRandomValues(), which works everywhere.
 */
export function newRequestId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = (bytes[6]! & 0x0f) | 0x40 // version 4
  bytes[8] = (bytes[8]! & 0x3f) | 0x80 // RFC 4122 variant
  const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

const SOURCE_LABELS: Record<SupplierCreditSource, string> = {
  CreditNote: 'Credit note',
  WalletTopUp: 'Prepaid from wallet',
  POApplication: 'Used on PO',
  Adjustment: 'Adjustment',
  Reversal: 'Reversal',
  CreditLimit: 'Credit limit',
  Repayment: 'Paid back',
}

export function supplierCreditSourceLabel(source: SupplierCreditSource): string {
  return SOURCE_LABELS[source] ?? source
}

export function supplierCreditSourceColor(source: SupplierCreditSource): string {
  switch (source) {
    case 'CreditNote': return 'success'
    case 'WalletTopUp': return 'teal'
    case 'POApplication': return 'primary'
    case 'Adjustment': return 'warning'
    case 'Reversal': return 'grey'
    case 'CreditLimit': return 'indigo'
    case 'Repayment': return 'teal'
    default: return 'grey'
  }
}

/** The API's `{ message }` error body, else a fallback sentence. */
export function apiErrorMessage(e: any, fallback: string): string {
  return e?.data?.message || e?.response?._data?.message || fallback
}

/** Ledger label that knows the account kind: on a credit line, using it is a debt to pay back. */
export function supplierCreditEntryLabel(source: SupplierCreditSource, kind: SupplierCreditKind | undefined): string {
  if (kind === 'CreditLine' && source === 'POApplication') return 'Used on PO (to pay back)'
  return supplierCreditSourceLabel(source)
}

export function supplierCreditKindLabel(kind: SupplierCreditKind): string {
  return kind === 'CreditLine' ? 'credit line' : 'prepaid credit'
}
