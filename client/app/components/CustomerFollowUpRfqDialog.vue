<template>
  <v-dialog :model-value="!!row" max-width="860" scrollable @update:model-value="(open) => !open && emit('close')">
    <v-card v-if="row">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-file-search-outline" color="primary" class="mr-2" />
        RFQ Ref# — row #{{ row.id }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('close')" />
      </v-card-title>

      <div v-if="row.rfq" class="d-flex align-center flex-wrap gap-2 px-4 pb-2">
        <span class="text-body-2 text-medium-emphasis">Linked:</span>
        <NuxtLink :to="rfqPath(row.rfq.id)" target="_blank" class="font-weight-bold text-primary">
          {{ row.rfq.name }}<v-icon icon="mdi-open-in-new" size="13" class="ml-1" />
        </NuxtLink>
        <v-chip size="x-small" variant="tonal">{{ row.rfq.status ?? 'Open' }}</v-chip>
        <span v-if="row.rfq.customerCode" class="text-caption text-medium-emphasis">{{ row.rfq.customerCode }}</span>
        <v-spacer />
        <v-btn size="small" variant="text" color="error" prepend-icon="mdi-link-variant-off" :loading="saving" @click="link(null)">Unlink</v-btn>
      </div>

      <v-tabs v-model="tab" color="primary" density="compact" class="px-4">
        <v-tab value="select" prepend-icon="mdi-magnify">Select RFQ</v-tab>
        <v-tab value="create" prepend-icon="mdi-plus">Create new RFQ</v-tab>
      </v-tabs>
      <v-divider />

      <v-card-text class="pa-4" style="height: 520px;">
        <!-- Existing RFQs (never No Quote), loaded page by page while scrolling -->
        <div v-show="tab === 'select'" class="cfu-pane">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            placeholder="Search RFQ name, customer or part number..."
            density="compact"
            variant="outlined"
            hide-details
            clearable
            autofocus
            class="mb-3 flex-grow-0"
          />
          <div class="cfu-picker-list border rounded" @scroll="onScroll">
            <v-list density="compact" class="py-0">
              <v-list-item
                v-for="item in picker.items"
                :key="item.id"
                :active="row.rfq?.id === item.id"
                color="primary"
                :disabled="saving"
                @click="link(item.id)"
              >
                <v-list-item-title class="font-weight-medium">
                  {{ item.label }}
                  <v-chip size="x-small" variant="tonal" class="ml-2" :color="followUpStatusColor(item.status)">{{ item.status ?? 'Open' }}</v-chip>
                </v-list-item-title>
                <v-list-item-subtitle>{{ item.detail }}</v-list-item-subtitle>
                <template #append>
                  <span class="text-caption text-medium-emphasis mr-2">{{ new Date(item.createdAt).toLocaleDateString() }}</span>
                  <v-btn
                    icon="mdi-open-in-new"
                    size="x-small"
                    variant="text"
                    :href="router.resolve(rfqPath(item.id)).href"
                    target="_blank"
                    title="Open this RFQ"
                    @click.stop
                  />
                </template>
              </v-list-item>
            </v-list>
            <div v-if="picker.loading" class="d-flex justify-center pa-3"><v-progress-circular indeterminate size="22" width="2" /></div>
            <p v-else-if="picker.error" class="text-caption text-error text-center py-6 mb-0">{{ picker.error }}</p>
            <p v-else-if="!picker.items.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">No RFQs found.</p>
            <div v-else-if="picker.hasMore" class="d-flex justify-center pa-2">
              <v-btn size="small" variant="text" @click="picker.loadMore()">Load more</v-btn>
            </div>
          </div>
        </div>

        <!-- New RFQ -->
        <v-form v-show="tab === 'create'" ref="createForm" @submit.prevent="create">
          <v-row dense>
            <v-col cols="12" md="5">
              <v-text-field v-model="draft.name" label="RFQ name" density="compact" variant="outlined" :rules="[required]" />
            </v-col>
            <v-col cols="12" md="4">
              <v-autocomplete
                v-model="draft.customerId"
                :items="customers"
                :item-title="customerTitle"
                item-value="id"
                label="Customer code"
                density="compact"
                variant="outlined"
                :loading="customersLoading"
                :rules="[required]"
              />
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field v-model="draft.deadline" type="date" label="Deadline" density="compact" variant="outlined" :rules="[required]" />
            </v-col>
          </v-row>

          <div class="d-flex align-center mb-2">
            <span class="text-subtitle-2">Parts ({{ draft.items.length }})</span>
            <v-spacer />
            <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="addPart">Add part</v-btn>
          </div>
          <div v-for="(item, index) in draft.items" :key="item.key" class="d-flex align-start gap-2 mb-1">
            <span class="text-caption text-medium-emphasis pt-2" style="width: 20px;">{{ index + 1 }}</span>
            <v-text-field v-model="item.partNumber" label="Part number" density="compact" variant="outlined" :rules="[required]" style="flex: 2;" />
            <v-text-field v-model="item.description" label="Description" density="compact" variant="outlined" hide-details="auto" style="flex: 3;" />
            <v-text-field v-model.number="item.qty" type="number" min="1" label="Qty" density="compact" variant="outlined" :rules="[positive]" style="max-width: 100px;" />
            <v-btn
              icon="mdi-delete-outline"
              size="small"
              variant="text"
              color="error"
              class="mt-1"
              :disabled="draft.items.length === 1"
              title="Remove this part"
              @click="draft.items.splice(index, 1)"
            />
          </div>
        </v-form>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-3">
        <span v-if="tab === 'select'" class="text-caption text-medium-emphasis">Click an RFQ to link it. No Quote RFQs are not listed.</span>
        <v-spacer />
        <v-btn variant="text" @click="emit('close')">Close</v-btn>
        <v-btn v-if="tab === 'create'" color="primary" variant="flat" prepend-icon="mdi-check" :loading="saving" @click="create">Create &amp; link RFQ</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { followUpStatusColor, localToday, rfqPath, type FollowUpRow } from '~/utils/customerFollowUps'
