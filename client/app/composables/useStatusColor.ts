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

    // Proforma Invoice workflow statuses
    Pending: 'warning',
    Running: 'blue',
    'Waiting For PrePayment': 'orange',
    Delivered: 'teal',
    Finish: 'success',

    // Purchase Order
    Received: 'success',

    // RFQ
    Open: 'light-blue',
    'In Progress': 'amber',
    'Waiting For Admin': 'deep-orange',
    'Ready To Quote': 'orange',
    Quoted: 'orange',
    'No Quote': 'deep-purple',
    Closed: 'blue-grey',
    Completed: 'success',
    Cancelled: 'error',

    // Total Projects lifecycle
    'Not Started': 'grey',
    'Under Contract': 'blue',
    'Waiting For Payment': 'warning',
    'PO Sent': 'cyan',
    'Document Added': 'purple',
    'Payment Done': 'teal',
    'Waiting For Supplier to Ship': 'orange',
    'Ship to Warehouse/Customer': 'success',

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
