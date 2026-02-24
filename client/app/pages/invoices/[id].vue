<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/invoices" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Proforma Invoice {{ invoice.invoiceNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Status Chip with Dropdown -->
        <v-menu>
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor(invoice.status)"
              v-bind="menuProps"
              class="cursor-pointer"
              append-icon="mdi-chevron-down"
              size="default"
            >
              {{ invoice.status || '—' }}
            </v-chip>
          </template>
          <v-list density="compact" style="min-width: 140px">
             <v-list-item v-for="s in ['Pending', 'Paid', 'Overdue', 'Cancelled']" :key="s" :value="s" @click="updateStatus(s)">
               <v-list-item-title>{{ s }}</v-list-item-title>
             </v-list-item>
          </v-list>
        </v-menu>

        <v-btn v-if="isAdmin" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true">Perms</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true">Audit</v-btn>
      </div>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer" :value="invoice.customerName" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ invoice.totalAmount?.toLocaleString() || '0' }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-clock" color="warning" label="Due Date"
          :value="invoice.dueDate ? new Date(invoice.dueDate).toLocaleDateString() : '—'"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-check" color="info" label="Paid Date"
          :value="invoice.paidDate ? new Date(invoice.paidDate).toLocaleDateString() : 'Unpaid'"
        />
      </v-col>
    </v-row>

    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="invoice.items || []" density="comfortable">
          <template #item.expectedDeliveryDate="{ item }: { item: any }">
            {{ item.expectedDeliveryDate ? new Date(item.expectedDeliveryDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.unitPrice="{ item }: { item: any }">
            ${{ item.unitPrice?.toFixed(2) }}
          </template>
          <template #item.totalPrice="{ item }: { item: any }">
            ${{ item.totalPrice?.toFixed(2) }}
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <v-dialog v-model="showPermissions" max-width="600">
      <PermissionManager :entity-name="'Invoice'" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-dialog v-model="showAudit" max-width="700">
      <AuditLogViewer :entity-name="'Invoice'" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const { statusColor } = useStatusColor()

const invoice = ref<any>({})
const showPermissions = ref(false)
const showAudit = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const isAdmin = computed(() => authStore.isAdmin)

const itemHeaders = [
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Description', key: 'description' },
  { title: 'Qty', key: 'qty' },
  { title: 'Lead Time', key: 'expectedDeliveryDate' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
]

onMounted(async () => {
  await loadInvoice()
})

async function loadInvoice() {
  try {
    invoice.value = await api.get(`/invoices/${route.params.id}`)
  } catch {
    showSnack('Failed to load proforma invoice', 'error')
  }
}

async function updateStatus(newStatus: string) {
  try {
    await api.patch(`/invoices/${route.params.id}/status`, { status: newStatus })
    invoice.value.status = newStatus
    showSnack(`Status updated to ${newStatus}`, 'success')
  } catch {
    showSnack('Failed to update status', 'error')
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>
