<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/quotes" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Quote {{ quote.quoteNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Status Chip with Dropdown -->
        <v-menu>
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor(quote.status)"
              v-bind="menuProps"
              class="cursor-pointer"
              append-icon="mdi-chevron-down"
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
              @click="changeStatus(s.value)"
            >
              <template #prepend>
                <v-icon :icon="s.icon" :color="s.color" size="18" />
              </template>
              <v-list-item-title>{{ s.label }}</v-list-item-title>
            </v-list-item>
          </v-list>
      </v-menu>
        <v-btn prepend-icon="mdi-pencil" variant="tonal" color="warning" size="small" @click="editQuote">Edit</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true">Perms</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true">Audit</v-btn>
        <v-btn prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
      </div>
    </div>

    <!-- Stat Cards -->
    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer" :value="quote.customerName" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ quote.totalAmount?.toLocaleString() || '0' }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar" color="warning" label="Valid Until"
          :value="quote.validUntil ? new Date(quote.validUntil).toLocaleDateString() : undefined"
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

    <!-- Line Items -->
    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="quote.items || []" density="comfortable">
          <template #item.alt="{ item: row }">
            <span v-if="(row as any).alt" style="color: #fbbf24;">{{ (row as any).alt }}</span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.unitPrice="{ item: row }">
            ${{ (row as any).unitPrice?.toFixed(2) || '0.00' }}
          </template>
          <template #item.totalPrice="{ item: row }">
            <strong style="color: #4ade80;">${{ (row as any).totalPrice?.toFixed(2) || '0.00' }}</strong>
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
const showPermissions = ref(false)
const showAudit = ref(false)
const showPdf = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const isAdmin = computed(() => authStore.isAdmin)

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
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total Price', key: 'totalPrice' },
]

onMounted(() => loadQuote())

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

        // Enrich quote items
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
            }
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

async function changeStatus(newStatus: string) {
  if (newStatus === quote.value.status) return
  try {
    await api.patch(`/quotes/${route.params.id}/status`, { status: newStatus })
    quote.value.status = newStatus
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
