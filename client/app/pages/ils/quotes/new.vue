<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo('/ils?tab=quotes')" />
      <div>
        <h1 class="text-h5 font-weight-bold">New ILS Quote</h1>
        <p class="text-caption text-medium-emphasis mb-0">Step 1 of 3 — Quote details</p>
      </div>
    </div>

    <v-stepper :model-value="1" :items="['Details', 'Select Parts', 'Serials & Pricing']" hide-actions class="mb-4 glass-card" flat />

    <v-card class="glass-card" max-width="720">
      <v-card-text class="pa-4">
        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.ilsCustomerId"
              :items="ilsCustomers"
              item-title="name"
              item-value="id"
              label="ILS Customer *"
              variant="outlined"
              density="compact"
              hide-details
              :loading="customersLoading"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field
              v-model="form.rfqReference"
              label="RFQ Reference Number"
              variant="outlined"
              density="compact"
              hide-details
              placeholder="e.g. RFQ-2024-001"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-textarea v-model="form.billTo" label="Bill To" variant="outlined" density="compact" hide-details rows="2" placeholder="Pre-filled from customer" />
          </v-col>
          <v-col cols="12" md="6">
            <v-textarea v-model="form.shipTo" label="Ship To" variant="outlined" density="compact" hide-details rows="2" placeholder="Pre-filled from customer" />
          </v-col>
          <v-col cols="12">
            <v-textarea v-model="form.notes" label="Notes" variant="outlined" density="compact" hide-details rows="3" />
          </v-col>
        </v-row>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="navigateTo('/ils?tab=quotes')">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          append-icon="mdi-arrow-right"
          :loading="saving"
          :disabled="!form.ilsCustomerId"
          @click="next"
        >
          Next: Select Parts
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()

const ilsCustomers = ref<any[]>([])
const customersLoading = ref(false)
const saving = ref(false)

const form = ref({
  ilsCustomerId: null as number | null,
  rfqReference: '',
  notes: '',
  billTo: '',
  shipTo: '',
})

// Pre-fill Bill To / Ship To from the chosen customer (only when empty)
watch(() => form.value.ilsCustomerId, (id) => {
  const c = ilsCustomers.value.find(x => x.id === id)
  if (!c) return
  if (!form.value.billTo) form.value.billTo = c.billTo || ''
  if (!form.value.shipTo) form.value.shipTo = c.shipTo || ''
})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

async function loadCustomers() {
  customersLoading.value = true
  try {
    ilsCustomers.value = await api.get<any[]>('/ils-customers')
  } catch {
    showSnack('Failed to load customers', 'error')
  } finally {
    customersLoading.value = false
  }
}

async function next() {
  if (!form.value.ilsCustomerId) return
  saving.value = true
  try {
    const created = await api.post<any>('/ils-quotes', {
      ilsCustomerId: form.value.ilsCustomerId,
      rfqReference: form.value.rfqReference || null,
      notes: form.value.notes || null,
      billTo: form.value.billTo || null,
      shipTo: form.value.shipTo || null,
      items: [],
    })
    navigateTo(`/ils/quotes/${created.id}/select-parts`)
  } catch {
    showSnack('Failed to create quote', 'error')
    saving.value = false
  }
}

onMounted(loadCustomers)
</script>
