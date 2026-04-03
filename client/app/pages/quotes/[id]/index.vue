<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/quotes" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Quote {{ quote.quoteNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Status Chip with Dropdown (admin only) -->
        <v-menu v-if="isAdmin" :disabled="isLocked">
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor(quote.status)"
              v-bind="menuProps"
              class="cursor-pointer"
              :append-icon="isLocked ? 'mdi-lock' : 'mdi-chevron-down'"
              size="default"
            >
              {{ quote.status || '—' }}
            </v-chip>
          </template>
          <v-list density="compact" style="min-width: 180px">
            <v-list-subheader>Change Status</v-list-subheader>
            <v-list-item
              v-for="s in statuses"
              :key="s.value"
              :value="s.value"
              :active="quote.status === s.value"
              @click="onStatusSelect(s.value)"
            >
              <template #prepend>
                <v-icon :icon="s.icon" :color="s.color" size="18" />
              </template>
              <v-list-item-title>{{ s.label }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
        <v-chip v-else :color="statusColor(quote.status)" size="default" :append-icon="isLocked ? 'mdi-lock' : undefined">{{ quote.status || '—' }}</v-chip>

        <v-btn v-if="quote.status !== 'Sent' && quote.status !== 'Accepted'" prepend-icon="mdi-pencil" variant="tonal" color="warning" size="small" @click="editQuote">Edit</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true">Perms</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true">Audit</v-btn>
        <v-btn v-if="isAdmin && quote.status === 'Accepted'" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
      </div>
    </div>

    <!-- Stat Cards -->
    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer">
          <template v-if="isAdmin">{{ quote.customerName }}<span v-if="quote.customerCode" class="text-medium-emphasis ml-1">({{ quote.customerCode }})</span></template>
          <template v-else>{{ quote.customerCode || '—' }}</template>
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ formatPrice(quote.totalAmount) }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-cash-check" color="info" label="Final Price">
          <span v-if="quote.finalPrice != null">${{ formatPrice(quote.finalPrice) }}</span>
          <span v-else class="text-medium-emphasis">—</span>
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar" color="warning" label="Valid Until"
          :value="quote.validUntil ? new Date(quote.validUntil).toLocaleDateString() : undefined"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-send-clock" color="info" label="Sent At"
          :value="quote.sentAt ? new Date(quote.sentAt).toLocaleDateString() : undefined"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-file-document-outline" color="info" label="RFQ">
          <nuxt-link v-if="quote.rfqId" :to="`/rfqs/${quote.rfqId}`" class="text-primary text-decoration-none">
            {{ quote.rfqName || `RFQ #${quote.rfqId}` }}
          </nuxt-link>
          <span v-else>—</span>
        </StatCard>
      </v-col>
    </v-row>

    <!-- Rejection Note -->
    <v-alert
      v-if="quote.status === 'Rejected' && quote.rejectionNote"
      type="error"
      variant="tonal"
      class="mb-6"
      icon="mdi-close-circle-outline"
    >
      <div class="font-weight-bold mb-1">Rejection Reason</div>
      {{ quote.rejectionNote }}
    </v-alert>

    <!-- All RFQ Items + Procurement Records -->
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center gap-2">
        RFQ Items
        <v-chip size="x-small" color="success" variant="tonal" class="ml-1">
          {{ selectedProcIds.size }} selected
        </v-chip>
      </v-card-title>
      <v-card-text class="pa-0">
        <div class="detail-table-wrap">
          <table class="detail-master-grid">
            <thead>
              <tr>
                <th style="width: 50px;">#</th>
                <th style="min-width: 140px;">Part Number</th>
                <th>Description</th>
                <th style="width: 70px;">Qty</th>
                <th style="width: 90px;">Condition</th>
                <th style="width: 100px;">Suppliers</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="(item, idx) in allRfqItems" :key="item.id">
                <!-- Master Row -->
                <tr class="master-row" :class="{ 'master-row-inactive': !itemHasSelection(item.id) }">
                  <td class="cell-number">{{ idx + 1 }}</td>
                  <td class="cell-pn" :class="{ 'cell-pn-inactive': !itemHasSelection(item.id) }">{{ item.partNumberName }}</td>
                  <td class="text-medium-emphasis" style="padding-left: 12px; font-size: 13px;">{{ item.description || '—' }}</td>
                  <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
                  <td style="padding-left: 12px; font-size: 13px;">{{ item.condition || 'N/A' }}</td>
                  <td class="cell-status">
                    <span :class="getProcRecords(item.id).length > 0 ? 'text-success' : 'text-medium-emphasis'">
                      {{ getProcRecords(item.id).length }} price{{ getProcRecords(item.id).length !== 1 ? 's' : '' }}
                    </span>
                  </td>
                </tr>

                <!-- Detail: All procurement records for this item -->
                <tr v-if="getProcRecords(item.id).length > 0" class="detail-sub-row">
                  <td :colspan="6" class="detail-sub-cell">
                    <div class="proc-panel">
                      <table class="proc-grid">
                        <thead>
                          <tr>
                            <th style="width: 28px;"></th>
                            <th style="min-width: 90px;">Supplier</th>
                            <th style="width: 120px;">Alt P/N</th>
                            <th style="width: 80px;">Cond</th>
                            <th style="width: 60px;">Qty</th>
                            <th style="width: 100px;">Buy Price</th>
                            <th style="width: 65px;">Coef 1</th>
                            <th style="width: 65px;">Coef 2</th>
                            <th style="width: 65px;">Coef 3</th>
                            <th style="width: 100px;">Unit Price</th>
                            <th style="width: 100px;">Total Price</th>
                            <th style="width: 80px;">Cert</th>
                            <th style="width: 90px;">Tag Date</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr
                            v-for="rec in getProcRecords(item.id)"
                            :key="rec.id"
                            class="proc-row"
                            :class="selectedProcIds.has(rec.id) ? 'selected-proc-row' : 'unselected-proc-row'"
                          >
                            <td class="text-center">
                              <v-icon
                                v-if="selectedProcIds.has(rec.id)"
                                icon="mdi-check-circle"
                                color="success"
                                size="16"
                              />
                              <v-icon
                                v-else
                                icon="mdi-circle-outline"
                                color="grey"
                                size="16"
                              />
                            </td>
                            <td style="padding-left: 8px; font-size: 13px;">{{ rec.supplierName }}</td>
                            <td style="padding-left: 8px; font-size: 12px; color: #fbbf24;">{{ rec.alt || '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.condition || 'N/A' }}</td>
                            <td class="text-center" style="font-size: 13px;">{{ rec.qty }}</td>
                            <td style="font-family: monospace; text-align: right; padding-right: 10px; font-size: 13px;" class="text-medium-emphasis">
                              ${{ formatPrice(rec.price) }}
                            </td>
                            <td class="text-center mono-cell">{{ rec.coef_1 ?? '—' }}</td>
                            <td class="text-center mono-cell">{{ rec.coef_2 ?? '—' }}</td>
                            <td class="text-center mono-cell">{{ rec.coef_3 ?? '—' }}</td>
                            <td class="mono-cell text-right pr-2">
                              ${{ formatPrice(rec.unitPrice) }}
                            </td>
                            <td class="mono-cell text-right pr-2" :class="selectedProcIds.has(rec.id) ? 'total-selected' : ''">
                              ${{ formatPrice(rec.totalPrice) }}
                            </td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.certName || '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.tagDate || '—' }}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                </tr>
                <tr v-else class="detail-sub-row">
                  <td :colspan="6" class="detail-sub-cell">
                    <div class="proc-panel empty-proc">
                      <span class="text-caption text-medium-emphasis">No procurement records for this item.</span>
                    </div>
                  </td>
                </tr>
              </template>

              <tr v-if="!allRfqItems.length && !loading">
                <td :colspan="6" class="text-center pa-8">
                  <v-icon icon="mdi-file-document-outline" size="48" color="grey-darken-1" class="mb-3" />
                  <p class="text-body-2 text-medium-emphasis">No RFQ items found</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <v-dialog v-model="showPermissions" max-width="600">
      <PermissionManager :entity-name="'Quote'" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-dialog v-model="showAudit" max-width="700">
      <AuditLogViewer :entity-name="'Quote'" :entity-id="route.params.id as string" />
    </v-dialog>

    <QuotePdfGenerator v-model="showPdf" :quote="quote" />

    <!-- Rejection Note Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="450" persistent>
      <v-card>
        <v-card-title class="text-h6">Reject Quote</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">Please provide a reason for rejecting this quote:</p>
          <v-textarea
            v-model="rejectionNote"
            label="Rejection Reason"
            variant="outlined"
            rows="3"
            auto-grow
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" @click="confirmReject">Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const api = useApi()
const authStore = useAuthStore()
const { statusColor } = useStatusColor()

const quote = ref<any>({})
const allRfqItems = ref<any[]>([])
const allProcRecords = ref<any[]>([])
const selectedProcIds = ref(new Set<number>())
const loading = ref(true)

const showPermissions = ref(false)
const showAudit = ref(false)
const showPdf = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const isAdmin = computed(() => authStore.isAdmin)

const displayedItems = computed(() => {
  if (!showAllItems.value) return quote.value.items || []
  // Merge quote items with all RFQ items, marking unselected ones
  const quoteItemIds = new Set((quote.value.items || []).map((i: any) => i.rfqItemId))
  const allItems = [...(quote.value.items || [])]
  allRfqItems.value.forEach((rfqItem: any) => {
    if (!quoteItemIds.has(rfqItem.id)) {
      allItems.push({
        rfqItemId: rfqItem.id,
        rfqRef: rfqItem.rfqRef,
        partNumberName: rfqItem.partNumberName,
        alt: rfqItem.alt,
        condition: rfqItem.condition,
        qty: rfqItem.qty,
        supplierName: null,
        buyPrice: null,
        unitPrice: null,
        totalPrice: null,
        shippingCost: null,
        isUnselected: true,
      })
    }
  })
  return allItems.sort((a: any, b: any) => {
    const aRef = typeof a.rfqRef === 'number' ? a.rfqRef : Infinity
    const bRef = typeof b.rfqRef === 'number' ? b.rfqRef : Infinity
    return aRef - bRef
  })
})

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('quote', entityId)

const statuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Accepted', label: 'Accepted', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle', color: 'error' },
]

