/** Customer Follow Ups sheet — column definitions and display helpers shared by the page and its rows. */

/**
 * 'docs', 'chat', 'rfq' and 'refs' cells are not edited in place — clicking one opens the row's
 * documents / messages / RFQ / Other Refs dialog. 'select' is a dropdown without free entry.
 */
export type FollowUpColumnType = 'id' | 'text' | 'date' | 'combo' | 'select' | 'docs' | 'chat' | 'rfq' | 'refs'

export interface FollowUpColumn {
    key: FollowUpKey
    label: string
    type: FollowUpColumnType
    /** Width before the user resizes the column. */
    width: number
}

export interface FollowUpRfqRef {
    id: number
    name: string
    status: string | null
    customerCode: string | null
}

export type FollowUpRefType = 'PurchaseOrder' | 'Quote' | 'Invoice' | 'FinalInvoice'

export interface FollowUpRef {
    type: FollowUpRefType
    id: number
    /** The document number ("#id" when it was deleted since). */
    label: string
    missing: boolean
}

export interface FollowUpRow {
    id: number
    customerCode: string | null
    category: string | null
    /** The linked RFQ (RFQ Ref#), or null. */
    rfq: FollowUpRfqRef | null
    /** Other Refs: purchase orders, quotes, invoices, final invoices. */
    refs: FollowUpRef[]
    partNumber: string | null
    requestedItem: string | null
    /** Dates are kept as yyyy-MM-dd strings (the server returns date-only values). */
    dateOfRequest: string | null
    deadline: string | null
    assign: string | null
    /** Open | In Progress | Done. */
    procurementStatus: string
    /** Open | In Progress | Done. */
    salesStatus: string
    issueStatus: string | null
    dateOfReply: string | null
    ourResponse: string | null
    feedback: string | null
    createdBy: string | null
    createdAt: string
    modifiedBy: string | null
    modifyAt: string | null
    /** PDFs attached to the row (managed in the documents dialog, not by cell edits). */
    documentCount: number
    /** Chat messages on the row (managed in the messages dialog). */
    messageCount: number
    /** Someone else wrote in the chat since the current user last opened it, or they marked it unread. */
    hasUnreadMessages: boolean
}

export type FollowUpKey =
    | 'id' | 'customerCode' | 'category' | 'rfq' | 'refs' | 'partNumber' | 'requestedItem' | 'dateOfRequest' | 'deadline'
    | 'assign' | 'documentCount' | 'messageCount' | 'procurementStatus' | 'salesStatus' | 'issueStatus' | 'dateOfReply'
    | 'ourResponse' | 'feedback'

export type EditableFollowUpKey = Exclude<FollowUpKey, 'id' | 'documentCount' | 'messageCount' | 'rfq' | 'refs'>

export const FOLLOW_UP_COLUMNS: FollowUpColumn[] = [
    { key: 'id', label: 'Row Id', type: 'id', width: 70 },
    { key: 'customerCode', label: 'Customer Code', type: 'combo', width: 130 },
    { key: 'category', label: 'Category', type: 'combo', width: 110 },
    { key: 'rfq', label: 'RFQ Ref#', type: 'rfq', width: 150 },
    { key: 'refs', label: 'Other Refs', type: 'refs', width: 190 },
    { key: 'partNumber', label: 'Partnumber', type: 'text', width: 150 },
    { key: 'requestedItem', label: 'Requested - To Do Item', type: 'text', width: 260 },
    { key: 'dateOfRequest', label: 'Date Of Request', type: 'date', width: 125 },
    { key: 'deadline', label: 'Deadline', type: 'date', width: 125 },
    { key: 'assign', label: 'Assign', type: 'combo', width: 120 },
    { key: 'documentCount', label: 'Documents', type: 'docs', width: 110 },
    { key: 'messageCount', label: 'Messages', type: 'chat', width: 110 },
    { key: 'procurementStatus', label: 'Procurement Status', type: 'select', width: 135 },
    { key: 'salesStatus', label: 'Sales Status', type: 'select', width: 120 },
    { key: 'issueStatus', label: 'Issue Status', type: 'combo', width: 160 },
    { key: 'dateOfReply', label: 'Date Of Reply', type: 'date', width: 125 },
    { key: 'ourResponse', label: 'Our Response', type: 'text', width: 240 },
    { key: 'feedback', label: 'Feedback', type: 'text', width: 240 },
]

export const FOLLOW_UP_COLUMN_BY_KEY = Object.fromEntries(
    FOLLOW_UP_COLUMNS.map(column => [column.key, column])) as Record<FollowUpKey, FollowUpColumn>

/** Cells that open a dialog instead of an in-place editor. */
export const DIALOG_COLUMN_TYPES = new Set<FollowUpColumnType>(['docs', 'chat', 'rfq', 'refs'])

/** Other Refs types: picker tab, chip prefix and the page each one opens. */
export const FOLLOW_UP_REF_TYPES: { type: FollowUpRefType; tab: string; short: string; icon: string; path: (id: number) => string }[] = [
    { type: 'PurchaseOrder', tab: 'Purchase Orders', short: 'PO', icon: 'mdi-cart-outline', path: id => `/purchase-orders/${id}` },
    { type: 'Quote', tab: 'Quotes', short: 'Quote', icon: 'mdi-file-document-outline', path: id => `/quotes/${id}` },
    { type: 'Invoice', tab: 'Invoices', short: 'PI', icon: 'mdi-receipt-text-outline', path: id => `/invoices/${id}` },
    { type: 'FinalInvoice', tab: 'Final Invoices', short: 'FI', icon: 'mdi-file-certificate-outline', path: id => `/final-invoices/${id}` },
]

