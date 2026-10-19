<template>
  <v-dialog :model-value="modelValue" max-width="1000" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-star-circle" color="amber" class="mr-2" />
        Preferred suppliers
        <v-chip size="small" variant="tonal" class="ml-2">{{ rows.length }}</v-chip>
        <v-spacer />
        <v-btn color="primary" variant="flat" size="small" prepend-icon="mdi-plus" class="mr-2" @click="openForm()">Add supplier</v-btn>
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-card-subtitle class="px-4 pb-2 text-wrap">
        Kept in their own list — they don't have to be in the catalog. File rows match them by email, email domain or name.
        A catalog link is only needed to put their prices on RFQs.
      </v-card-subtitle>

      <v-card-text class="pt-2">
        <v-text-field
          v-model="search"
          placeholder="Filter"
          prepend-inner-icon="mdi-magnify"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          class="mb-2"
          style="max-width: 280px"
        />
        <v-progress-linear v-if="loading" indeterminate color="primary" height="2" />
        <table class="pref-table">
          <thead>
            <tr>
              <th>Supplier</th>
              <th>Emails · phone</th>
              <th style="width: 180px">Sending method</th>
              <th>Catalog</th>
              <th style="width: 76px" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in filtered" :key="r.id">
              <td>
                <div class="font-weight-medium">{{ r.name }}</div>
                <div v-if="r.note" class="text-caption text-medium-emphasis">{{ r.note }}</div>
                <div class="text-caption text-medium-emphasis">{{ fmtDate(r.addedAt) }}<span v-if="r.addedBy"> · {{ r.addedBy }}</span></div>
              </td>
              <td class="text-caption">
                <div v-for="e in r.emails" :key="e">{{ e }}</div>
                <div v-if="r.phone" class="text-medium-emphasis">{{ r.phone }}</div>
                <span v-if="!r.emails.length && !r.phone" class="text-medium-emphasis">—</span>
              </td>
              <td>
                <v-select
                  :model-value="r.sourceMethod"
                  :items="SOURCE_METHODS"
                  item-title="title"
                  item-value="value"
                  density="compact"
                  variant="plain"
                  hide-details
                  @update:model-value="(m: string) => setMethod(r, m)"
                >
                  <template #selection="{ item }">
                    <v-icon :icon="item.raw.icon" :color="item.raw.color" size="16" class="mr-1" />{{ item.raw.title }}
                  </template>
                </v-select>
              </td>
              <td>
                <v-chip v-if="r.supplierId" size="x-small" color="success" variant="tonal" prepend-icon="mdi-link-variant">
                  {{ r.supplierName }}
                </v-chip>
                <v-chip v-else size="x-small" variant="outlined" prepend-icon="mdi-link-variant-off">not linked</v-chip>
                <v-chip v-if="r.supplierStatus && r.supplierStatus !== 'Approved'" size="x-small" color="warning" variant="tonal" class="ml-1">{{ r.supplierStatus }}</v-chip>
              </td>
              <td class="nowrap">
                <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" title="Edit" @click="openForm(r)" />
                <v-btn icon="mdi-star-off-outline" size="x-small" variant="text" color="error" title="Remove from preferred" @click="remove(r)" />
              </td>
            </tr>
            <tr v-if="!loading && !filtered.length">
              <td colspan="5" class="text-center text-medium-emphasis pa-6">No preferred suppliers yet — add one.</td>
            </tr>
          </tbody>
        </table>
      </v-card-text>
    </v-card>

    <!-- Add / edit -->
    <v-dialog v-model="form.open" max-width="560">
      <v-card>
        <v-card-title class="pa-4">{{ form.id ? 'Edit preferred supplier' : 'Add preferred supplier' }}</v-card-title>
        <v-card-text class="pt-0">
          <v-text-field v-model="form.name" label="Supplier name" density="compact" variant="outlined" class="mb-3" autofocus hide-details="auto" />
          <v-combobox
            v-model="form.emails"
            label="Emails"
            placeholder="Type an email and press Enter"
            multiple
            chips
            closable-chips
            density="compact"
            variant="outlined"
            :delimiters="[',', ';', ' ']"
            prepend-inner-icon="mdi-email-multiple-outline"
            hint="Files match this supplier by these addresses and by their company domain"
            persistent-hint
            class="mb-3"
          />
          <div class="d-flex gap-3 mb-3">
            <v-text-field v-model="form.phone" label="Phone" density="compact" variant="outlined" hide-details />
            <v-select
              v-model="form.sourceMethod"
              :items="SOURCE_METHODS"
              item-title="title"
              item-value="value"
              label="Sending method"
              density="compact"
              variant="outlined"
              hide-details
            />
          </div>
          <v-text-field v-model="form.note" label="Note" density="compact" variant="outlined" hide-details class="mb-3" />
          <ProcurementPlusSupplierPicker
            :key="form.pickerKey"
            v-model="form.supplierId"
            label="Catalog supplier (optional)"
            :initial-search="form.emails[0] || form.name"
            @select="onCatalogPicked"
          />
          <div class="text-caption text-medium-emphasis mt-1">Only needed to put this supplier's prices on RFQs — can be linked later.</div>
          <v-alert v-if="form.error" type="error" variant="tonal" density="compact" class="mt-3">{{ form.error }}</v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="form.open = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="form.saving" :disabled="!form.name?.trim() && !form.supplierId" @click="save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-dialog>
