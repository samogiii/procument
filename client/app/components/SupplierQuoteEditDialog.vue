<template>
  <!--
    Add / edit one supplier quote (or repair-shop row) for the RFQ page and /procument. The grids
    are read-only; every field is edited here. Works on a copy — Cancel discards, Save hands the
    copy to the page's `save` (which persists it and patches the grid row).
  -->
  <v-dialog :model-value="modelValue" max-width="760" scrollable persistent @update:model-value="v => !v && close()">
    <v-card v-if="form">
      <v-card-title class="pa-4 d-flex align-center">
        <v-icon :icon="shop ? 'mdi-wrench' : 'mdi-account-cash-outline'" size="20" class="mr-2" :color="shop ? 'warning' : 'primary'" />
        {{ form.id ? 'Edit' : 'Add' }} {{ shop ? 'shop record' : 'supplier quote' }}
        <span v-if="partNumber" class="text-medium-emphasis text-body-2 ml-2">— {{ partNumber }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" :disabled="saving" @click="close" />
      </v-card-title>

      <v-card-text class="pa-4 pt-0">
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3" closable @click:close="error = ''">
          {{ error }}
        </v-alert>

        <div class="sqe-label">{{ shop ? 'SHOP' : 'SUPPLIER' }}</div>
        <div class="mb-3">
          <SupplierPicker
            v-model="form.supplierName"
            v-model:supplier-id="form.supplierId"
            v-model:email="form.supplierEmail"
            :placeholder="shop ? 'Search shop...' : 'Search supplier...'"
            @select="onSupplierPicked"
            @create="openAddSupplier"
            @add-email="openAddSupplierEmail"
          />
          <div v-if="form.supplierStatus === 'Pending'" class="text-caption mt-1" style="color:#f59e0b;">⏳ Pending approval</div>
          <div v-else-if="form.supplierStatus === 'Rejected'" class="text-caption mt-1" style="color:#ef4444;">❌ Rejected — correct &amp; resubmit it from the grid</div>
          <div v-else-if="form.supplierStatus === 'Disabled'" class="text-caption mt-1 text-error">Disabled supplier — it can't be quoted</div>
        </div>

        <div class="sqe-row sqe-row--3">
          <v-select v-model="form.condition" :items="shop ? SHOP_CONDITIONS : QUOTE_CONDITIONS" label="Condition" density="compact" variant="outlined" clearable hide-details />
          <v-text-field v-model="form.alt" label="Alt P/N" placeholder="Same P/N" density="compact" variant="outlined" clearable hide-details />
          <v-text-field v-model.number="form.qty" type="number" min="0" label="Qty" density="compact" variant="outlined" hide-details />
        </div>
        <div class="sqe-row" :class="{ 'sqe-row--3': shop }">
          <v-select v-model="form.unit" :items="QUOTE_UNITS" item-title="title" item-value="value" label="Unit" density="compact" variant="outlined" clearable hide-details />
          <v-text-field v-model.number="form.price" type="number" min="0" step="0.01" label="Cost Price ($)" density="compact" variant="outlined" hide-details />
          <v-text-field v-if="shop" v-model.number="form.fixPrice" type="number" min="0" step="0.01" label="Repair Cost ($)" density="compact" variant="outlined" hide-details class="sqe-repair" />
        </div>
        <div class="sqe-row">
          <v-combobox v-model="form.certName" :items="CERT_TYPES" label="Cert Type" placeholder="Pick or type…" density="compact" variant="outlined" clearable hide-details />
          <v-text-field v-model="form.tagDate" type="date" :max="today" label="Tag Date" density="compact" variant="outlined" hide-details />
        </div>
        <div class="sqe-row">
          <v-text-field v-model.number="form.shippingCost" type="number" min="0" step="0.01" label="Shipping Cost ($)" density="compact" variant="outlined" hide-details />
          <v-text-field v-model="form.shippingPoint" label="Shipping Point" placeholder="City / Hub" density="compact" variant="outlined" hide-details />
        </div>
        <div class="sqe-row">
          <v-text-field
            :model-value="form.leadTime"
            label="Buy Lead Time"
            placeholder="e.g. 1-2 weeks, Stock"
            density="compact"
            variant="outlined"
            hide-details
            @update:model-value="v => setBuyLeadTime(form, v ?? '', customerBase)"
          />
          <v-text-field
            v-model="form.sellLeadTime"
            label="Sell Lead Time"
            :placeholder="sellLeadTimePreview ? `auto: ${sellLeadTimePreview}` : 'auto'"
            density="compact"
            variant="outlined"
            clearable
            hide-details
            title="Follows the buy lead time unless you type your own"
          />
        </div>
        <div v-if="!shop" class="sqe-row">
          <v-select
            v-model="form.companyPresetId"
            :items="companyPresetOptions"
            item-title="name"
            item-value="id"
            label="Our Company"
            density="compact"
            variant="outlined"
            clearable
            hide-details
          />
          <v-checkbox
            v-if="form.supplierDependency === 'Certificated'"
            v-model="form.isCertificated"
            label="Certificated"
            density="compact"
            hide-details
          />
        </div>
        <v-checkbox
          v-else-if="form.supplierDependency === 'Certificated'"
          v-model="form.isCertificated"
          label="Certificated"
          density="compact"
          hide-details
          class="mb-2"
        />

        <div class="sqe-label">QUOTE DETAILS</div>
        <div class="sqe-row sqe-row--3">
          <template v-for="f in QUOTE_DETAIL_FIELDS" :key="f.key">
            <v-combobox
              v-if="f.suggestions.length"
              v-model="form[f.key]"
              :items="f.suggestions"
              :label="f.label"
              :placeholder="f.placeholder"
              density="compact"
              variant="outlined"
              clearable
              hide-details
            />
            <v-text-field
              v-else
              v-model="form[f.key]"
              :label="f.label"
              :placeholder="f.placeholder"
              density="compact"
              variant="outlined"
              clearable
              hide-details
            />
          </template>
        </div>

        <v-textarea v-model="form.note" label="Note" rows="2" auto-grow density="compact" variant="outlined" class="mb-3" hide-details />
        <v-textarea v-model="form.myNotes" label="My Notes" rows="2" auto-grow density="compact" variant="outlined" class="mb-3" hide-details />
        <v-combobox
          v-model="form.customCertReferences"
          label="Cert Reference"
          placeholder="Type a reference and press Enter"
          multiple
          chips
          closable-chips
          density="compact"
          variant="outlined"
          hide-details
        />
        <div v-if="form.certReferences?.length" class="d-flex flex-wrap align-center gap-1 mt-2">
          <span class="text-caption text-medium-emphasis mr-1">From cert PDFs:</span>
          <v-chip v-for="r in form.certReferences" :key="r" size="x-small" color="error" variant="tonal" prepend-icon="mdi-file-pdf-box">REF#{{ r }}</v-chip>
        </div>
      </v-card-text>

      <v-card-actions class="pa-4 pt-0">
        <span class="text-caption text-medium-emphasis">Cert PDFs are managed from the grid's PDF button.</span>
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close">Cancel</v-btn>
        <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :loading="saving" @click="submit">Save</v-btn>
      </v-card-actions>
    </v-card>

    <!-- Add-supplier / add-email for the picker above -->
    <SupplierQuickAddDialog
      v-model="supplierDialog.open"
      :mode="supplierDialog.mode"
      :name="supplierDialog.name"
      :supplier-id="supplierDialog.supplierId"
      :current-email="supplierDialog.currentEmail"
      @saved="onSupplierDialogSaved"
    />
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  /** The grid row to edit, or a blank new row (id null). Never mutated — the form is a copy. */
  quote: any | null
  /** Repair-shop row under an AR quote. */
  shop?: boolean
  partNumber?: string
  /** RFQ customer base — Base 1 derives a different sell lead time. */
  customerBase?: number | null
  companyPresetOptions?: { id: number; name: string }[]
  /** Persists the edited copy; throw to keep the dialog open with the error shown. */
  save: (form: any) => Promise<void>
}>()
const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
}>()