export const FOLLOW_UP_REF_TYPE = Object.fromEntries(
    FOLLOW_UP_REF_TYPES.map(meta => [meta.type, meta])) as Record<FollowUpRefType, (typeof FOLLOW_UP_REF_TYPES)[number]>

export function rfqPath(id: number): string {
    return `/rfqs/${id}`
}

const DATE_KEYS = new Set<FollowUpKey>(['dateOfRequest', 'deadline', 'dateOfReply'])

/** Server rows carry dates as "2026-10-14T00:00:00"; the sheet works with "2026-10-14". */
export function normalizeFollowUpRow(raw: any): FollowUpRow {
    const row = { ...raw } as FollowUpRow
    for (const key of DATE_KEYS) {
        const value = (row as any)[key]
        ;(row as any)[key] = typeof value === 'string' && value ? value.slice(0, 10) : null
    }
    row.documentCount = Number(raw?.documentCount) || 0
    row.messageCount = Number(raw?.messageCount) || 0
    row.hasUnreadMessages = !!raw?.hasUnreadMessages
    row.rfq = raw?.rfq ?? null
    row.refs = Array.isArray(raw?.refs) ? raw.refs : []
    row.procurementStatus = raw?.procurementStatus || 'Open'
    row.salesStatus = raw?.salesStatus || 'Open'
    return row
}

/** The user's local date as yyyy-MM-dd (not UTC — early-morning rows must not land on yesterday). */
export function localToday(): string {
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

export const BLANK_FILTER_VALUE = '(Blank)'

/** The text a cell shows — also the value its Excel-style filter lists. */
export function followUpCellText(row: FollowUpRow, key: FollowUpKey): string {
    if (key === 'documentCount') return documentCountText(row.documentCount)
    if (key === 'messageCount') return messageCountText(row.messageCount)
    if (key === 'rfq') return row.rfq?.name ?? ''
    if (key === 'refs') return row.refs.map(followUpRefText).join(', ')
    const value = row[key]
    if (value === null || value === undefined || value === '') return ''
    return String(value)
}

/** "PO 12-345", "PI P101-…" — the chip text of one Other Ref. */
export function followUpRefText(ref: FollowUpRef): string {
    return `${FOLLOW_UP_REF_TYPE[ref.type]?.short ?? ref.type} ${ref.label}`
}

export function documentCountText(count: number): string {
    return count > 0 ? `${count} file${count === 1 ? '' : 's'}` : ''
}

/** Status colors (Vuetify theme color names). Values users add get the neutral color. */
const STATUS_COLORS: Record<string, string> = {
    'yes': 'success',
    'no': 'error',
    'closed': 'grey',
    'waiting': 'warning',
    'open': 'info',
    'in progress': 'orange-darken-1',
    'done': 'success',
    '1st follow up': 'amber-darken-2',
    '2nd follow up': 'orange-darken-2',
    '3rd follow up': 'deep-orange',
    'waiting for feedback': 'purple',
}

export function followUpStatusColor(status: string | null | undefined): string {
    return STATUS_COLORS[(status ?? '').trim().toLowerCase()] ?? 'blue-grey'
}

/** Greyed out and never overdue: Issue Status Closed or Sales Status Done. */
export function isClosedFollowUp(row: FollowUpRow): boolean {
    return (row.issueStatus ?? '').trim().toLowerCase() === 'closed'
        || row.salesStatus.trim().toLowerCase() === 'done'
}

export function messageCountText(count: number): string {
    return count > 0 ? `${count} message${count === 1 ? '' : 's'}` : ''
}

/** Distinct, readable-on-both-themes colors for chat senders. */
const SENDER_PALETTE = [
    '#6366f1', '#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6',
    '#14b8a6', '#ec4899', '#f97316', '#3b82f6', '#84cc16', '#d946ef',
]

function nameHash(name: string): number {
    let hash = 0
    for (const char of name.toLowerCase()) hash = (hash * 31 + char.charCodeAt(0)) >>> 0
    return hash
}

/**
 * One color per sender. Each name starts from its own hashed palette slot (so it is the same on
 * every row); two names landing on the same slot in one conversation get the next free one.
 */
export function senderColors(names: string[]): Map<string, string> {
    const colors = new Map<string, string>()
    const taken = new Set<number>()
    const unique = [...new Set(names.map(name => name.toLowerCase()))].sort()
    for (const name of unique) {
        let slot = nameHash(name) % SENDER_PALETTE.length
        for (let i = 0; i < SENDER_PALETTE.length && taken.has(slot); i++) slot = (slot + 1) % SENDER_PALETTE.length
        taken.add(slot)
        colors.set(name, SENDER_PALETTE[slot]!)
    }
    return colors
}

/** "AMJ" → "AMJ", "Sam Smith" → "SS", "Mohammad" → "MO". */
export function senderInitials(name: string): string {
    const words = name.trim().split(/\s+/).filter(Boolean)
    if (words.length > 1) return (words[0]![0]! + words[1]![0]!).toUpperCase()
    const word = words[0] ?? '?'
    return (word.length <= 3 ? word : word.slice(0, 2)).toUpperCase()
}

/** Files a row accepts (Documents dialog and chat): PDFs and photos browsers can show. Mirrors the server's list. */
const FOLLOW_UP_FILE_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png', '.gif', '.webp', '.bmp']

export const FOLLOW_UP_FILE_ACCEPT = [
    ...FOLLOW_UP_FILE_EXTENSIONS,
    'application/pdf', 'image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp',
].join(',')

export function isAllowedFollowUpFile(file: File): boolean {
    const name = file.name.toLowerCase()
    return FOLLOW_UP_FILE_EXTENSIONS.some(extension => name.endsWith(extension))
}

export function isImageMime(mimeType: string | null | undefined): boolean {
    return (mimeType ?? '').startsWith('image/')
}
