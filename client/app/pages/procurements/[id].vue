<template>
  <div v-if="loading && !procurement" class="d-flex justify-center align-center" style="min-height: 400px;">
    <v-progress-circular indeterminate color="primary" size="64" />
  </div>

  <div v-else-if="procurement">
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" to="/procurements" size="small" />
      <div>
        <div class="d-flex align-center gap-2">
          <h1 class="text-h5 font-weight-bold">{{ procurement.procurementNumber }}</h1>
          <v-chip :color="statusColor(procurement.status)" size="small" variant="flat" class="text-uppercase font-weight-bold">
            {{ procurement.status }}
          </v-chip>
        </div>
        <div class="text-caption text-medium-emphasis">
          <NuxtLink :to="`/procurements`" class="text-decoration-none text-medium-emphasis hover-underline">Procurements</NuxtLink>
          <v-icon icon="mdi-chevron-right" size="14" />
          <span>{{ procurement.procurementNumber }}</span>
        </div>
      </div>

      <v-spacer />

      <!-- Admin Actions -->
      <template v-if="isAdmin && !isFinalizedOrCancelled">
        <v-btn
          color="error"
          variant="tonal"
          prepend-icon="mdi-cancel"
          :loading="cancelling"
          @click="showCancelConfirm = true"
        >
          Cancel
        </v-btn>
        <v-btn
          color="success"
          variant="flat"
          prepend-icon="mdi-check-decagram"
          :loading="finalizing"
          @click="showFinalizeConfirm = true"
        >
          Finalize → Create PO Items
        </v-btn>
      </template>
    </div>

    <!-- Meta Strip -->
    <v-card class="glass-card mb-6 border-s-lg" :style="{ borderSColor: 'rgb(var(--v-theme-primary))' }">
      <v-card-text class="pa-4">
        <v-row dense>
          <v-col v-if="isAdmin" cols="12" sm="6" md="3">
            <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Proforma Invoice</div>
            <NuxtLink v-if="procurement.invoiceId" :to="`/invoices/${procurement.invoiceId}`" class="text-body-1 font-weight-bold text-primary text-decoration-none hover-underline">
              {{ procurement.invoiceNumber || `#${procurement.invoiceId}` }}
            </NuxtLink>
            <div v-else class="text-body-1">—</div>
          </v-col>
          <v-col v-if="isAdmin" cols="12" sm="6" md="3">
            <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Customer</div>
            <div class="text-body-1 font-weight-bold">{{ procurement.customerName || '—' }}</div>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Created</div>
            <div class="text-body-1 font-weight-bold">{{ new Date(procurement.createdAt).toLocaleString() }}</div>
          </v-col>
          <v-col cols="12" sm="6" md="3" v-if="procurement.finalizedAt">
            <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Finalized At</div>
            <div class="text-body-1 font-weight-bold text-success">{{ new Date(procurement.finalizedAt).toLocaleString() }}</div>
          </v-col>
          <!-- Header-level Assigned Users -->
          <v-col v-if="isAdmin" cols="12">
            <div class="text-caption text-medium-emphasis font-weight-bold uppercase mb-1">Header-level Assigned Users</div>
            <div class="d-flex flex-wrap gap-2">
              <template v-if="headerPermissions.length">
                <v-chip
                  v-for="u in headerPermissions"
                  :key="u.id"
                  size="small"
                  variant="tonal"
                  color="primary"
                  prepend-icon="mdi-account"
                  closable
                  @click:close="deletePermission(procurement.id, u.id, true)"
                >
                  {{ u.userName }} ({{ u.permission }})
                </v-chip>
              </template>
              <v-btn 
                size="x-small" 
                variant="tonal" 
                color="primary" 
                prepend-icon="mdi-plus"
                @click="openHeaderAssign"
              >
                Assign
              </v-btn>
            </div>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- Items Grid -->
    <div v-for="(item, idx) in procurement.items" :key="item.id" class="mb-4">
      <v-card class="glass-card overflow-hidden" :class="{ 'opacity-60': item.itemStatus === 'Cancelled' }">
        <!-- Collapsed Summary -->
        <div 
          class="pa-3 d-flex align-center gap-3 cursor-pointer hover-bg" 
          @click="toggleExpand(item.id)"
        >
          <div class="text-caption text-medium-emphasis font-weight-bold" style="width: 24px;">{{ idx + 1 }}</div>
          
          <div style="flex: 1; min-width: 0;">
            <div class="d-flex align-center gap-2">
              <span class="font-weight-bold cell-pn text-truncate">{{ item.partNumberName }}</span>
              <v-tooltip v-if="item.partNumberDescription" location="bottom">
                <template #activator="{ props }">
                  <v-icon v-bind="props" icon="mdi-information-outline" size="14" color="grey" />
                </template>
                {{ item.partNumberDescription }}
              </v-tooltip>
            </div>
            <div class="text-caption text-medium-emphasis text-truncate">{{ item.currentSupplierName || 'No supplier selected' }}</div>
            <!-- Assigned Users Summary -->
            <div v-if="isAdmin && itemPermissions[item.id]?.length" class="d-flex flex-wrap gap-1 mt-1">
              <v-chip
                v-for="p in itemPermissions[item.id]"
                :key="p.id"
                size="x-small"
                variant="tonal"
                color="primary"
                class="px-1"
                style="height: 16px; font-size: 9px;"
              >
                {{ p.userName }}
              </v-chip>
            </div>
          </div>

          <div class="d-flex align-center gap-4 text-center" style="width: 350px;">
            <div style="width: 80px;">
              <div class="text-caption text-medium-emphasis uppercase font-weight-bold" style="font-size: 9px;">Qty</div>
              <input 
                v-if="!isFinalizedOrCancelled"
                type="number" 
                v-model.number="item.qty" 
                class="inline-input text-center" 
                @change="updateItem(item, { qty: item.qty })"
                @click.stop
              />
              <div v-else class="text-body-2 font-weight-bold">{{ item.qty }}</div>
            </div>
            <div style="width: 100px;">
              <div class="text-caption text-medium-emphasis uppercase font-weight-bold" style="font-size: 9px;">Unit Price</div>
              <div class="d-flex align-center justify-center">
                <span class="text-caption mr-1">$</span>
                <input 
                  v-if="!isFinalizedOrCancelled"
                  type="number" 
                  v-model.number="item.unitPrice" 
                  class="inline-input text-center" 
                  step="0.01"
                  @change="updateItem(item, { unitPrice: item.unitPrice })"
                  @click.stop
                />
                <div v-else class="text-body-2 font-weight-bold">{{ formatPrice(item.unitPrice) }}</div>
              </div>
            </div>
            <div style="width: 110px;">
              <div class="text-caption text-medium-emphasis uppercase font-weight-bold" style="font-size: 9px;">Total</div>
              <div class="text-body-2 font-weight-bold text-success">${{ formatPrice(item.qty * item.unitPrice) }}</div>
            </div>
          </div>

          <div style="width: 120px;" class="text-center">
            <v-menu v-if="!isFinalizedOrCancelled">
              <template #activator="{ props }">
                <v-chip 
                  v-bind="props" 
                  size="x-small" 
                  :color="itemStatusColor(item.itemStatus)" 
                  variant="flat" 
                  class="cursor-pointer font-weight-bold"
                  append-icon="mdi-chevron-down"
                >
                  {{ item.itemStatus }}
                </v-chip>
              </template>
              <v-list density="compact">
                <v-list-item v-for="s in ['Open', 'Sourcing', 'Ready', 'Cancelled']" :key="s" @click="updateItem(item, { itemStatus: s })">
                  <v-list-item-title class="text-caption">{{ s }}</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
            <v-chip v-else size="x-small" :color="itemStatusColor(item.itemStatus)" variant="flat" class="font-weight-bold">
              {{ item.itemStatus }}
            </v-chip>
          </div>

          <v-btn 
            icon 
            variant="text" 
            size="small" 
            :class="{ 'rotate-180': expanded.has(item.id) }"
          >
            <v-icon icon="mdi-chevron-down" />
          </v-btn>
        </div>

        <!-- Expanded Detail -->
        <v-expand-transition>
          <div v-if="expanded.has(item.id)">
            <v-divider />
            <div class="pa-4 bg-surface-variant-light">
              <!-- (A) Provenance Box -->
              <template v-if="isAdmin">
                <div class="text-caption font-weight-bold text-medium-emphasis mb-2 uppercase">Historical Provenance</div>
                <v-row dense class="mb-6">
                  <!-- RFQ -->
                  <v-col cols="12" md="4">
                    <v-card variant="outlined" class="pa-3 h-100 bg-surface">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-file-document-outline" size="18" color="primary" />
                        <span class="text-caption font-weight-bold uppercase">RFQ Snapshot</span>
                        <v-spacer />
                        <NuxtLink v-if="item.sourceRfqId" :to="`/rfqs/${item.sourceRfqId}`" target="_blank" class="text-caption text-primary text-decoration-none">View →</NuxtLink>
                      </div>
                      <div class="text-body-2 mb-1"><strong>{{ item.rfqName || '—' }}</strong> &middot; {{ item.rfqExType === 0 ? 'Warehouse' : item.rfqExType === 1 ? 'Vendor' : 'Customer' }}</div>
                      <div class="text-body-2">{{ item.rfqQty }} {{ item.rfqUnit }} @ {{ item.rfqCondition }}</div>
                      <div class="text-caption text-medium-emphasis mt-2" v-if="item.rfqNote">Note: {{ item.rfqNote }}</div>
                    </v-card>
                  </v-col>
                  <!-- Quote -->
                  <v-col cols="12" md="4">
                    <v-card variant="outlined" class="pa-3 h-100 bg-surface">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-currency-usd" size="18" color="info" />
                        <span class="text-caption font-weight-bold uppercase">Quote Snapshot</span>
                        <v-spacer />
                        <NuxtLink v-if="item.sourceQuoteId" :to="`/quotes/${item.sourceQuoteId}`" target="_blank" class="text-caption text-primary text-decoration-none">View →</NuxtLink>
                      </div>
                      <div class="text-body-2 mb-1"><strong>{{ item.quoteNumber || '—' }}</strong></div>
                      <div class="text-body-2">{{ item.quoteQty }} @ ${{ formatPrice(item.quoteUnitPrice) }} = ${{ formatPrice(item.quoteQty * item.quoteUnitPrice) }}</div>
                      <div class="text-body-2">{{ item.quoteCondition }} &middot; {{ item.quoteLeadTimeDays }}d lead</div>
                    </v-card>
                  </v-col>
                  <!-- Invoice -->
                  <v-col cols="12" md="4">
                    <v-card variant="outlined" class="pa-3 h-100 bg-surface">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-receipt-text-outline" size="18" color="success" />
                        <span class="text-caption font-weight-bold uppercase">Invoice Selection</span>
                      </div>
                      <div class="text-body-2 mb-1">The customer accepted:</div>
                      <div class="text-h6 font-weight-bold text-success">{{ item.acceptedQty }} @ ${{ formatPrice(item.acceptedUnitPrice) }}</div>
                      <v-divider class="my-2" />
                      <div class="text-body-2 text-medium-emphasis">Original Supplier: {{ item.supplierName || '—' }}</div>
                    </v-card>
                  </v-col>
                </v-row>

                <!-- (B) Source Assigned Users -->
                <div v-if="itemSourceUsers(item).length" class="mb-6">
                  <div class="text-caption font-weight-bold text-medium-emphasis mb-2 uppercase">Users assigned to source chain</div>
                  <div class="d-flex flex-wrap gap-2">
                    <v-chip v-for="u in itemSourceUsers(item)" :key="u.userId" size="small" variant="tonal" color="grey" prepend-icon="mdi-account">
                      {{ u.userName }} ({{ u.entityName }})
                    </v-chip>
                  </div>
                </div>
              </template>

              <!-- (C) Supplier Quotes Grid -->
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="text-caption font-weight-bold text-medium-emphasis uppercase">Supplier Quotes</div>
                <v-btn 
                  v-if="!isFinalizedOrCancelled"
                  size="x-small" 
                  color="primary" 
                  variant="flat" 
                  prepend-icon="mdi-plus"
                  @click="addSupplierQuote(item)"
                >
                  Add Quote
                </v-btn>
              </div>

              <div class="border rounded bg-surface overflow-hidden mb-6">
                <table class="proc-grid">
                  <thead>
                    <tr>
                      <th style="width: 50px;">Sel</th>
                      <th>Supplier</th>
                      <th>Alt P/N</th>
                      <th>Cond</th>
                      <th>Qty</th>
                      <th>Price ($)</th>
                      <th>Total</th>
                      <th>Lead Time</th>
                      <th>Note</th>
                      <th v-if="!isFinalizedOrCancelled" style="width: 80px;"></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="sq in item.supplierQuotes" :key="sq.id || `new-${sq.tempId}`" :class="{ 'bg-success-light': sq.isSelected }">
                      <td class="text-center">
                        <v-radio 
                          :model-value="sq.isSelected" 
                          :readonly="isFinalizedOrCancelled"
                          color="success"
                          density="compact"
                          hide-details
                          @click="selectSupplierQuote(item, sq)"
                        />
                      </td>
                      <td>
                        <input
                          type="text"
                          v-model="sq.supplierName"
                          class="quote-input"
                          placeholder="Name..."
                          :readonly="isFinalizedOrCancelled"
                          list="procurement-supplier-suggestions"
                          @input="onSupplierNameInput(sq, ($event.target as HTMLInputElement).value)"
                          @blur="saveSupplierQuote(item, sq)"
                        />
                      </td>
                      <td><input type="text" v-model="sq.alt" class="quote-input" :readonly="isFinalizedOrCancelled" @blur="saveSupplierQuote(item, sq)" /></td>
                      <td>
                        <select v-model="sq.condition" class="quote-input" :disabled="isFinalizedOrCancelled" @change="saveSupplierQuote(item, sq)">
                          <option value="NE">NE</option>
                          <option value="OH">OH</option>
                          <option value="SV">SV</option>
                          <option value="AR">AR</option>
                          <option value="RP">RP</option>
                        </select>
                      </td>
                      <td><input type="number" v-model.number="sq.qty" class="quote-input text-center" :readonly="isFinalizedOrCancelled" @blur="saveSupplierQuote(item, sq)" /></td>
                      <td><input type="number" v-model.number="sq.price" class="quote-input text-right" step="0.01" :readonly="isFinalizedOrCancelled" @blur="saveSupplierQuote(item, sq)" /></td>
                      <td class="text-right text-caption font-weight-bold px-2">${{ formatPrice(sq.qty * sq.price) }}</td>
                      <td><input type="text" v-model="sq.leadTime" class="quote-input" placeholder="e.g. 3-5 days" :readonly="isFinalizedOrCancelled" @blur="saveSupplierQuote(item, sq)" /></td>
                      <td><input type="text" v-model="sq.note" class="quote-input" :readonly="isFinalizedOrCancelled" @blur="saveSupplierQuote(item, sq)" /></td>
                      <td v-if="!isFinalizedOrCancelled" class="text-center">
                        <v-btn 
                          v-if="!sq.isSelected"
                          icon="mdi-delete" 
                          variant="text" 
                          size="x-small" 
                          color="error" 
                          @click="deleteSupplierQuote(item, sq)"
                        />
                      </td>
                    </tr>
                    <tr v-if="!item.supplierQuotes?.length">
                      <td colspan="10" class="pa-4 text-center text-caption text-medium-emphasis">No quotes yet.</td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <!-- (D) Per-Item Permissions (Admin only) -->
              <div v-if="isAdmin">
                <div class="text-caption font-weight-bold text-medium-emphasis mb-2 uppercase">Assigned Users (Item-specific)</div>
                <div class="d-flex flex-wrap gap-2 align-center">
                  <template v-if="itemPermissions[item.id]?.length">
                    <v-chip 
                      v-for="p in itemPermissions[item.id]" 
                      :key="p.id" 
                      size="small" 
                      variant="tonal" 
                      color="primary" 
                      closable
                      @click:close="deletePermission(item.id, p.id)"
                    >
                      {{ p.userName }}
                    </v-chip>
                  </template>
                  <v-btn 
                    size="x-small" 
                    variant="tonal" 
                    color="primary" 
                    prepend-icon="mdi-plus"
                    @click="openItemAssign(item.id)"
                  >
                    Assign
                  </v-btn>
                </div>
              </div>
            </div>
          </div>
        </v-expand-transition>
      </v-card>
    </div>

    <!-- Finalize Confirmation -->
    <v-dialog v-model="showFinalizeConfirm" max-width="500">
      <v-card>
        <v-card-title class="text-h6" :class="{ 'text-warning': !allItemsReady }">
          {{ allItemsReady ? 'Finalize Procurement?' : 'Not All Items are Ready!' }}
        </v-card-title>
        <v-card-text>
          <div v-if="!allItemsReady" class="mb-4 text-warning font-weight-bold">
            Some items are still in Open or Sourcing status. Are you sure you want to finalize?
          </div>
          This will lock this procurement and create unassigned PO Items ready for Purchase Orders.
          <v-textarea v-model="finalizeNotes" label="Finalization Notes" variant="outlined" density="compact" rows="2" class="mt-4" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showFinalizeConfirm = false">Back</v-btn>
          <v-btn :color="allItemsReady ? 'success' : 'warning'" variant="flat" :loading="finalizing" @click="finalize">
            Finalize Anyway
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Cancel Confirmation -->
    <v-dialog v-model="showCancelConfirm" max-width="500">
      <v-card>
        <v-card-title class="text-h6 text-error">Cancel Procurement?</v-card-title>
        <v-card-text>This action cannot be undone. All edits in this snapshot will be lost.</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showCancelConfirm = false">Back</v-btn>
          <v-btn color="error" variant="flat" :loading="cancelling" @click="cancel">Cancel Procurement</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Single Item Assign Dialog -->
    <v-dialog v-model="showItemAssign" max-width="400">
      <v-card>
        <v-card-title>Assign User to {{ assignTargetItemId === null ? 'Procurement' : 'Item' }}</v-card-title>
        <v-card-text>
          <v-autocomplete
            v-model="assignTargetUserId"
            :items="users"
            item-title="name"
            item-value="id"
            label="Select User"
            variant="outlined"
            density="compact"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showItemAssign = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!assignTargetUserId" @click="doItemAssign">Assign</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Shared supplier autocomplete suggestions -->
    <datalist id="procurement-supplier-suggestions">
      <option
        v-for="s in supplierSuggestions.filter(x => x.status === 'Approved')"
        :key="s.id"
        :value="s.name"
      >{{ s.status }}</option>
    </datalist>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const loading = ref(true)
