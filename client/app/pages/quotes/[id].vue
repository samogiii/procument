<template>
  <div>
    <div class="d-flex align-center mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/quotes" class="mr-2" />
      <h1 class="text-h5 font-weight-bold">Quote {{ quote.quoteNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex align-center gap-2">
        <!-- Status Chip with Menu -->
        <v-menu>
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor"
              v-bind="menuProps"
              class="cursor-pointer"
              append-icon="mdi-chevron-down"
              size="default"
            >
              {{ quote.status || '—' }}
            </v-chip>
          </template>
          <v-list density="compact" class="status-menu">
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

        <v-btn
          class="mx-1" prepend-icon="mdi-pencil"
          variant="tonal"
          color="warning"
          size="small"
          @click="editQuote"
        >
          Edit Quote
        </v-btn>

        <v-btn class="mx-1" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true" v-if="isAdmin">Permission</v-btn>
        <v-btn class="mx-1" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true" v-if="isAdmin">Audit</v-btn>
        <v-btn class="mx-1" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">Generate PDF</v-btn>
      </div>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Customer</p>
          <p class="text-body-1 font-weight-medium">{{ quote.customerName || '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Total Amount</p>
          <p class="text-body-1 font-weight-medium">${{ quote.totalAmount?.toLocaleString() || '0' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Valid Until</p>
          <p class="text-body-1 font-weight-medium">{{ quote.validUntil ? new Date(quote.validUntil).toLocaleDateString() : '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">RFQ</p>
          <p class="text-body-1 font-weight-medium">
            <nuxt-link v-if="quote.rfqId" :to="`/rfqs/${quote.rfqId}`" class="text-primary text-decoration-none">
              {{ quote.rfqName || `RFQ #${quote.rfqId}` }}
            </nuxt-link>
            <span v-else>—</span>
          </p>
        </v-card>
      </v-col>
    </v-row>

    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="quote.items || []" density="comfortable" />
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

const statusColor = computed(() => {
  const map: Record<string, string> = { Draft: 'grey', Sent: 'info', Accepted: 'success', Rejected: 'error' }
  return map[quote.value.status] || 'grey'
})

const itemHeaders = [
  { title: 'Part', key: 'partNumberName' },
  { title: 'Qty', key: 'qty' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
  { title: 'Condition', key: 'condition' },
]

onMounted(async () => {
  await loadQuote()
})

async function loadQuote() {
  try {
    quote.value = await api.get(`/quotes/${route.params.id}`)
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
  // Navigate to create-quote page for this RFQ, passing the quote id as a query param so we can pre-fill
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
.cursor-pointer { cursor: pointer; }
.status-menu {
  min-width: 180px !important;
}
</style>
