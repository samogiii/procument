export interface WarehouseSyncSummary {
  tracksSent: number
  quantities: number
  notes: number
  boxes: number
  deletedBoxes: number
  files: number
  skipped: string[]
}

export interface WarehousePaymentSyncSummary {
  paymentsSent: number
  /** Paid results (POP + final amount) received for review. */
  paid: number
  docs: number
  skipped: string[]
}

interface Envelope { cipherText: string; iv: string }
interface WarehouseBatch extends Envelope { hasMore: boolean; itemCount: number }
interface PaymentExchange extends WarehouseBatch { needDocs: Envelope | null; needDocCount: number }
interface SyncConfig { enabled: boolean; warehouseUrl: string; apiKey: string }
interface ImportResult {
  ack: Envelope
  summary: Omit<WarehouseSyncSummary, 'tracksSent'>
}
interface PaymentImportResult { ack: Envelope; summary: { stored: number; skipped: string[] } }
interface DocsResult { batch: Envelope; count: number; next: Envelope | null; remaining: number }

// Same relay pattern as useSatelliteSync: the main API encrypts/decrypts, the browser only carries
// envelopes to the warehouse server (procument-warehouse) and back.
export function useWarehouseSync() {
  const api = useApi()

  async function getConfig() {
    return api.get<SyncConfig>('/warehouse-sync/config')
  }

  /** Payment users have their own config endpoint (the track one is for shipping roles). */
  async function getPaymentConfig() {
    return api.get<SyncConfig>('/warehouse-sync/payments/config')
  }

  function relay<T>(cfg: SyncConfig, path: string, body: unknown) {
    return $fetch<T>(`${cfg.warehouseUrl}/api/sync/${path}`, {
      method: 'POST',
      headers: { 'X-Sync-Api-Key': cfg.apiKey, 'Content-Type': 'application/json' },
      body: body as Record<string, any>,
      credentials: 'omit',
    })
  }

  async function sync(onProgress?: (text: string) => void): Promise<WarehouseSyncSummary> {
    const cfg = await getConfig()
    if (!cfg.enabled) throw new Error('Warehouse sync is not configured (WarehouseSync:Url).')
    const warehouse = (path: string, body: unknown) => relay<WarehouseBatch>(cfg, path, body)

    // 1. Open tracks → warehouse; it answers with its first batch of changes.
    onProgress?.('Sending track numbers…')
    const push = await api.get<Envelope & { trackCount: number }>('/warehouse-sync/prepare')
    let batch = await warehouse('exchange', { cipherText: push.cipherText, iv: push.iv })

    const total: WarehouseSyncSummary = {
      tracksSent: push.trackCount, quantities: 0, notes: 0, boxes: 0, deletedBoxes: 0, files: 0, skipped: [],
    }

    // 2. Import each batch, ack it, pull the next — until the warehouse has nothing left.
    //    Stop if a round stores nothing (e.g. every item failed) so it can't loop forever.
    for (let round = 1; round <= 50; round++) {
      if (!batch.itemCount) break
      onProgress?.(`Receiving warehouse data (batch ${round})…`)
      const result = await api.post<ImportResult>('/warehouse-sync/import', { cipherText: batch.cipherText, iv: batch.iv })
      const s = result.summary
      total.quantities += s.quantities
      total.notes += s.notes
      total.boxes += s.boxes
      total.deletedBoxes += s.deletedBoxes
      total.files += s.files
      total.skipped.push(...s.skipped)

      await warehouse('ack', result.ack)
      const stored = s.quantities + s.notes + s.boxes + s.deletedBoxes + s.files
      if (!batch.hasMore || stored === 0) break
      batch = await warehouse('pull', {})
    }

    return total
  }

  /**
   * Payment page: payments saved in Ready To Pay → warehouse; its paid results (POP + final amount)
   * come back for review; then the supplier documents it is missing go over in batches.
   */
  async function syncPayments(onProgress?: (text: string) => void): Promise<WarehousePaymentSyncSummary> {
    const cfg = await getPaymentConfig()
    if (!cfg.enabled) throw new Error('Warehouse sync is not configured (WarehouseSync:Url).')

    onProgress?.('Sending payments…')
    const push = await api.get<Envelope & { paymentCount: number }>('/warehouse-sync/payments/prepare')
    const exchange = await relay<PaymentExchange>(cfg, 'payments/exchange', { cipherText: push.cipherText, iv: push.iv })
    const total: WarehousePaymentSyncSummary = { paymentsSent: push.paymentCount, paid: 0, docs: 0, skipped: [] }

    // Paid results: a resend is acked with its reason, so every round answers each item and moves on.
    let batch: WarehouseBatch = exchange
    for (let round = 1; round <= 50; round++) {
      if (!batch.itemCount) break
      onProgress?.(`Receiving paid payments (batch ${round})…`)
      const result = await api.post<PaymentImportResult>('/warehouse-sync/payments/import', { cipherText: batch.cipherText, iv: batch.iv })
      total.paid += result.summary.stored
      total.skipped.push(...result.summary.skipped)
      await relay(cfg, 'payments/ack', result.ack)
      if (!batch.hasMore) break
      batch = await relay<WarehouseBatch>(cfg, 'payments/pull', {})
    }

    // Supplier documents the warehouse doesn't have yet.
    let need = exchange.needDocs
    for (let round = 1; need && round <= 50; round++) {
      onProgress?.(`Sending supplier documents (${total.docs} of ${exchange.needDocCount})…`)
      const docs = await api.post<DocsResult>('/warehouse-sync/payments/docs', need)
      if (docs.count) await relay(cfg, 'payments/docs', docs.batch)
      total.docs += docs.count
      if (!docs.count) break
      need = docs.next
    }

    return total
  }

  return { getConfig, getPaymentConfig, sync, syncPayments }
}