</template>

<script setup lang="ts">
export interface PpPreferredSupplier {
  id: number; name: string; emails: string[]; phone?: string; sourceMethod: string; note?: string
  supplierId?: number; supplierName?: string; supplierStatus?: string; addedAt: string; addedBy?: string
}

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; changed: [] }>()

const api = useApi()
const rows = ref<PpPreferredSupplier[]>([])
const loading = ref(false)
const search = ref('')

const filtered = computed(() => {
  const q = (search.value || '').toLowerCase()
  return q ? rows.value.filter(r => `${r.name} ${r.emails.join(' ')} ${r.supplierName ?? ''}`.toLowerCase().includes(q)) : rows.value
})

async function load() {
  loading.value = true
  try {
    rows.value = await api.get<PpPreferredSupplier[]>('/procurement-plus/preferred-suppliers')
  } finally {
    loading.value = false
  }
}

// ── Add / edit ──
const form = reactive({
  open: false, id: null as number | null, name: '', emails: [] as string[], phone: '', sourceMethod: 'Email',
  note: '', supplierId: null as number | null, hadSupplier: false, saving: false, error: '', pickerKey: 0,
})

function openForm(r?: PpPreferredSupplier) {
  Object.assign(form, {
    open: true, id: r?.id ?? null, name: r?.name ?? '', emails: [...(r?.emails ?? [])], phone: r?.phone ?? '',
    sourceMethod: r?.sourceMethod ?? 'Email', note: r?.note ?? '', supplierId: r?.supplierId ?? null,
    hadSupplier: !!r?.supplierId, saving: false, error: '',
  })
  form.pickerKey++
}

/** Picking a catalog supplier on a new entry fills an empty name. */
function onCatalogPicked(s: { name: string } | null) {
  if (s && !form.name.trim()) form.name = s.name
}

async function save() {
  form.saving = true
  form.error = ''
  try {
    const body = {
      name: form.name,
      emails: form.emails.map(e => String(e).trim()).filter(Boolean),
      phone: form.phone || null,
      sourceMethod: form.sourceMethod,
      note: form.note || null,
      // 0 = unlink an existing catalog link; null on a new entry = none.
      supplierId: form.supplierId ?? (form.hadSupplier ? 0 : null),
    }
    if (form.id) await api.put(`/procurement-plus/preferred-suppliers/${form.id}`, body)
    else await api.post('/procurement-plus/preferred-suppliers', body)
    form.open = false
    await load()
    emit('changed')
  } catch (e: any) {
    form.error = e?.data?.message || 'Could not save'
  } finally {
    form.saving = false
  }
}

async function setMethod(r: PpPreferredSupplier, method: string) {
  const before = r.sourceMethod
  r.sourceMethod = method
  try {
    await api.patch(`/procurement-plus/preferred-suppliers/${r.id}`, { sourceMethod: method })
    emit('changed')
  } catch {
    r.sourceMethod = before
  }
}

async function remove(r: PpPreferredSupplier) {
  await api.del(`/procurement-plus/preferred-suppliers/${r.id}`)
  rows.value = rows.value.filter(x => x.id !== r.id)
  emit('changed')
}

watch(() => props.modelValue, (open) => { if (open) load() }, { immediate: true })
</script>

<style scoped>
.pref-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.pref-table th {
  text-align: left; font-size: 12px; font-weight: 600; opacity: 0.7; padding: 6px 8px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.pref-table td { padding: 6px 8px; border-bottom: 1px solid rgba(var(--v-border-color), 0.06); vertical-align: middle; }
.nowrap { white-space: nowrap; }
</style>