import { useFollowUpRefPicker } from '~/composables/useFollowUpRefPicker'

interface CustomerOption { id: number; name: string; customerCode: string | null }

/** Links an RFQ to a Customer Follow Ups row: pick an existing one, or create a new RFQ with several parts. */
const props = defineProps<{ row: FollowUpRow | null }>()

const emit = defineEmits<{
  close: []
  /** The saved row as the server returned it. */
  saved: [row: any]
  notify: [text: string, color: string]
}>()

const api = useApi()
const router = useRouter()
const tab = ref<'select' | 'create'>('select')
const saving = ref(false)
const search = ref('')
const picker = useFollowUpRefPicker('Rfq', search)

function errorMessage(error: any, fallback: string) {
  return error?.data?.message || error?.response?._data?.message || fallback
}

function onScroll(event: Event) {
  const el = event.target as HTMLElement
  if (el.scrollTop + el.clientHeight >= el.scrollHeight - 60) picker.loadMore()
}

async function link(rfqId: number | null) {
  if (!props.row || saving.value) return
  saving.value = true
  try {
    const saved = await api.put<any>(`/customer-follow-ups/${props.row.id}/rfq`, { rfqId })
    emit('saved', saved)
    emit('notify', rfqId ? `RFQ ${saved?.rfq?.name ?? ''} linked to row #${props.row.id}.` : `RFQ unlinked from row #${props.row.id}.`, 'success')
    if (rfqId) emit('close')
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not link the RFQ.'), 'error')
  } finally {
    saving.value = false
  }
}

// ── New RFQ ──────────────────────────────────────────────────────────────────

const required = (value: unknown) => (value !== null && value !== undefined && String(value).trim() !== '') || 'Required'
const positive = (value: unknown) => Number(value) > 0 || 'More than 0'

let partKey = 0
function blankPart(partNumber = '') {
  return { key: ++partKey, partNumber, description: '', qty: 1 }
}

const draft = reactive({
  name: '',
  customerId: null as number | null,
  deadline: '',
  items: [blankPart()],
})

const createForm = ref<{ validate: () => Promise<{ valid: boolean }> } | null>(null)
const customers = ref<CustomerOption[]>([])
const customersLoading = ref(false)

function customerTitle(customer: CustomerOption) {
  return customer.customerCode ? `${customer.customerCode} — ${customer.name}` : customer.name
}

function addPart() {
  draft.items.push(blankPart())
}

function inDays(days: number) {
  const date = new Date(`${localToday()}T00:00:00`)
  date.setDate(date.getDate() + days)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** The row's customer, matched by code (or by name when the row holds a name). */
function matchCustomer() {
  const wanted = (props.row?.customerCode ?? '').trim().toLowerCase()
  if (!wanted) return null
  return customers.value.find(c => (c.customerCode ?? '').toLowerCase() === wanted)?.id
    ?? customers.value.find(c => c.name.toLowerCase() === wanted)?.id
    ?? null
}

async function loadCustomers() {
  if (customers.value.length || customersLoading.value) return
  customersLoading.value = true
  try {
    customers.value = await api.get<CustomerOption[]>('/customer-follow-ups/rfq-customers')
    draft.customerId ??= matchCustomer()
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not load customers.'), 'error')
  } finally {
    customersLoading.value = false
  }
}

async function create() {
  if (!props.row || saving.value) return
  if (!(await createForm.value?.validate())?.valid) return
  saving.value = true
  try {
    const saved = await api.post<any>(`/customer-follow-ups/${props.row.id}/rfq`, {
      name: draft.name.trim(),
      customerId: draft.customerId,
      deadline: draft.deadline,
      items: draft.items.map(item => ({
        partNumber: item.partNumber.trim(),
        description: item.description.trim() || null,
        qty: Number(item.qty) || 1,
      })),
    })
    emit('saved', saved)
    emit('notify', `RFQ ${saved?.rfq?.name ?? draft.name} created and linked to row #${props.row.id}.`, 'success')
    emit('close')
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not create the RFQ.'), 'error')
  } finally {
    saving.value = false
  }
}

// Each opening starts from the row: its part number, deadline and customer prefill the new-RFQ form.
watch(() => props.row?.id, (id) => {
  if (id === undefined) return
  const row = props.row!
  tab.value = 'select'
  search.value = ''
  draft.name = ''
  draft.deadline = row.deadline && row.deadline >= localToday() ? row.deadline : inDays(3)
  draft.items = [blankPart(row.partNumber ?? '')]
  draft.customerId = matchCustomer()
  picker.reset()
}, { immediate: true })

watch(tab, (value) => {
  if (value === 'create') loadCustomers()
})
</script>

<style scoped>
/* Not d-flex: its display !important would beat v-show and keep hidden tabs visible. */
.cfu-pane {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.cfu-picker-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}
</style>