onMounted(async () => {
  await loadQuote()
  await checkLock()
})

async function loadQuote() {
  loading.value = true
  try {
    const q = await api.get<any>(`/quotes/${route.params.id}`)

    if (q.rfqId) {
      try {
        const [rfq, procRecords] = await Promise.all([
          api.get<any>(`/rfqs/${q.rfqId}`),
          api.get<any[]>(`/rfqs/${q.rfqId}/supplier-quotes`)
        ])
        q.rfqName = rfq.name || `RFQ #${q.rfqId}`

        // Build RFQ items list with enrichment
        allRfqItems.value = (rfq.items || []).map((item: any) => ({
          id: item.id,
          partNumberName: item.partNumberName,
          description: item.description || '',
          qty: item.qty,
          condition: item.condition || '',
        }))

        // Store all procurement records
        allProcRecords.value = procRecords || []

        // Build the set of selected procurement record IDs from quote items
        const ids = new Set<number>()
        ;(q.items || []).forEach((qi: any) => {
          if (qi.procumentRecordId) ids.add(qi.procumentRecordId)
        })
        selectedProcIds.value = ids

      } catch {
        // RFQ fetch failed, continue without enrichment
      }
    }

    quote.value = q
  } catch {
    showSnack('Failed to load quote', 'error')
  } finally {
    loading.value = false
  }
}