const today = new Date().toISOString().split('T')[0]
const form = ref<any>(null)
const saving = ref(false)
const error = ref('')

const sellLeadTimePreview = computed(() => toSellLeadTime(form.value?.leadTime, props.customerBase))

watch(() => [props.modelValue, props.quote] as const, ([open, q]) => {
  if (!open || !q) return
  form.value = {
    ...emptyQuoteDetails(),
    ...q,
    customCertReferences: [...(q.customCertReferences ?? [])],
    certReferences: [...(q.certReferences ?? [])],
  }
  for (const f of QUOTE_DETAIL_FIELDS) form.value[f.key] ??= ''
  error.value = ''
}, { immediate: true })

function close() {
  if (saving.value) return
  emit('update:modelValue', false)
}

async function submit() {
  const f = form.value
  if (!f) return
  if (!f.supplierName?.trim()) {
    error.value = `${props.shop ? 'Shop' : 'Supplier'} is required`
    return
  }
  if (f.supplierStatus === 'Disabled') {
    error.value = `Supplier '${f.supplierName}' is disabled and cannot be saved.`
    return
  }
  saving.value = true
  error.value = ''
  try {
    await props.save(f)
    emit('update:modelValue', false)
  } catch (e: any) {
    const missing = e?.data?.missingSuppliers
    error.value = Array.isArray(missing) && missing.length
      ? `'${missing.join("', '")}' is not in the Catalog — pick a supplier from the list or use "Add new supplier".`
      : e?.data?.message || e?.message || 'Failed to save'
  } finally {
    saving.value = false
  }
}

// ── Supplier picker: add-supplier / add-email ──
const supplierDialog = ref({
  open: false,
  mode: 'supplier' as 'supplier' | 'email',
  name: '',
  supplierId: null as number | null,
  currentEmail: null as string | null,
})

function onSupplierPicked(supplier: { status?: string; dependency?: string }) {
  form.value.supplierStatus = supplier.status || 'Approved'
  form.value.supplierDependency = supplier.dependency || 'Normal'
}

function openAddSupplier(name: string) {
  supplierDialog.value = { open: true, mode: 'supplier', name, supplierId: null, currentEmail: null }
}

function openAddSupplierEmail() {
  const f = form.value
  supplierDialog.value = { open: true, mode: 'email', name: f.supplierName?.trim() || '', supplierId: f.supplierId || null, currentEmail: f.supplierEmail || null }
}

function onSupplierDialogSaved(result: { id: number; name: string; status?: string; dependency?: string; email: string }) {
  const f = form.value
  if (!f) return
  // A domain match may swap the typed name for the existing supplier's name.
  f.supplierName = result.name
  f.supplierId = result.id
  onSupplierPicked(result)
  f.supplierEmail = result.email // the address the user talks to for this RFQ item
}
</script>

<style scoped>
.sqe-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 12px;
  align-items: center;
}
.sqe-row--3 {
  grid-template-columns: 1fr 1fr 1fr;
}
.sqe-label {
  font-size: 12px;
  font-weight: 700;
  opacity: 0.7;
  margin-bottom: 6px;
}
.sqe-repair :deep(input) {
  color: #ff9800;
}
@media (max-width: 600px) {
  .sqe-row,
  .sqe-row--3 {
    grid-template-columns: 1fr;
  }
}
</style>
