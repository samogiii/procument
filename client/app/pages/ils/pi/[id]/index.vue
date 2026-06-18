<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo('/ils?tab=pi')" />
      <div>
        <h1 class="text-h5 font-weight-bold">{{ pi?.piNumber || 'Proforma Invoice' }}</h1>
        <p class="text-caption text-medium-emphasis mb-0">ILS Proforma Invoice</p>
      </div>
      <v-spacer />
      <v-chip v-if="pi" :color="statusColor(pi.status)" variant="tonal" size="small" class="mr-2">{{ pi.status }}</v-chip>
      <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-file-pdf-box" :disabled="!pi" @click="showPdf = true">PDF</v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-row v-if="pi" class="mb-4">
      <v-col cols="12" md="6">
        <v-card class="glass-card h-100">
          <v-card-text>
            <div class="text-caption text-medium-emphasis mb-3 font-weight-bold" style="text-transform: uppercase; letter-spacing: 0.08em;">PI Info</div>
            <div class="info-grid">
              <span class="info-label">PI Number</span>
              <span class="info-value font-weight-bold" style="font-family: monospace;">{{ pi.piNumber }}</span>
              <span class="info-label">Status</span>
              <v-chip :color="statusColor(pi.status)" variant="tonal" size="x-small">{{ pi.status }}</v-chip>
              <span v-if="pi.subject" class="info-label">Subject</span>
              <span v-if="pi.subject" class="info-value">{{ pi.subject }}</span>
              <span v-if="pi.customerPONumber" class="info-label">Customer PO #</span>
              <span v-if="pi.customerPONumber" class="info-value">{{ pi.customerPONumber }}</span>
              <span class="info-label">Total</span>
              <span class="info-value font-weight-bold" style="font-family: monospace; color: #4ade80;">${{ formatPrice(pi.totalAmount) }}</span>
              <span class="info-label">Created</span>
              <span class="info-value">{{ new Date(pi.createdAt).toLocaleDateString() }}</span>
              <span v-if="pi.notes" class="info-label">Notes</span>
              <span v-if="pi.notes" class="info-value text-medium-emphasis" style="white-space: pre-wrap;">{{ pi.notes }}</span>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="6">
        <v-card class="glass-card h-100">
          <v-card-text>
            <div class="text-caption text-medium-emphasis mb-3 font-weight-bold" style="text-transform: uppercase; letter-spacing: 0.08em;">Customer</div>
            <div class="info-grid">
              <span class="info-label">Name</span>
              <span class="info-value font-weight-bold">{{ pi.ilsCustomerName }}</span>
              <span v-if="pi.billTo" class="info-label">Bill To</span>
              <span v-if="pi.billTo" class="info-value text-medium-emphasis" style="white-space: pre-wrap;">{{ pi.billTo }}</span>
              <span v-if="pi.shipTo" class="info-label">Ship To</span>
              <span v-if="pi.shipTo" class="info-value text-medium-emphasis" style="white-space: pre-wrap;">{{ pi.shipTo }}</span>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-if="pi" class="glass-card">
      <v-card-text>
        <div class="text-caption text-medium-emphasis mb-3 font-weight-bold" style="text-transform: uppercase; letter-spacing: 0.08em;">
          Items ({{ pi.items?.length || 0 }})
        </div>
        <div class="detail-table-wrap">
          <table class="detail-table">
            <thead>
              <tr>
                <th style="width: 40px;">#</th>
                <th style="min-width: 140px;">Part Number</th>
                <th style="width: 120px;">Alt P/N</th>
                <th style="width: 140px;">Serial #</th>
                <th style="width: 90px;">Condition</th>
                <th style="width: 80px;">Cert</th>
                <th style="width: 60px;">Qty</th>
                <th style="width: 110px;">Sell Price</th>
                <th style="width: 120px;">Total Price</th>
                <th style="width: 110px;">Lead Time</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, i) in pi.items" :key="item.id">
                <td class="text-center text-medium-emphasis text-caption">{{ Number(i) + 1 }}</td>
                <td class="font-weight-bold" style="font-family: monospace; padding-left: 8px;">{{ item.partNumberName }}</td>
                <td style="padding-left: 8px; color: #fbbf24; font-size: 12px;">{{ item.altPartNumber || '—' }}</td>
                <td style="padding-left: 8px; font-family: monospace; font-size: 12px;">{{ item.serialNumber || '—' }}</td>
                <td style="padding-left: 8px;">
                  <v-chip v-if="item.condition" size="x-small" variant="tonal" :color="conditionColor(item.condition)">{{ item.condition }}</v-chip>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td style="padding-left: 8px; font-size: 12px;">{{ item.certName || '—' }}</td>
                <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
                <td class="text-right" style="font-family: monospace; padding-right: 12px; font-size: 13px;">${{ formatPrice(item.sellPrice) }}</td>
                <td class="text-right font-weight-bold" style="font-family: monospace; padding-right: 12px; font-size: 13px; color: #4ade80;">${{ formatPrice(item.totalPrice) }}</td>
                <td style="padding-left: 8px; font-size: 12px;">{{ item.leadTime || '—' }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr>
                <td colspan="7"></td>
                <td class="text-right text-caption font-weight-bold" style="padding-right: 4px; border-top: 1px solid rgba(255,255,255,0.1);">TOTAL:</td>
                <td class="text-right font-weight-bold" style="font-family: monospace; padding-right: 12px; font-size: 14px; color: #4ade80; border-top: 1px solid rgba(255,255,255,0.1);">${{ formatPrice(pi.totalAmount) }}</td>
                <td style="border-top: 1px solid rgba(255,255,255,0.1);"></td>
              </tr>
            </tfoot>
          </table>
        </div>
      </v-card-text>
    </v-card>

    <!-- Status Actions -->
    <div v-if="pi && isAdmin" class="d-flex gap-2 mt-4 justify-end">
      <v-btn v-if="pi.status === 'Open'" color="success" variant="tonal" size="small" prepend-icon="mdi-check" :loading="statusUpdating" @click="updateStatus('Paid')">Mark Paid</v-btn>
      <v-btn v-if="pi.status !== 'Cancelled'" color="error" variant="tonal" size="small" prepend-icon="mdi-close" :loading="statusUpdating" @click="updateStatus('Cancelled')">Cancel PI</v-btn>
      <v-btn v-if="pi.status === 'Cancelled'" color="info" variant="tonal" size="small" prepend-icon="mdi-restore" :loading="statusUpdating" @click="updateStatus('Open')">Reopen</v-btn>
    </div>

    <IlsQuotePdfGenerator v-if="pi" v-model="showPdf" :quote="pi" title="PROFORMA INVOICE" />

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const id = computed(() => route.params.id as string)

const pi = ref<any>(null)
const loading = ref(true)
const showPdf = ref(false)
const statusUpdating = ref(false)

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text; snackbarColor.value = color; snackbar.value = true
}

