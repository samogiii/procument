<template>
  <v-navigation-drawer
    :model-value="modelValue"
    location="right"
    temporary
    :width="Math.min(760, viewportWidth)"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="d-flex flex-column h-100">
      <!-- Header: one supplier × company balance -->
      <div class="pa-4 border-b">
        <div class="d-flex align-start">
          <div class="flex-grow-1">
            <div class="text-overline text-medium-emphasis">{{ isLine ? 'Credit line' : 'Supplier credit' }}</div>
            <h2 class="text-h6 font-weight-bold">{{ account?.supplierName ?? '…' }}</h2>
            <div class="text-body-2 text-medium-emphasis">
              <v-icon icon="mdi-domain" size="16" class="mr-1" />{{ account?.companyPresetName }}
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" aria-label="Close ledger" @click="$emit('update:modelValue', false)" />
        </div>

        <!-- Credit line: what we owe comes first — it is the reminder -->
        <div v-if="isLine" class="d-flex flex-wrap align-end gap-6 mt-3">
          <div>
            <div class="text-caption text-medium-emphasis">Owed to the supplier</div>
            <div class="text-h5 font-weight-bold" :class="(account?.owed ?? 0) > 0 ? 'text-error' : 'text-success'">
              ${{ formatPrice(account?.owed ?? 0) }}
            </div>
          </div>
          <div>
            <div class="text-caption text-medium-emphasis">Limit</div>
            <div class="text-body-1">${{ formatPrice(account?.creditLimit ?? 0) }}</div>
          </div>
          <div>
            <div class="text-caption text-medium-emphasis">Available</div>
            <div class="text-body-1">${{ formatPrice(account?.balance ?? 0) }}</div>
          </div>
        </div>
        <div v-else class="d-flex flex-wrap align-end gap-6 mt-3">
          <div>
            <div class="text-caption text-medium-emphasis">Balance</div>
            <div class="text-h5 font-weight-bold" :class="(account?.balance ?? 0) > 0 ? 'text-success' : ''">
              ${{ formatPrice(account?.balance ?? 0) }}
            </div>
          </div>
          <div>
            <div class="text-caption text-medium-emphasis">Total in</div>
            <div class="text-body-1">${{ formatPrice(account?.totalIn ?? 0) }}</div>
          </div>
          <div>
            <div class="text-caption text-medium-emphasis">Total used / out</div>
            <div class="text-body-1">${{ formatPrice(account?.totalOut ?? 0) }}</div>
          </div>
        </div>

        <div v-if="account && isLine && (canPay || canAdjust)" class="d-flex flex-wrap gap-2 mt-3">
          <v-btn v-if="canPay && (account.owed > 0 || account.openRepaymentId)" size="small" variant="tonal" color="teal"
            prepend-icon="mdi-cash-refund" @click="$emit('pay-back', account)">Pay back</v-btn>
          <v-btn v-if="canAdjust" size="small" variant="tonal" color="indigo" prepend-icon="mdi-pencil-outline"
            @click="$emit('change-limit', account)">Change limit</v-btn>
        </div>
        <div v-else-if="account && !isLine && (canAdd || canAdjust)" class="d-flex flex-wrap gap-2 mt-3">
          <v-btn v-if="canAdd" size="small" variant="tonal" color="success" prepend-icon="mdi-file-document-plus-outline"
            @click="$emit('add', 'creditNote', account)">Add credit note</v-btn>
          <v-btn v-if="canAdd" size="small" variant="tonal" color="teal" prepend-icon="mdi-bank-transfer-out"
            @click="$emit('add', 'walletTopUp', account)">Prepay from wallet</v-btn>
          <v-btn v-if="canAdjust" size="small" variant="tonal" color="warning" prepend-icon="mdi-scale-balance"
            @click="$emit('add', 'adjustment', account)">Adjust</v-btn>
        </div>
      </div>

      <!-- Ledger -->
      <div class="flex-grow-1 overflow-y-auto">
        <v-data-table-server
          :headers="headers"
          :items="entries"
          :items-length="entriesTotal"
          :loading="entriesLoading"
          v-model:page="page"
          v-model:items-per-page="pageSize"
          :items-per-page-options="[25, 50, 100]"
          density="compact"
          @update:options="loadEntries"
        >
          <template #item.createdAt="{ item }">
            <div class="text-no-wrap">{{ new Date(item.createdAt).toLocaleDateString() }}</div>
            <div class="text-caption text-medium-emphasis">{{ item.createdByName }}</div>
          </template>
          <template #item.source="{ item }">
            <v-chip size="x-small" variant="tonal" :color="supplierCreditSourceColor(item.source)">
              {{ supplierCreditEntryLabel(item.source, account?.kind) }}
            </v-chip>
            <div v-if="item.reversedByEntryId" class="text-caption text-medium-emphasis mt-1">Reversed</div>
          </template>
          <template #item.amount="{ item }">
            <span class="font-weight-medium text-no-wrap" :class="item.direction === 'In' ? 'text-success' : 'text-error'">
              {{ item.direction === 'In' ? '+' : '−' }}${{ formatPrice(item.amount) }}
            </span>
          </template>
          <template #item.balanceAfter="{ item }">
            <span class="text-no-wrap">${{ formatPrice(item.balanceAfter) }}</span>
          </template>
          <template #item.details="{ item }">
            <div class="py-1 text-body-2" style="max-width: 300px">
              <NuxtLink v-if="item.poLink" :to="item.poLink" class="text-primary text-decoration-none">
                {{ item.poNumber }}<template v-if="item.prNumber"> · PR-{{ item.prNumber }}</template>
              </NuxtLink>
              <!-- Where a credit note came from -->
              <div v-if="item.sourcePoLink" class="text-caption">
                From <NuxtLink :to="item.sourcePoLink" class="text-primary text-decoration-none">{{ item.sourcePoNumber }}</NuxtLink>
              </div>
              <div v-else-if="item.sourcePoReference" class="text-caption">From: {{ item.sourcePoReference }}</div>
              <div v-if="item.creditMemo" class="text-caption">Credit memo: {{ item.creditMemo }}</div>
              <div v-if="item.walletName" class="text-caption"><v-icon icon="mdi-wallet-outline" size="14" /> {{ item.walletName }}</div>
              <div v-if="item.notes" class="text-caption text-medium-emphasis text-truncate" :title="item.notes">{{ item.notes }}</div>
              <div v-if="item.documents.length" class="d-flex flex-wrap gap-1 mt-1">
                <v-chip
                  v-for="doc in item.documents"
                  :key="doc.id"
                  size="x-small"
                  variant="tonal"
                  :color="doc.category === 'SupplierPI' ? 'primary' : doc.category === 'CreditMemo' ? 'success' : undefined"
                  prepend-icon="mdi-paperclip"
                  :title="`${SUPPLIER_CREDIT_DOCUMENT_LABELS[doc.category]} · ${doc.originalFileName}`"
                  @click="openDocument(doc)"
                >
                  {{ SUPPLIER_CREDIT_DOCUMENT_LABELS[doc.category] }}: {{ doc.originalFileName }}
                </v-chip>
              </div>
            </div>
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex justify-end">
              <v-btn v-if="canAdd" icon="mdi-paperclip-plus" size="small" variant="text" title="Attach documents"
                :aria-label="`Attach documents to entry ${item.id}`" @click="startAttach(item)" />
              <v-btn v-if="canReverse(item)" icon="mdi-undo-variant" size="small" variant="text" color="error"
                :aria-label="`Reverse entry ${item.id}`" title="Reverse" @click="startReverse(item)" />
            </div>
          </template>
          <template #no-data>
            <div class="pa-6 text-medium-emphasis">No credit movements yet.</div>
          </template>
        </v-data-table-server>
      </div>
    </div>
  </v-navigation-drawer>

  <!-- Attach more proof to one entry (never changes a balance) -->
  <v-dialog v-model="attachOpen" max-width="480" persistent>
    <v-card class="glass-card">
      <v-form ref="attachFormRef" @submit.prevent="confirmAttach">
        <v-card-title class="pa-4">Attach documents</v-card-title>
        <v-card-text class="pt-0">
          <p v-if="attachTarget" class="text-body-2 text-medium-emphasis mb-3">
            {{ supplierCreditSourceLabel(attachTarget.source) }} of ${{ formatPrice(attachTarget.amount) }}
            on {{ new Date(attachTarget.createdAt).toLocaleDateString() }}.
          </p>
          <v-radio-group v-model="attachCategory" label="Document type *" inline>
            <v-radio v-for="(label, key) in SUPPLIER_CREDIT_DOCUMENT_LABELS" :key="key" :label="label" :value="key" />
          </v-radio-group>
          <v-file-input
            v-model="attachFiles"
            label="Files *"
            :accept="SUPPLIER_CREDIT_FILE_ACCEPT"
            :multiple="attachCategory !== 'SupplierPI'"
            chips
            counter
            show-size
            prepend-icon=""
            prepend-inner-icon="mdi-paperclip"
            variant="outlined"
            :rules="[attachFilesRule]"
          />
          <div aria-live="polite">
            <v-alert v-if="attachError" type="error" variant="tonal" density="compact" class="mt-2">{{ attachError }}</v-alert>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" :disabled="attaching" @click="attachOpen = false">Cancel</v-btn>
          <v-btn type="submit" color="primary" variant="flat" :loading="attaching">Attach</v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>

  <!-- Reverse one ledger entry -->
  <v-dialog v-model="reverseOpen" max-width="480" persistent>
    <v-card class="glass-card">
      <v-form ref="reverseFormRef" @submit.prevent="confirmReverse">
        <v-card-title class="pa-4">Reverse this entry?</v-card-title>
        <v-card-text class="pt-0">
          <p v-if="reverseTarget" class="text-body-2 mb-3">
            {{ supplierCreditEntryLabel(reverseTarget.source, account?.kind) }} of
            <strong>${{ formatPrice(reverseTarget.amount) }}</strong>
            will be undone with a mirror entry. The original stays in the history.
            <template v-if="reverseTarget.source === 'POApplication'"> The PO's payment status is re-opened.</template>
            <template v-if="reverseTarget.source === 'WalletTopUp'"> The money goes back into the wallet.</template>
            <template v-if="reverseTarget.source === 'Repayment'"> The money goes back into the wallet and the amount is owed again.</template>
          </p>
          <v-textarea
            v-model="reverseNotes"
            label="Reason *"
            variant="outlined"
            rows="2"
            auto-grow
            maxlength="500"
            counter
            :rules="[(v: string) => !!v?.trim() || 'Required']"
          />
          <div aria-live="polite">
            <v-alert v-if="reverseError" type="error" variant="tonal" density="compact" class="mt-2">{{ reverseError }}</v-alert>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" :disabled="reversing" @click="reverseOpen = false">Cancel</v-btn>
          <v-btn type="submit" color="error" variant="flat" :loading="reversing">Reverse</v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>

  <DocPreviewModal
    :open="docPreview.open.value"
    :blob-url="docPreview.blobUrl.value"
    :file-name="docPreview.fileName.value"
    :mime-type="docPreview.mimeType.value"
    @close="docPreview.close()"
  />