function getProcRecords(rfqItemId: number) {
  return allProcRecords.value.filter(r => r.rfqItemId === rfqItemId)
}

function itemHasSelection(rfqItemId: number) {
  return getProcRecords(rfqItemId).some(r => selectedProcIds.value.has(r.id))
}

const showRejectDialog = ref(false)
const rejectionNote = ref('')

function onStatusSelect(newStatus: string) {
  if (newStatus === quote.value.status) return
  if (newStatus === 'Rejected') {
    rejectionNote.value = ''
    showRejectDialog.value = true
    return
  }
  changeStatus(newStatus)
}

async function confirmReject() {
  showRejectDialog.value = false
  await changeStatus('Rejected', rejectionNote.value || undefined)
}

async function changeStatus(newStatus: string, note?: string) {
  try {
    await api.patch(`/quotes/${route.params.id}/status`, { status: newStatus, rejectionNote: note || null })
    quote.value.status = newStatus
    quote.value.rejectionNote = note || null
    showSnack(`Status changed to ${newStatus}`, 'success')
  } catch {
    showSnack('Failed to change status', 'error')
  }
}

function editQuote() {
  if (quote.value.rfqId) {
    router.push(`/rfqs/${quote.value.rfqId}/create-quote?editQuoteId=${route.params.id}`)
  } else {
    showSnack('No RFQ linked to this quote', 'warning')
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
.detail-table-wrap {
  overflow-x: auto;
}

.detail-master-grid {
  width: 100%;
  border-collapse: collapse;
  min-width: 700px;
}

.detail-master-grid thead th {
  background: var(--toolbar-bg, rgba(0,0,0,0.04));
  color: rgba(var(--v-theme-on-surface), 0.6);
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid var(--excel-border, rgba(0,0,0,0.08));
  text-align: left;
  white-space: nowrap;
}

.detail-master-grid tbody td {
  padding: 0 12px;
  height: 40px;
  border-bottom: 1px solid var(--card-border, rgba(0,0,0,0.08));
  vertical-align: middle;
}

/* Master rows */
.master-row {
  background: var(--toolbar-bg, rgba(0,0,0,0.02));
}
.master-row-inactive {
  opacity: 0.5;
  font-style: italic;
}

.cell-number {
  text-align: center;
  opacity: 0.5;
  font-size: 12px;
}
.cell-pn {
  color: var(--pn-color, #60a5fa);
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}
.cell-pn-inactive {
  color: var(--v-medium-emphasis-opacity, rgba(0,0,0,0.38)) !important;
}
.cell-status {
  font-size: 12px;
  font-style: italic;
}

/* Detail sub-row */
.detail-sub-row {}
.detail-sub-cell {
  padding: 0 !important;
  background: var(--excel-bg, rgba(0,0,0,0.01));
  border-bottom: 2px solid var(--card-hover-border, rgba(0,0,0,0.12)) !important;
}

/* Procurement panel */
.proc-panel {
  padding: 10px 16px 10px 48px;
  border-left: 3px solid #3b82f6;
  margin-left: 16px;
}
.empty-proc {
  padding: 10px 16px 10px 48px;
}

/* Procurement grid */
.proc-grid {
  width: 100%;
  border-collapse: collapse;
}
.proc-grid thead th {
  opacity: 0.55;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 5px 8px;
  border-bottom: 1px solid var(--card-border, rgba(0,0,0,0.08));
  text-align: left;
  white-space: nowrap;
}
.proc-grid tbody td {
  padding: 3px 4px;
  height: 36px;
  border-bottom: 1px solid var(--card-border, rgba(0,0,0,0.06));
  vertical-align: middle;
}

.proc-row {
  transition: background-color 0.15s;
}

/* Selected: green highlight */
.selected-proc-row {
  background: rgba(74, 222, 128, 0.10);
  border-left: 3px solid #4ade80;
}
.selected-proc-row:hover {
  background: rgba(74, 222, 128, 0.16);
}

/* Unselected: muted */
.unselected-proc-row {
  opacity: 0.45;
}
.unselected-proc-row:hover {
  opacity: 0.65;
  background: var(--row-hover);
}

.mono-cell {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 12px;
  opacity: 0.75;
}
.total-selected {
  color: #4ade80 !important;
  font-weight: 600;
  opacity: 1 !important;
}
.text-right { text-align: right; }
.pr-2 { padding-right: 8px !important; }
.text-center { text-align: center; }
</style>