const procurement = ref<any>(null)
const expanded = ref(new Set<number>())
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const finalizing = ref(false)
const showFinalizeConfirm = ref(false)
const finalizeNotes = ref('')

const cancelling = ref(false)
const showCancelConfirm = ref(false)

const headerPermissions = ref<any[]>([])
const itemPermissions = ref<Record<number, any[]>>({})
const users = ref<any[]>([])
const showItemAssign = ref(false)
const assignTargetItemId = ref<number | null>(null) // null means header
const assignTargetUserId = ref<number | null>(null)

// Supplier autocomplete (shared across all quote rows via a single <datalist>)
const supplierSuggestions = ref<{ id: number; name: string; username?: string; status: string }[]>([])

const isFinalizedOrCancelled = computed(() => 
  procurement.value?.status === 'Finalized' || procurement.value?.status === 'Cancelled'
)

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

function statusColor(status: string) {
  const map: Record<string, string> = { 
    Open: 'grey', 
    Sourcing: 'info',
    InProgress: 'warning', 
    Finalized: 'success', 
    Cancelled: 'error' 
  }
  return map[status] || 'grey'
}

function itemStatusColor(status: string) {
  const map: Record<string, string> = { Open: 'grey', Sourcing: 'info', Ready: 'success', Cancelled: 'error' }
  return map[status] || 'grey'
}

