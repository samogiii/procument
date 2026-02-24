/**
 * Centralized status → color mapping for the entire application.
 * Replaces 4+ duplicated `statusColor()` helpers across list pages.
 */

const STATUS_COLORS: Record<string, string> = {
    // Quote
    Draft: 'grey',
    Sent: 'info',
    Accepted: 'success',
    Rejected: 'error',

    // Proforma Invoice
    Pending: 'warning',
    Paid: 'success',
    Overdue: 'error',

    // Purchase Order
    Received: 'success',
    Cancelled: 'error',

    // General
    Active: 'success',
    Inactive: 'grey',
}

export function useStatusColor() {
    /**
     * Returns a Vuetify color string for any status value.
     * Works across all entity types (quote, invoice, PO, etc).
     */
    function statusColor(status: string | undefined | null): string {
        if (!status) return 'grey'
        return STATUS_COLORS[status] ?? 'grey'
    }

    return { statusColor }
}
