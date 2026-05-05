<template>
  <v-container>
    <v-row>
      <v-col cols="12">
        <h1 class="text-h4 mb-1">Browser Sync with Satellite</h1>
        <p class="text-body-2 text-medium-emphasis mb-4">
          Your browser acts as the bridge between this app (local network) and the satellite (internet).
          No server-to-server connection is needed.
        </p>
      </v-col>
    </v-row>

    <!-- Step 1: Sync -->
    <v-row>
      <v-col cols="12">
        <v-card>
          <v-card-title class="d-flex align-center">
            Step 1 — Push &amp; Pull
            <v-spacer />
            <v-btn
              color="primary"
              :loading="syncing"
              :disabled="syncing"
              prepend-icon="mdi-sync"
              @click="runSync"
            >
              Sync with Satellite
            </v-btn>
          </v-card-title>
          <v-card-text>
            <v-row align="center" class="mb-2">
              <v-col cols="12" sm="auto">
                <span class="text-body-2 text-medium-emphasis">Sync window:</span>
              </v-col>
              <v-col cols="12" sm="4" md="3">
                <v-select
                  v-model="syncWindow"
                  :items="syncWindowOptions"
                  item-title="label"
                  item-value="value"
                  density="compact"
                  hide-details
                  variant="outlined"
                />
              </v-col>
              <v-col v-if="syncWindow === 'custom'" cols="12" sm="5" md="4">
                <v-text-field
                  v-model="customSince"
                  label="Since (ISO date, UTC)"
                  placeholder="2025-01-01T00:00:00Z"
                  density="compact"
                  hide-details
                  variant="outlined"
                />
              </v-col>
            </v-row>

            <p class="text-body-2 text-medium-emphasis mb-3">
              Sends Base {{ baseNumber }} RFQs created or modified within the selected window to
              the satellite, and retrieves matching changes from the satellite.
            </p>

            <v-row v-if="lastResult">
              <v-col cols="6" md="3">
                <v-card variant="tonal" color="info">
                  <v-card-text class="text-center">
                    <div class="text-h5">{{ lastResult.totalSatelliteRfqs }}</div>
                    <div class="text-caption">Total in Satellite</div>
                  </v-card-text>
                </v-card>
              </v-col>
              <v-col cols="6" md="3">
                <v-card variant="tonal" color="success">
                  <v-card-text class="text-center">
                    <div class="text-h5">{{ lastResult.receivedCount }}</div>
                    <div class="text-caption">Satellite Accepted</div>
                  </v-card-text>
                </v-card>
              </v-col>
              <v-col cols="6" md="3">
                <v-card variant="tonal" color="warning">
                  <v-card-text class="text-center">
                    <div class="text-h5">{{ lastResult.newRfqs.length }}</div>
                    <div class="text-caption">New from Satellite</div>
                  </v-card-text>
                </v-card>
              </v-col>
              <v-col cols="6" md="3">
                <v-card variant="tonal" color="orange">
                  <v-card-text class="text-center">
                    <div class="text-h5">{{ lastResult.updatedRfqs.length }}</div>
                    <div class="text-caption">Updated in Satellite</div>
                  </v-card-text>
                </v-card>
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Step 2: Review & Import -->
    <v-row v-if="pendingImport.length > 0">
      <v-col cols="12">
        <v-card>
          <v-card-title class="d-flex align-center">
            Step 2 — Review &amp; Import to Main
            <v-chip class="ml-2" color="warning" size="small">{{ pendingImport.length }} RFQs</v-chip>
            <v-spacer />
            <v-btn
              color="success"
              :loading="importing"
              prepend-icon="mdi-database-import"
              @click="importAll"
            >
              Import All to Main
            </v-btn>
          </v-card-title>
          <v-card-text>
            <v-data-table
              :headers="rfqHeaders"
              :items="pendingImport"
              :items-per-page="20"
              show-select
              v-model="selectedForImport"
              item-value="id"
            >
              <template #[`item.syncType`]="{ item }">
                <v-chip
                  :color="item.mainAppId == null ? 'success' : 'orange'"
                  size="small"
                >
                  {{ item.mainAppId == null ? 'New' : 'Updated' }}
                </v-chip>
              </template>
              <template #[`item.updatedAt`]="{ item }">
                {{ item.updatedAt ? new Date(item.updatedAt).toLocaleString() : '—' }}
              </template>
              <template #[`item.itemCount`]="{ item }">
                {{ item.items?.length ?? 0 }}
              </template>
            </v-data-table>

            <div class="d-flex justify-end mt-2">
              <v-btn
                color="success"
                variant="outlined"
                :loading="importing"
                :disabled="selectedForImport.length === 0"
                prepend-icon="mdi-database-import"
                @click="importSelected"
              >
                Import Selected ({{ selectedForImport.length }})
              </v-btn>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Import encrypted package from satellite -->
    <v-row>
      <v-col cols="12">
        <v-expansion-panels>
          <v-expansion-panel title="Import Encrypted Package from Satellite">
            <v-expansion-panel-text>
              <p class="text-body-2 mb-3">
                If a satellite user exported a sync package, paste it here to review and import.
              </p>
              <v-row>
                <v-col cols="12" md="9">
                  <v-textarea
                    v-model="packageJson"
                    label="Paste exported package JSON here"
                    rows="4"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" md="3" class="d-flex align-center">
                  <v-btn color="primary" @click="decryptPackageInput" :loading="decryptingPackage">
                    Decrypt &amp; Preview
                  </v-btn>
                </v-col>
              </v-row>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </v-col>
    </v-row>

    <!-- Import result snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="6000">
      {{ snackbarText }}
    </v-snackbar>
  </v-container>