function formatPrice(val: any) {
  return (Number(val) || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function statusColor(status: string) {
  const map: Record<string, string> = { Open: 'info', Paid: 'success', Cancelled: 'error' }
  return map[status] || 'grey'
}

function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error', RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

async function loadPI() {
  loading.value = true
  try {
    pi.value = await api.get<any>(`/ils-proforma/${id.value}`)
  } catch {
    showSnack('Failed to load PI', 'error')
  } finally {
    loading.value = false
  }
}

async function updateStatus(status: string) {
  statusUpdating.value = true
  try {
    pi.value = await api.patch<any>(`/ils-proforma/${id.value}/status`, { status })
    showSnack(`PI marked ${status}`)
  } catch {
    showSnack('Failed to update status', 'error')
  } finally {
    statusUpdating.value = false
  }
}

onMounted(loadPI)
</script>

<style scoped>
.info-grid { display: grid; grid-template-columns: 120px 1fr; gap: 8px 12px; align-items: center; }
.info-label { font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.55); font-weight: 500; }
.info-value { font-size: 13px; }
.detail-table-wrap { overflow-x: auto; }
.detail-table { width: 100%; border-collapse: collapse; min-width: 1000px; }
.detail-table thead th {
  opacity: 0.55; font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;
  padding: 6px 8px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1); text-align: left;
}
.detail-table tbody td { padding: 8px 6px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05); font-size: 13px; }
.detail-table tbody tr:hover td { background: rgba(var(--v-theme-surface-variant), 0.3); }
.detail-table tfoot td { padding: 10px 6px; }
</style>