</template>

<script setup lang="ts">
import type {
  SupplierCreditAccountRow, SupplierCreditDialogMode, SupplierCreditDocumentCategory, SupplierCreditDocumentRow, SupplierCreditEntryRow,
} from '~/utils/supplierCredits'

const props = defineProps<{
  modelValue: boolean
  accountId: number | null
  /** Bump to reload after a credit was added from outside the drawer. */
  refreshKey?: number
}>()
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  add: [mode: SupplierCreditDialogMode, account: SupplierCreditAccountRow]
  'pay-back': [account: SupplierCreditAccountRow]
  'change-limit': [account: SupplierCreditAccountRow]
  changed: []
}>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
// Never wider than the screen (phones).
const { width: viewportWidth } = useDisplay()

const role = computed(() => authStore.user?.role ?? '')
const canAdd = computed(() => ['SuperAdmin', 'Admin', 'Payment'].includes(role.value))
const canAdjust = computed(() => ['SuperAdmin', 'Admin'].includes(role.value))
// Repaying a credit line follows the supplier-payment roles.
const canPay = computed(() => ['SuperAdmin', 'Admin', 'Payment', 'AHM'].includes(role.value))
const isLine = computed(() => account.value?.kind === 'CreditLine')

const headers = [
  { title: 'Date', key: 'createdAt', sortable: false },
  { title: 'Type', key: 'source', sortable: false },
  { title: 'Amount', key: 'amount', sortable: false, align: 'end' as const },
  { title: 'Balance', key: 'balanceAfter', sortable: false, align: 'end' as const },
  { title: 'Details', key: 'details', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]

const account = ref<SupplierCreditAccountRow | null>(null)
const entries = shallowRef<SupplierCreditEntryRow[]>([])
const entriesTotal = ref(0)
const entriesLoading = ref(false)
const page = ref(1)
const pageSize = ref(25)

async function loadAccount() {
  if (!props.accountId) return
  try { account.value = await api.get<SupplierCreditAccountRow>(`/supplier-credits/${props.accountId}`) }
  catch { account.value = null }
}

let loadSeq = 0
async function loadEntries(options?: { page: number; itemsPerPage: number }) {
  if (!props.accountId) return
  if (options) { page.value = options.page; pageSize.value = options.itemsPerPage }
  const seq = ++loadSeq
  entriesLoading.value = true
  try {
    const res = await api.get<any>(`/supplier-credits/${props.accountId}/entries?page=${page.value}&pageSize=${pageSize.value}`)
    if (seq !== loadSeq) return
    entries.value = res.items ?? []
    entriesTotal.value = res.totalCount ?? 0
  } catch {
    if (seq === loadSeq) { entries.value = []; entriesTotal.value = 0 }
  } finally {
    if (seq === loadSeq) entriesLoading.value = false
  }
}

async function reload() {
  await Promise.all([loadAccount(), loadEntries()])
}

watch(() => [props.modelValue, props.accountId] as const, ([open, id]) => {
  if (!open || !id) return
  account.value = null
  page.value = 1
  reload()
}, { immediate: true })
watch(() => props.refreshKey, () => { if (props.modelValue) reload() })

// ── Documents ──
async function openDocument(doc: SupplierCreditDocumentRow) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/supplier-credits/documents/${doc.id}/file`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, doc.originalFileName)
  } catch { /* the preview simply does not open; the row stays usable */ }
}

const attachOpen = ref(false)
const attachTarget = ref<SupplierCreditEntryRow | null>(null)
const attachCategory = ref<SupplierCreditDocumentCategory>('Other')
const attachFiles = ref<File | File[] | null>(null)
const attachError = ref('')
const attaching = ref(false)
const attachFormRef = ref<any>(null)
const attachList = computed<File[]>(() => {
  const v = attachFiles.value
  return Array.isArray(v) ? v : v ? [v] : []
})
const attachFilesRule = () => {
  if (!attachList.value.length) return 'Choose at least one file'
  return attachList.value.length <= SUPPLIER_CREDIT_MAX_FILES || `At most ${SUPPLIER_CREDIT_MAX_FILES} files at a time`
}

function startAttach(entry: SupplierCreditEntryRow) {
  attachTarget.value = entry
  attachCategory.value = 'Other'
  attachFiles.value = null
  attachError.value = ''
  attachOpen.value = true
}

async function confirmAttach() {
  if (!attachTarget.value || attaching.value) return
  const { valid } = await attachFormRef.value.validate()
  if (!valid) return
  attaching.value = true
  attachError.value = ''
  try {
    const form = new FormData()
    form.append('category', attachCategory.value)
    for (const f of attachList.value) form.append('files', f)
    await api.post(`/supplier-credits/entries/${attachTarget.value.id}/documents`, form)
    attachOpen.value = false
    await loadEntries()
  } catch (e: any) {
    attachError.value = apiErrorMessage(e, 'The documents could not be attached.')
  } finally {
    attaching.value = false
  }
}

// ── Reversal ──
const reverseOpen = ref(false)
const reverseTarget = ref<SupplierCreditEntryRow | null>(null)
const reverseNotes = ref('')
const reverseError = ref('')
const reversing = ref(false)
const reverseFormRef = ref<any>(null)
let reverseRequestId = ''

function canReverse(entry: SupplierCreditEntryRow) {
  // A limit is changed with "Change limit", never reversed (the API refuses it too).
  if (entry.source === 'CreditLimit') return false
  return canAdjust.value && entry.source !== 'Reversal' && !entry.reversedByEntryId
}

function startReverse(entry: SupplierCreditEntryRow) {
  reverseTarget.value = entry
  reverseNotes.value = ''
  reverseError.value = ''
  reverseRequestId = newRequestId()
  reverseOpen.value = true
}

async function confirmReverse() {
  if (!reverseTarget.value || reversing.value) return
  const { valid } = await reverseFormRef.value.validate()
  if (!valid) return
  reversing.value = true
  reverseError.value = ''
  try {
    await api.post(`/supplier-credits/entries/${reverseTarget.value.id}/reverse`, {
      notes: reverseNotes.value.trim(),
      requestId: reverseRequestId,
    })
    reverseOpen.value = false
    await reload()
    emit('changed')
  } catch (e: any) {
    reverseError.value = apiErrorMessage(e, 'The entry could not be reversed.')
  } finally {
    reversing.value = false
  }
}
</script>
