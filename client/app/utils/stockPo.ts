/** One editable line of a Stock PO (see StockPoLinesEditor.vue). */
export type StockPoLine = {
  key: string
  partNumberId: number | null
  partNumber: string
  qty: number
  unitPrice: number
  condition: string
}

let lineSeq = 0

export function makeStockPoLine(partial: Partial<StockPoLine> = {}): StockPoLine {
  return { key: `l${Date.now()}-${lineSeq++}`, partNumberId: null, partNumber: '', qty: 1, unitPrice: 0, condition: 'NE', ...partial }
}

/** Status → chip colour for Stock POs; mirrors the PO pages. */
export function stockPoStatusColor(status?: string | null): string {
  switch (status) {
    case 'Draft': return 'grey'
    case 'Waiting For Admin Approval': return 'orange'
    case 'Waiting For Supplier Documents': return 'amber'
    case 'Waiting For PR': return 'deep-orange'
    case 'Waiting For Payment': return 'warning'
    case 'PR Rejected': return 'error'
    case 'Payment Done': return 'teal'
    case 'Ship to Warehouse':
    case 'Waiting For Shipment': return 'info'
    case 'Received in Warehouse': return 'indigo'
    case 'Completed': return 'success'
    case 'Cancelled':
    case 'Returned': return 'error'
    default: return 'grey'
  }
}