const allItemsReady = computed(() => {
  if (!procurement.value?.items) return false
  const activeItems = procurement.value.items.filter((i: any) => i.itemStatus !== 'Cancelled')
  return activeItems.length > 0 && activeItems.every((i: any) => i.itemStatus === 'Ready')
})

async function loadDetail() {
  loading.value = true
  try {
    const data = await api.get<any>(`/procurements/${route.params.id}`)
    procurement.value = data
    
    if (isAdmin.value) {
      // Map permissions from the flat list in response
      const allPerms = data.assignedUsers || []
      headerPermissions.value = allPerms.filter((p: any) => p.entityId === String(data.id))
      
      const itemMap: Record<number, any[]> = {}
      data.items.forEach((it: any) => {
        itemMap[it.id] = allPerms.filter((p: any) => p.entityId === String(it.id))
      })
      itemPermissions.value = itemMap

      if (!users.value.length) {
        const allUsers = await api.get<any[]>('/users')
        const allowed = ['GHS', 'SNP', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
        users.value = allUsers.filter(u => allowed.includes(u.name) || allowed.includes(u.username))
      }
    }
  } catch (e) {
    showSnack('Failed to load detail', 'error')
  } finally {
    loading.value = false
  }
}

async function loadHeaderPermissions() {
  try {
    const perms = await api.get<any[]>(`/permissions/Procurement/${procurement.value.id}`)
    headerPermissions.value = perms
  } catch {
    headerPermissions.value = []
  }
}

async function loadItemPermissions(itemId: number) {
  try {
    const perms = await api.get<any[]>(`/permissions/Procurement/${itemId}`)
    itemPermissions.value[itemId] = perms
  } catch {
    itemPermissions.value[itemId] = []
  }
}

function toggleExpand(id: number) {
  if (expanded.value.has(id)) expanded.value.delete(id)
  else expanded.value.add(id)
}

function itemSourceUsers(item: any) {
  if (!procurement.value?.sourceAssignedUsers) return []
  return procurement.value.sourceAssignedUsers.filter((u: any) => 
    (u.entityName === 'RFQ' && u.entityId === String(item.sourceRfqId)) ||
    (u.entityName === 'Quote' && u.entityId === String(item.sourceQuoteId))
  )
}

// ── Item Edits ──
async function updateItem(item: any, patch: any) {
  if (isFinalizedOrCancelled.value) return
  try {
    await api.patch(`/procurements/${procurement.value.id}/items/${item.id}`, patch)
    Object.assign(item, patch)
    showSnack('Item updated', 'success')
  } catch {
    showSnack('Update failed', 'error')
    loadDetail() // Revert local state
  }
}

// ── Supplier Quote CRUD ──
function addSupplierQuote(item: any) {
  if (!item.supplierQuotes) item.supplierQuotes = []
  item.supplierQuotes.push({
    tempId: Date.now(),
    supplierName: '',
    price: 0,
    qty: item.qty,
    condition: item.rfqCondition || 'NE',
    leadTime: '',
    note: '',
    isSelected: false
  })
}

async function saveSupplierQuote(item: any, sq: any) {
  if (isFinalizedOrCancelled.value || !sq.supplierName.trim()) return
  try {
    const typed = (sq.supplierName || '').trim().toLowerCase()
    const match = supplierSuggestions.value.find(s => s.name.toLowerCase() === typed)
    const resolvedSupplierId = sq.supplierId || (match ? match.id : null)
    const payload = {
      id: sq.id || null,
      supplierId: resolvedSupplierId,
      supplierName: sq.supplierName,
      price: sq.price,
      qty: sq.qty,
      condition: sq.condition,
      leadTime: sq.leadTime,
      note: sq.note
    }
    const res = await api.post<any>(`/procurements/${procurement.value.id}/items/${item.id}/supplier-quotes`, payload)
    Object.assign(sq, res)
    showSnack('Quote saved')
  } catch {
    showSnack('Save failed', 'error')
  }
}

// ── Supplier Autocomplete ──
let supplierSearchDebounce: any = null
function searchSupplier(val: string) {
  clearTimeout(supplierSearchDebounce)
  if (!val || val.length < 1) {
    supplierSuggestions.value = []
    return
  }
  supplierSearchDebounce = setTimeout(async () => {
    try {
      supplierSuggestions.value = await api.get<{ id: number; name: string; username?: string; status: string }[]>(`/suppliers/search?q=${encodeURIComponent(val)}`)
    } catch {
      supplierSuggestions.value = []
    }
  }, 300)
}

function onSupplierNameInput(sq: any, val: string) {
  sq.supplierName = val
  const typed = (val || '').trim().toLowerCase()
  const match = supplierSuggestions.value.find(s => s.name.toLowerCase() === typed)
  sq.supplierId = match ? match.id : null
  searchSupplier(val)
}

async function selectSupplierQuote(item: any, sq: any) {
  if (isFinalizedOrCancelled.value || !sq.id) return
  try {
    await api.post(`/procurements/${procurement.value.id}/items/${item.id}/supplier-quotes/${sq.id}/select`, {})
    item.supplierQuotes.forEach((q: any) => q.isSelected = (q.id === sq.id))
    await loadDetail()
    showSnack('Supplier selected')
  } catch {
    showSnack('Selection failed', 'error')
  }
}

async function deleteSupplierQuote(item: any, sq: any) {
  if (isFinalizedOrCancelled.value) return
  if (!sq.id) {
    item.supplierQuotes = item.supplierQuotes.filter((q: any) => q !== sq)
    return
  }
  try {
    await api.del(`/procurements/${procurement.value.id}/items/${item.id}/supplier-quotes/${sq.id}`)
    item.supplierQuotes = item.supplierQuotes.filter((q: any) => q.id !== sq.id)
    showSnack('Quote removed')
  } catch {
    showSnack('Delete failed', 'error')
  }
}

// ── Permissions ──
function openHeaderAssign() {
  assignTargetItemId.value = null
  assignTargetUserId.value = null
  showItemAssign.value = true
}

function openItemAssign(itemId: number) {
  assignTargetItemId.value = itemId
  assignTargetUserId.value = null
  showItemAssign.value = true
}

async function doItemAssign() {
  if (!assignTargetUserId.value) return
  const isHeader = assignTargetItemId.value === null
  const targetId = isHeader ? procurement.value.id : assignTargetItemId.value

  try {
    await api.post('/permissions/assign', {
      userId: assignTargetUserId.value,
      entityName: 'Procurement',
      entityId: String(targetId),
      permission: 'Edit'
    })
    
    if (isHeader) await loadHeaderPermissions()
    else await loadItemPermissions(targetId as number)

    showItemAssign.value = false
    showSnack('User assigned')
  } catch {
    showSnack('Assignment failed', 'error')
  }
}

async function deletePermission(targetId: number, permId: number, isHeader = false) {
  try {
    await api.del(`/permissions/${permId}`)
    if (isHeader) await loadHeaderPermissions()
    else await loadItemPermissions(targetId)
    showSnack('Permission removed')
  } catch {
    showSnack('Removal failed', 'error')
  }
}

// ── Finalize / Cancel ──
async function finalize() {
  finalizing.value = true
  try {
    await api.post(`/procurements/${procurement.value.id}/finalize`, { notes: finalizeNotes.value })
    showSnack('Procurement finalized! Redirecting to Purchase Orders...', 'success')
    setTimeout(() => router.push('/purchase-orders'), 1500)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Finalization failed', 'error')
  } finally {
    finalizing.value = false
    showFinalizeConfirm.value = false
  }
}

async function cancel() {
  cancelling.value = true
  try {
    await api.post(`/procurements/${procurement.value.id}/cancel`, {})
    showSnack('Procurement cancelled', 'warning')
    await loadDetail()
  } catch {
    showSnack('Cancellation failed', 'error')
  } finally {
    cancelling.value = false
    showCancelConfirm.value = false
  }
}

onMounted(loadDetail)
</script>

<style scoped>
.cell-pn { font-family: 'Roboto Mono', monospace; color: rgb(var(--v-theme-primary)); }
.hover-bg:hover { background-color: rgba(var(--v-theme-on-surface), 0.04); }
.bg-surface-variant-light { background-color: rgba(var(--v-theme-on-surface), 0.02); }
.rotate-180 { transform: rotate(180deg); }

.inline-input {
  width: 100%;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 4px;
  background: rgba(var(--v-theme-surface), 1);
  font-size: 13px;
  font-weight: 600;
  padding: 2px 4px;
  outline: none;
}
.inline-input:focus { border-color: rgb(var(--v-theme-primary)); }

.proc-grid { width: 100%; border-collapse: collapse; font-size: 12px; }
.proc-grid th {
  background: rgba(var(--v-theme-on-surface), 0.05);
  text-align: left;
  padding: 8px;
  font-weight: bold;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.05em;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.proc-grid td { padding: 4px 8px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05); vertical-align: middle; }
.quote-input {
  width: 100%;
  border: none;
  background: transparent;
  padding: 4px;
  font-size: 12px;
  outline: none;
}
.quote-input:focus { background: rgba(var(--v-theme-primary), 0.05); }
.bg-success-light { background-color: rgba(74, 222, 128, 0.1) !important; }
.opacity-60 { opacity: 0.6; }
</style>
