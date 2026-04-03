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

    <!-- Line Items -->
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        Line Items
        <v-spacer />
        <v-switch
          v-if="isAdmin && allRfqItems.length > 0"
          v-model="showAllItems"
          label="Show all RFQ items (including unselected)"
          color="primary"
          hide-details
          density="compact"
          class="mr-4"
        />
      </v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="displayedItems" density="comfortable" :items-per-page="50">
          <template #item.alt="{ item: row }">
            <span v-if="(row as any).alt" style="color: #fbbf24;">{{ (row as any).alt }}</span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.shippingCost="{ item: row }">
            <span v-if="(row as any).shippingCost != null" class="text-medium-emphasis">${{ formatPrice((row as any).shippingCost) }}</span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.buyPrice="{ item: row }">
            <span v-if="(row as any).buyPrice != null" class="text-medium-emphasis">${{ formatPrice((row as any).buyPrice) }}</span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.supplierName="{ item: row }">
            <span v-if="(row as any).supplierName">{{ (row as any).supplierName }}</span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.unitPrice="{ item: row }">
            ${{ formatPrice((row as any).unitPrice) }}
          </template>
          <template #item.totalPrice="{ item: row }">
            <strong :class="(row as any).isUnselected ? 'text-medium-emphasis' : ''" :style="(row as any).isUnselected ? '' : 'color: #4ade80;'">${{ formatPrice((row as any).totalPrice) }}</strong>
          </template>
          <template #tfoot="{ items }">
            <tr v-if="isAdmin && items.length > 0" class="totals-row">
              <td colspan="6" class="text-right font-weight-bold pr-4" style="padding: 12px;">Totals:</td>
              <td class="font-weight-bold" style="padding: 12px; color: #fb923c;">
                ${{ formatPrice(items.reduce((sum: number, i: any) => sum + (Number(i.buyPrice) || 0), 0)) }}
                <div class="text-caption text-medium-emphasis">Total Buy</div>
              </td>
              <td class="font-weight-bold" style="padding: 12px; color: #4ade80;">
                ${{ formatPrice(items.reduce((sum: number, i: any) => sum + (Number(i.unitPrice) || 0), 0)) }}
                <div class="text-caption text-medium-emphasis">Total Unit</div>
              </td>
              <td class="font-weight-bold" style="padding: 12px; color: #60a5fa;">
                ${{ formatPrice(items.reduce((sum: number, i: any) => sum + (Number(i.totalPrice) || 0), 0)) }}
                <div class="text-caption text-medium-emphasis">Total Sell</div>
              </td>
              <td class="font-weight-bold" style="padding: 12px; color: #a78bfa;">
                ${{ formatPrice(items.reduce((sum: number, i: any) => sum + (Number(i.shippingCost) || 0), 0)) }}
                <div class="text-caption text-medium-emphasis">Total Ship</div>
              </td>
            </tr>
          </template>
        </v-data-table>
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
const showAllItems = ref(false)
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

const itemHeaders = [
  { title: 'Ref.', key: 'rfqRef', width: '60px' },
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Alt P/N', key: 'alt' },
  { title: 'Condition', key: 'condition' },
  { title: 'Qty', key: 'qty' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Buy Price', key: 'buyPrice' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total Price', key: 'totalPrice' },
  { title: 'Shipping', key: 'shippingCost' },
]

onMounted(async () => {
  await loadQuote()
  await checkLock()
})

async function loadQuote() {
  try {
    const q = await api.get<any>(`/quotes/${route.params.id}`)

    // Fetch the RFQ and procurement records to enrich quote items
    if (q.rfqId) {
      try {
        const [rfq, procRecords] = await Promise.all([
          api.get<any>(`/rfqs/${q.rfqId}`),
          api.get<any[]>(`/rfqs/${q.rfqId}/supplier-quotes`)
        ])
        q.rfqName = rfq.name || `RFQ #${q.rfqId}`

        // Store all RFQ items for the "show all" toggle
        allRfqItems.value = (rfq.items || []).map((item: any, idx: number) => ({
          id: item.id,
          rfqRef: idx + 1,
          partNumberName: item.partNumberName,
          alt: item.alt,
          condition: item.condition,
          qty: item.qty,
        }))

        // Build a map of rfqItemId → row index (1-based) and rfqItemId → description
        const refMap: Record<number, number> = {}
        const descMap: Record<number, string> = {}
        ;(rfq.items || []).forEach((item: any, idx: number) => {
          refMap[item.id] = idx + 1
          descMap[item.id] = item.description || ''
        })

        // Build a map of procurementRecordId → record details
        const procMap: Record<number, any> = {}
        ;(procRecords || []).forEach((r: any) => {
          procMap[r.id] = r
        })

        // Enrich quote items and sort by RFQ ref (1, 2, 3…)
        if (q.items) {
          q.items = q.items.map((qi: any) => {
            const proc = qi.procumentRecordId ? procMap[qi.procumentRecordId] : null
            return {
              ...qi,
              rfqRef: qi.rfqItemId ? refMap[qi.rfqItemId] || '—' : '—',
              description: qi.rfqItemId ? descMap[qi.rfqItemId] || '' : '',
              shippingCost: proc?.shippingCost ?? null,
              certName: proc?.certName || null,
              tagDate: proc?.tagDate || null,
              note: proc?.note || '',
            }
          }).sort((a: any, b: any) => {
            const aRef = typeof a.rfqRef === 'number' ? a.rfqRef : Infinity
            const bRef = typeof b.rfqRef === 'number' ? b.rfqRef : Infinity
            return aRef - bRef
          })
        }
      } catch {
        // RFQ fetch failed, continue without enrichment
      }
    }

    quote.value = q
  } catch {
    showSnack('Failed to load quote', 'error')
  }
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
