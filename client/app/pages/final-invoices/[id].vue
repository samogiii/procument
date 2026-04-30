<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/final-invoices" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Invoice {{ inv.invoiceNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <v-menu v-if="isAdmin">
        <template #activator="{ props: menuProps }">
          <v-chip :color="statusColor(inv.status)" v-bind="menuProps" class="cursor-pointer" append-icon="mdi-chevron-down" size="default">
            {{ inv.status || '—' }}
          </v-chip>
        </template>
        <v-list density="compact" style="min-width: 160px">
          <v-list-subheader>Change Status</v-list-subheader>
          <v-list-item v-for="s in statuses" :key="s.value" :value="s.value" :active="inv.status === s.value" @click="changeStatus(s.value)">
            <template #prepend><v-icon :icon="s.icon" :color="s.color" size="18" /></template>
            <v-list-item-title>{{ s.label }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
      <v-chip v-else :color="statusColor(inv.status)" size="default">{{ inv.status || '—' }}</v-chip>
      <v-btn v-if="isAdmin" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account" color="primary" label="Customer" :value="inv.customerCode" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ formatPrice(inv.totalAmount) }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-file-document-outline" color="info" label="Proforma Ref" :value="inv.proformaInvoiceNumber || '—'" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-truck-delivery" color="warning" label="Shipping Method" :value="inv.shippingMethod || '—'" />
      </v-col>
    </v-row>

    <!-- Shipping & Details -->
    <v-card class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-truck-fast" class="mr-2" size="20" />
        Shipping & Details
        <v-spacer />
        <v-btn v-if="!editingDetails" variant="tonal" size="small" prepend-icon="mdi-pencil" @click="editingDetails = true">Edit</v-btn>
        <template v-else>
          <v-btn variant="text" size="small" class="mr-1" @click="cancelDetailsEdit">Cancel</v-btn>
          <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-content-save" :loading="savingDetails" @click="saveDetails">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="3">
            <v-select v-model="detailsForm.shippingMethod" :items="['Air', 'Sea', 'Ground', 'Express']" label="Shipping Method" variant="outlined" density="compact" hide-details :readonly="!editingDetails" clearable />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model.number="detailsForm.shippingCost" label="Shipping Cost" variant="outlined" density="compact" hide-details type="number" prefix="$" :readonly="!editingDetails" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="detailsForm.dueDate" label="Due Date" variant="outlined" density="compact" hide-details type="date" :readonly="!editingDetails" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field :model-value="inv.paidDate ? new Date(inv.paidDate).toLocaleDateString() : '—'" label="Paid Date" variant="outlined" density="compact" hide-details readonly />
          </v-col>
          <v-col cols="12">
            <v-textarea v-model="detailsForm.notes" label="Notes" variant="outlined" density="compact" hide-details rows="2" auto-grow :readonly="!editingDetails" />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- Items Table -->
    <v-card class="glass-card mb-6">
      <v-card-title>
        <v-icon icon="mdi-format-list-bulleted" class="mr-2" size="20" />
        Items
      </v-card-title>
      <v-data-table
        :headers="itemHeaders"
        :items="inv.items || []"
        density="comfortable"
        :items-per-page="50"
        class="rounded-b-lg"
      >
        <template #item.unitPrice="{ item }: { item: any }">
          ${{ formatPrice(item.unitPrice) }}
        </template>
        <template #item.totalPrice="{ item }: { item: any }">
          ${{ formatPrice(item.totalPrice) }}
        </template>
      </v-data-table>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>

    <FinalInvoicePdfGenerator v-model="showPdf" :invoice-id="String(route.params.id)" />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const inv = ref<any>({})
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const showPdf = ref(false)

const isAdmin = computed(() => authStore.isAdmin)
const { statusColor } = useStatusColor()

const statuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Paid', label: 'Paid', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Cancelled', label: 'Cancelled', icon: 'mdi-cancel', color: 'grey' },
]

const itemHeaders = [
  { title: '#', key: 'index', sortable: false },
  { title: 'Part Number', key: 'partNumberName', sortable: true },
  { title: 'Description', key: 'description', sortable: false },
  { title: 'Qty', key: 'qty', sortable: true },
  { title: 'Condition', key: 'condition', sortable: false },
  { title: 'Certification', key: 'certName', sortable: false },
  { title: 'Unit Price', key: 'unitPrice', sortable: true },
  { title: 'Total', key: 'totalPrice', sortable: true },
  { title: 'Track Number', key: 'trackNumber', sortable: false },
  { title: 'Carrier', key: 'carrier', sortable: false },
]

// Shipping & Details editing
const editingDetails = ref(false)
const savingDetails = ref(false)
const detailsForm = ref<any>({ shippingMethod: '', shippingCost: 0, notes: '', dueDate: '' })
const detailsOriginal = ref<any>({})

function cancelDetailsEdit() {
  detailsForm.value = { ...detailsOriginal.value }
  editingDetails.value = false
}

async function saveDetails() {
  savingDetails.value = true
  try {
    await api.put(`/final-invoices/${route.params.id}`, detailsForm.value)
    detailsOriginal.value = { ...detailsForm.value }
    editingDetails.value = false
    showSnack('Details saved', 'success')
    await loadInvoice()
  } catch { showSnack('Failed to save details', 'error') }
  finally { savingDetails.value = false }
}

async function changeStatus(status: string) {
  try {
    await api.put(`/final-invoices/${route.params.id}/status`, { status })
    showSnack(`Status changed to ${status}`, 'success')
    await loadInvoice()
  } catch { showSnack('Failed to change status', 'error') }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

async function loadInvoice() {
  try {
    const data = await api.get<any>(`/final-invoices/${route.params.id}`)
    // Add index to items
    if (data.items) {
      data.items = data.items.map((it: any, i: number) => ({ ...it, index: i + 1 }))
    }
    inv.value = data
    // Populate details form
    detailsForm.value = {
      shippingMethod: data.shippingMethod || '',
      shippingCost: data.shippingCost || 0,
      notes: data.notes || '',
      dueDate: data.dueDate ? data.dueDate.substring(0, 10) : '',
    }
    detailsOriginal.value = { ...detailsForm.value }
  } catch {}
}

onMounted(loadInvoice)
</script>
