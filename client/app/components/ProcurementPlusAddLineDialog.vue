<template>
  <v-dialog :model-value="modelValue" max-width="520" @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="pa-4 d-flex align-center">
        <v-icon icon="mdi-domain-plus" color="primary" class="mr-2" />
        Add supplier · {{ partNumber }}
      </v-card-title>
      <v-card-text class="pt-0">
        <v-btn-toggle v-model="mode" mandatory density="compact" variant="outlined" divided color="primary" class="mb-4">
          <v-btn value="preferred" size="small" prepend-icon="mdi-star-outline">Preferred</v-btn>
          <v-btn value="catalog" size="small" prepend-icon="mdi-domain">Catalog</v-btn>
          <v-btn value="custom" size="small" prepend-icon="mdi-form-textbox">Custom</v-btn>
        </v-btn-toggle>

        <v-autocomplete
          v-if="mode === 'preferred'"
          v-model="preferredId"
          :items="preferreds"
          item-title="name"
          item-value="id"
          label="Preferred supplier"
          density="compact"
          variant="outlined"
          :loading="loadingPreferred"
          prepend-inner-icon="mdi-star"
          hide-details
          autofocus
        >
          <template #item="{ props: itemProps, item }">
            <v-list-item v-bind="itemProps" :subtitle="item.raw.emails[0] || undefined">
              <template #append>
                <v-chip v-if="!item.raw.supplierId" size="x-small" variant="outlined">not in catalog</v-chip>
              </template>
            </v-list-item>
          </template>
        </v-autocomplete>

        <ProcurementPlusSupplierPicker v-else-if="mode === 'catalog'" v-model="supplierId" label="Catalog supplier" />

        <template v-else>
          <v-text-field v-model="customName" label="Supplier name" density="compact" variant="outlined" hide-details autofocus />
          <div class="text-caption text-medium-emphasis mt-2">
            A supplier that is nowhere yet. Pick its catalog supplier on the line before Done (prices on RFQs need one).
          </div>
        </template>

        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!ready" :loading="saving" @click="save">Add</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpQuoteLine } from '~/utils/procurementPlus'

const props = defineProps<{ modelValue: boolean; partId: number; partNumber: string }>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; added: [line: PpQuoteLine] }>()

const api = useProcurementPlusApi()
const mode = ref<'preferred' | 'catalog' | 'custom'>('preferred')
const preferreds = ref<{ id: number; name: string; emails: string[]; supplierId?: number }[]>([])
const loadingPreferred = ref(false)
const preferredId = ref<number | null>(null)
const supplierId = ref<number | null>(null)
const customName = ref('')
const saving = ref(false)
const error = ref('')

const ready = computed(() =>
  mode.value === 'preferred' ? !!preferredId.value : mode.value === 'catalog' ? !!supplierId.value : !!customName.value.trim())

watch(() => props.modelValue, async (open) => {
  if (!open) return
  preferredId.value = null
  supplierId.value = null
  customName.value = ''
  error.value = ''
  if (preferreds.value.length) return
  loadingPreferred.value = true
  try {
    preferreds.value = await api.get('/procurement-plus/preferred-suppliers')
    if (!preferreds.value.length) mode.value = 'catalog'
  } finally {
    loadingPreferred.value = false
  }
})

async function save() {
  saving.value = true
  error.value = ''
  try {
    const body = mode.value === 'preferred'
      ? { preferredId: preferredId.value }
      : mode.value === 'catalog' ? { supplierId: supplierId.value } : { supplierName: customName.value.trim() }
    const line = await api.post<PpQuoteLine>(`/procurement-plus/parts/${props.partId}/lines`, body)
    emit('update:modelValue', false)
    emit('added', line)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not add the supplier'
  } finally {
    saving.value = false
  }
}
</script>
