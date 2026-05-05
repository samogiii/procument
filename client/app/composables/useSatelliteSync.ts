export interface SyncRfqItem {
  id: number
  mainAppId: number | null
  alt: string | null
  condition: string | null
  qty: number
  unit: string | null
  priority: string | null
  remark: string | null
  partNumberId: number
  partNumberName: string
  partNumberDescription: string | null
}

export interface SyncRfq {
  id: number
  mainAppId: number | null
  name: string
  status: string
  exType: number
  leadTime: string | null
  notes: string | null
  customerId: number
  customerName: string
  customerCode: string | null
  customerEmail: string | null
  customerPhone: string | null
  customerContactPerson: string | null
  customerShipTo: string | null
  customerBillTo: string | null
  customerShippingAccount: string | null
  customerDescription: string | null
  customerIsActive: boolean
  customerModifyAt: string | null
  customerTermsAndConditions: string | null
  customerCurrencyType: string | null
  customerExWork: number | null
  createdAt: string
  updatedAt: string | null
  receivedDate: string
  items: SyncRfqItem[]
}

export interface SyncResult {
  newRfqs: SyncRfq[]
  updatedRfqs: SyncRfq[]
  totalSatelliteRfqs: number
  receivedCount: number
  pendingCount: number
}

export interface SyncConfig {
  satelliteUrl: string
  apiKey: string
  defaultNodeId: number
}

export function useSatelliteSync() {
  const api = useApi()

  async function getConfig(): Promise<SyncConfig> {
    return api.get<SyncConfig>('/browser-sync/config')
  }

  // since: ISO-8601 UTC string — only RFQs created/modified at or after this are synced.
  // Defaults to 24 hours ago on the server when omitted.
  async function syncWithSatellite(baseNumber = 2, since?: string): Promise<SyncResult> {
    const cfg = await getConfig()

    // 1. Ask main API to build and encrypt the payload — no crypto in browser
    const sinceParam = since ? `&since=${encodeURIComponent(since)}` : ''
    const encrypted = await api.get<{ cipherText: string; iv: string }>(
      `/browser-sync/prepare-sync?baseNumber=${baseNumber}${sinceParam}`
    )

    // 2. Relay the encrypted blob to satellite (cross-origin, browser as carrier)
    const raw = await $fetch<{
      cipherText: string
      iv: string
      receivedCount: number
      pendingCount: number
      totalSatelliteRfqs: number
    }>(`${cfg.satelliteUrl}/api/browser-sync/exchange`, {
      method: 'POST',
      headers: { 'X-Sync-Api-Key': cfg.apiKey, 'Content-Type': 'application/json' },
      body: encrypted,
      credentials: 'omit',
    })

    // 3. Relay satellite's encrypted response back to main API for decryption and diff
    return api.post<SyncResult>('/browser-sync/process-exchange', raw)
  }

  async function importToMain(
    rfqs: SyncRfq[],
    nodeId: number
  ): Promise<{ created: number; updated: number }> {
    const cfg = await getConfig()

    // 1. Import into main DB — returns ID mappings for newly created records
    const result = await api.post<{
      created: number
      updated: number
      idMappings: { satelliteId: number; mainAppId: number; items: { satelliteId: number; mainAppId: number }[] }[]
    }>('/browser-sync/import', { nodeId, rfqs })

    // 2. If any new records were created, push MainAppId back to satellite so next sync
    //    can match them instead of duplicating. Main API encrypts; browser just relays.
    if (result.idMappings?.length) {
      const encryptedMappings = await api.post<{ cipherText: string; iv: string }>(
        '/browser-sync/encrypt-mappings',
        result.idMappings
      )
      await $fetch(`${cfg.satelliteUrl}/api/browser-sync/set-main-ids`, {
        method: 'POST',
        headers: { 'X-Sync-Api-Key': cfg.apiKey, 'Content-Type': 'application/json' },
        body: encryptedMappings,
        credentials: 'omit',
      })
    }

    return result
  }

  // For manual package import: ask main API to decrypt the satellite export package.
  async function decryptPackage(cipherText: string, iv: string): Promise<{ rfqs: SyncRfq[] }> {
    return api.post('/browser-sync/decrypt-package', { cipherText, iv })
  }

  return { syncWithSatellite, importToMain, getConfig, decryptPackage }
}