</template>

<script setup lang="ts">
import { useSatelliteSync } from '~/composables/useSatelliteSync'
import type { SyncRfq, SyncResult } from '~/composables/useSatelliteSync'

const { syncWithSatellite, importToMain, getConfig, decryptPackage } = useSatelliteSync()

const baseNumber = 2
const syncing = ref(false)
const importing = ref(false)
const decryptingPackage = ref(false)
const lastResult = ref<SyncResult | null>(null)
const pendingImport = ref<SyncRfq[]>([])
const selectedForImport = ref<number[]>([])
const packageJson = ref('')

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Sync window selector
const syncWindow = ref<string>('24h')
const customSince = ref('')
const syncWindowOptions = [
  { label: 'Last 24 hours', value: '24h' },
  { label: 'Last 3 days',   value: '3d' },
  { label: 'Last 7 days',   value: '7d' },
  { label: 'Last 30 days',  value: '30d' },
  { label: 'All time',      value: 'all' },
  { label: 'Custom date…',  value: 'custom' },
]

function resolvedSince(): string | undefined {
  const now = new Date()
  switch (syncWindow.value) {
    case '24h':   return new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString()
    case '3d':    return new Date(now.getTime() - 3  * 24 * 60 * 60 * 1000).toISOString()
    case '7d':    return new Date(now.getTime() - 7  * 24 * 60 * 60 * 1000).toISOString()
    case '30d':   return new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString()
    case 'all':   return undefined   // no filter — server sends everything
    case 'custom': return customSince.value || undefined
    default:      return undefined
  }
}

const rfqHeaders = [
  { title: 'Type',       key: 'syncType',     sortable: false },
  { title: 'Name',       key: 'name' },
  { title: 'Customer',   key: 'customerName' },
  { title: 'Status',     key: 'status' },
  { title: 'Items',      key: 'itemCount',    sortable: false },
  { title: 'Updated At', key: 'updatedAt' },
]

async function runSync() {
  syncing.value = true
  try {
    const result = await syncWithSatellite(baseNumber, resolvedSince())
    lastResult.value = result
    pendingImport.value = [...result.newRfqs, ...result.updatedRfqs]
    selectedForImport.value = pendingImport.value.map(r => r.id)

    if (pendingImport.value.length === 0) {
      showMsg('Sync complete — no new or updated RFQs from satellite.', 'info')
    } else {
      showMsg(`Sync complete. ${result.newRfqs.length} new, ${result.updatedRfqs.length} updated RFQs from satellite.`, 'success')
    }
  } catch (e: any) {
    showMsg(`Sync failed: ${e?.message ?? e}`, 'error')
  } finally {
    syncing.value = false
  }
}

async function importAll() {
  await doImport(pendingImport.value)
}

async function importSelected() {
  const rfqs = pendingImport.value.filter(r => selectedForImport.value.includes(r.id))
  await doImport(rfqs)
}

async function doImport(rfqs: SyncRfq[]) {
  if (rfqs.length === 0) return
  importing.value = true
  try {
    const cfg = await getConfig()
    const result = await importToMain(rfqs, cfg.defaultNodeId)
    showMsg(`Import done — ${result.created} created, ${result.updated} updated.`, 'success')
    pendingImport.value = pendingImport.value.filter(r => !rfqs.some(imported => imported.id === r.id))
    selectedForImport.value = []
  } catch (e: any) {
    showMsg(`Import failed: ${e?.message ?? e}`, 'error')
  } finally {
    importing.value = false
  }
}

async function decryptPackageInput() {
  decryptingPackage.value = true
  try {
    const pkg = JSON.parse(packageJson.value)
    // Decryption happens server-side — browser just relays cipherText + iv
    const payload = await decryptPackage(pkg.cipherText, pkg.iv)
    pendingImport.value = payload.rfqs as SyncRfq[]
    selectedForImport.value = (payload.rfqs as SyncRfq[]).map(r => r.id)
    showMsg(`Package decrypted — ${payload.rfqs.length} RFQs ready to import.`, 'success')
  } catch (e: any) {
    showMsg(`Failed to decrypt package: ${e?.message ?? e}`, 'error')
  } finally {
    decryptingPackage.value = false
  }
}

function showMsg(msg: string, color: string) {
  snackbarText.value = msg
  snackbarColor.value = color
  snackbar.value = true
}
</script>
