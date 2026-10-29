/** Task Manager — one kanban board per user (api/task-board). */

export type TaskPriority = 'AOG' | 'High' | 'Normal'
export type TaskRefType = 'Rfq' | 'Quote' | 'Invoice' | 'PurchaseOrder' | 'FinalInvoice'

export interface TaskRef {
    type: TaskRefType
    id: number
    /** Document number ("#id" when it was deleted since). */
    label: string
    missing: boolean
}

export interface TaskLastComment {
    text: string
    userName: string
    createdAt: string
}

export interface TaskCard {
    id: number
    columnId: number
    position: number
    title: string
    priority: TaskPriority
    /** yyyy-MM-dd (server calendar day) or null. */
    dueDate: string | null
    hasDescription: boolean
    tagIds: number[]
    refs: TaskRef[]
    lastComment: TaskLastComment | null
    commentCount: number
    createdBy: string
    createdAt: string
}

export interface TaskColumn {
    id: number
    name: string
    color: string
    position: number
    isDone: boolean
    cards: TaskCard[]
}

export interface TaskTag {
    id: number
    name: string
    color: string
    usage: number
}

export interface TaskBoard {
    ownerUserId: number
    ownerName: string
    serverToday: string
    isOtherUser: boolean
    columns: TaskColumn[]
    tags: TaskTag[]
}

export interface TaskActivity {
    id: number
    kind: 'Comment' | 'Created' | 'Moved' | 'Title' | 'Description' | 'Priority' | 'DueDate' | 'Tag' | 'Ref'
    text: string
    userName: string
    createdAt: string
}

export interface TaskCardDetail {
    card: TaskCard
    description: string | null
    activities: TaskActivity[]
    tags: TaskTag[]
}

export interface TaskBoardUser {
    id: number
    name: string
    role: string
    cardCount: number
}

export const TASK_PRIORITIES: { value: TaskPriority; color: string; icon: string }[] = [
    { value: 'AOG', color: '#EF5350', icon: 'mdi-flag' },
    { value: 'High', color: '#FFB300', icon: 'mdi-flag' },
    { value: 'Normal', color: '#42A5F5', icon: 'mdi-flag' },
]

export const TASK_PRIORITY = Object.fromEntries(
    TASK_PRIORITIES.map(p => [p.value, p])) as Record<TaskPriority, (typeof TASK_PRIORITIES)[number]>

/** References: picker tab, chip prefix and the page each one opens. RFQ is one of them, not a separate field. */
export const TASK_REF_TYPES: { type: TaskRefType; tab: string; short: string; icon: string; path: (id: number) => string }[] = [
    { type: 'Rfq', tab: 'RFQs', short: 'RFQ', icon: 'mdi-file-search-outline', path: id => `/rfqs/${id}` },
    { type: 'Quote', tab: 'Quotes', short: 'Quote', icon: 'mdi-file-document-outline', path: id => `/quotes/${id}` },
    { type: 'Invoice', tab: 'Proforma Invoices', short: 'PI', icon: 'mdi-receipt-text-outline', path: id => `/invoices/${id}` },
    { type: 'PurchaseOrder', tab: 'Purchase Orders', short: 'PO', icon: 'mdi-cart-outline', path: id => `/purchase-orders/${id}` },
    { type: 'FinalInvoice', tab: 'Final Invoices', short: 'FI', icon: 'mdi-file-certificate-outline', path: id => `/final-invoices/${id}` },
]

export const TASK_REF_TYPE = Object.fromEntries(
    TASK_REF_TYPES.map(meta => [meta.type, meta])) as Record<TaskRefType, (typeof TASK_REF_TYPES)[number]>

export function taskRefText(ref: TaskRef): string {
    return `${TASK_REF_TYPE[ref.type]?.short ?? ref.type} ${ref.label}`
}

/** Column header colors offered when adding / editing a column. */
export const TASK_COLUMN_COLORS = ['#6E7681', '#8B949E', '#5B4FD8', '#1565C0', '#00838F', '#2E7D32', '#D4651F', '#E3B341', '#C62828', '#AD1457', '#8D6E63']

// ── Dates (yyyy-MM-dd strings, compared as text) ─────────────────────────────

const pad = (n: number) => String(n).padStart(2, '0')

/** yyyy-MM-dd of a Date's local calendar day. */
export function toDateKey(date: Date): string {
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** Local-midnight Date of a yyyy-MM-dd key (not UTC — the day must not shift). */
export function fromDateKey(key: string): Date {
    const [y, m, d] = key.slice(0, 10).split('-').map(Number)
    return new Date(y!, (m ?? 1) - 1, d ?? 1)
}

export function addDays(key: string, days: number): string {
    const date = fromDateKey(key)
    date.setDate(date.getDate() + days)
    return toDateKey(date)
}

export function daysBetween(from: string, to: string): number {
    return Math.round((fromDateKey(to).getTime() - fromDateKey(from).getTime()) / 86_400_000)
}

/** "Today", "Tomorrow", "Yesterday", "Mon 3 Nov", "3 Nov 2027" — relative to the server's today. */
export function dueDateLabel(due: string, today: string): string {
    const diff = daysBetween(today, due)
    if (diff === 0) return 'Today'
    if (diff === 1) return 'Tomorrow'
    if (diff === -1) return 'Yesterday'
    const date = fromDateKey(due)
    const sameYear = due.slice(0, 4) === today.slice(0, 4)
    return date.toLocaleDateString(undefined, {
        weekday: Math.abs(diff) < 7 ? 'short' : undefined,
        day: 'numeric',
        month: 'short',
        year: sameYear ? undefined : 'numeric',
    })
}

/** Text color of a due date: overdue red, today amber, otherwise none. Finished cards are never late. */
export function dueDateColor(due: string | null, today: string, done: boolean): string | undefined {
    if (!due || done) return undefined
    if (due < today) return 'error'
    if (due === today) return 'warning'
    return undefined
}

export function formatDateTime(value: string): string {
    return new Date(value).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
}

/** Initials-style avatar text: "GHS" → "GH". */
export function userInitials(name: string): string {
    return (name || '?').slice(0, 2).toUpperCase()
}
