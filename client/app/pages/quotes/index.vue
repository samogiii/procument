<template>
  <DataListPage
    title="Quotes"
    :headers="headers"
    api-url="/quotes"
    :status-options="quoteStatusOptions"
    detail-route="/quotes"
    :server-side="true"
    :extra-params="extraParams"
    page-key="quotes"
    :show-total-sum="true"
  >
    <template #actions>
      <v-btn
        v-if="isAmir"
        prepend-icon="mdi-download-multiple"
        variant="tonal"
        color="primary"
        size="small"
        @click="showBulkDownload = true"
      >
        Bulk Download
      </v-btn>
      <v-btn
        v-if="isAdmin"
        prepend-icon="mdi-shield-account"
        variant="tonal"
        @click="showBulkPerms = true"
      >
        Manage Permissions
      </v-btn>
    </template>

    <template #filters>
      <v-text-field
        v-model="pnSearch"
        label="Search by P/N"
        prepend-inner-icon="mdi-cog-outline"
        hide-details
        clearable
        density="compact"
        variant="outlined"
        class="mx-2"
        style="min-width: 180px; max-width: 280px;"
      />
      <v-autocomplete
        v-model="userFilter"
        :items="userOptions"
        label="User"
        hide-details
        multiple
        chips
        closable-chips
        clearable
        density="compact"
        variant="outlined"
        class="mx-2"
        style="min-width: 140px; max-width: 240px;"
      />
      <v-autocomplete
        v-model="customerFilter"
        :items="customerOptions"
        item-title="title"
        item-value="value"
        label="Customer"
        hide-details
        multiple
        chips
        closable-chips
        clearable
        density="compact"
        variant="outlined"
        class="mx-2"
        style="min-width: 140px; max-width: 260px;"
      />
      <v-btn
            v-if="hasActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearFilters"
          >
            Clear
          </v-btn>
    </template>

    <!-- Column filter header slots — server-side: toggling checks re-fetches from backend -->
    <template #header.quoteNumber="{ column, toggleSort, isSorted, sortBy }">
      <div class="q-th-inner">
        <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
          <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
        </span>
        <v-menu :close-on-content-click="false" max-width="260">
          <template #activator="{ props: mp }">
            <v-btn v-bind="mp" :icon="quoteNumberFilter?.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="quoteNumberFilter?.length ? 'primary' : undefined" class="q-filter-btn" @click.stop />
          </template>
          <v-card class="pa-2" min-width="220">
            <v-text-field v-model="colSearch.quoteNumber" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
            <div style="max-height:220px;overflow-y:auto;">
              <v-checkbox
                v-for="val in filteredQuoteNumberColOptions"
                :key="val"
                :label="val"
                :model-value="quoteNumberFilter.includes(val)"
                density="compact"
                hide-details
                @update:model-value="toggleColFilter(quoteNumberFilter, val)"
              />
              <div v-if="filteredQuoteNumberColOptions.length === 0" class="text-caption text-medium-emphasis pa-2">No options</div>
            </div>
            <v-btn v-if="quoteNumberFilter?.length" size="x-small" variant="text" color="error" class="mt-1" @click="quoteNumberFilter.splice(0)">Clear</v-btn>
          </v-card>
        </v-menu>
      </div>
    </template>

    <template #header.customerCode="{ column, toggleSort, isSorted, sortBy }">
      <div class="q-th-inner">
        <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
          <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
        </span>
        <v-menu :close-on-content-click="false" max-width="260">
          <template #activator="{ props: mp }">
            <v-btn v-bind="mp" :icon="customerFilter?.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="customerFilter?.length ? 'primary' : undefined" class="q-filter-btn" @click.stop />
          </template>
          <v-card class="pa-2" min-width="220">
            <v-text-field v-model="colSearch.customer" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
            <div style="max-height:220px;overflow-y:auto;">
              <v-checkbox
                v-for="opt in filteredCustomerColOptions"
                :key="opt.value"
                :label="opt.title"
                :model-value="customerFilter.includes(opt.value)"
                density="compact"
                hide-details
                @update:model-value="toggleColFilter(customerFilter, opt.value)"
              />
              <div v-if="filteredCustomerColOptions.length === 0" class="text-caption text-medium-emphasis pa-2">No options</div>
            </div>
            <v-btn v-if="customerFilter?.length" size="x-small" variant="text" color="error" class="mt-1" @click="customerFilter.splice(0)">Clear</v-btn>
          </v-card>
        </v-menu>
      </div>
    </template>

    <template #header.status="{ column, toggleSort, isSorted, sortBy }">
      <div class="q-th-inner">
        <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
          <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
        </span>
        <v-menu :close-on-content-click="false" max-width="260">
          <template #activator="{ props: mp }">
            <v-btn v-bind="mp" :icon="statusFilter?.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="statusFilter?.length ? 'primary' : undefined" class="q-filter-btn" @click.stop />
          </template>
          <v-card class="pa-2" min-width="220">
            <v-text-field v-model="colSearch.status" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
            <div style="max-height:220px;overflow-y:auto;">
              <v-checkbox
                v-for="val in filteredStatusColOptions"
                :key="val"
                :label="val"
                :model-value="statusFilter.includes(val)"
                density="compact"
                hide-details
                @update:model-value="toggleColFilter(statusFilter, val)"
              />
            </div>
            <v-btn v-if="statusFilter?.length" size="x-small" variant="text" color="error" class="mt-1" @click="statusFilter.splice(0)">Clear</v-btn>
          </v-card>
        </v-menu>
      </div>
    </template>

    <template #header.rfqName="{ column, toggleSort, isSorted, sortBy }">
      <div class="q-th-inner">
        <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
          <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
        </span>
        <v-menu :close-on-content-click="false" max-width="260">
          <template #activator="{ props: mp }">
            <v-btn v-bind="mp" :icon="rfqFilter?.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="rfqFilter?.length ? 'primary' : undefined" class="q-filter-btn" @click.stop />
          </template>
          <v-card class="pa-2" min-width="220">
            <v-text-field v-model="colSearch.rfq" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
            <div style="max-height:220px;overflow-y:auto;">
              <v-checkbox
                v-for="val in filteredRfqColOptions"
                :key="val"
                :label="val"
                :model-value="rfqFilter.includes(val)"
                density="compact"
                hide-details
                @update:model-value="toggleColFilter(rfqFilter, val)"
              />
              <div v-if="filteredRfqColOptions.length === 0" class="text-caption text-medium-emphasis pa-2">No options</div>
            </div>
            <v-btn v-if="rfqFilter?.length" size="x-small" variant="text" color="error" class="mt-1" @click="rfqFilter.splice(0)">Clear</v-btn>
          </v-card>
        </v-menu>
      </div>
    </template>

    <template #header.assignedUsers="{ column }">
      <div class="q-th-inner">
        <span>{{ column.title }}</span>
        <v-menu :close-on-content-click="false" max-width="260">
          <template #activator="{ props: mp }">
            <v-btn v-bind="mp" :icon="userFilter?.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="userFilter?.length ? 'primary' : undefined" class="q-filter-btn" @click.stop />
          </template>
          <v-card class="pa-2" min-width="220">
            <v-text-field v-model="colSearch.user" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
            <div style="max-height:220px;overflow-y:auto;">
              <v-checkbox
                v-for="val in filteredUserColOptions"
                :key="val"
                :label="val"
                :model-value="userFilter.includes(val)"
                density="compact"
                hide-details
                @update:model-value="toggleColFilter(userFilter, val)"
              />
              <div v-if="filteredUserColOptions.length === 0" class="text-caption text-medium-emphasis pa-2">No options</div>
            </div>
            <v-btn v-if="userFilter?.length" size="x-small" variant="text" color="error" class="mt-1" @click="userFilter.splice(0)">Clear</v-btn>
          </v-card>
        </v-menu>
      </div>
    </template>

    <template #item.quoteNumber="{ item }">
      <nuxt-link :to="`/quotes/${item.id}`" class="text-primary font-weight-bold text-decoration-none" @click.stop>
        {{ item.quoteNumber || `#${item.id}` }}
      </nuxt-link>
    </template>

    <template #item.rfqName="{ item }">
      <nuxt-link v-if="item.rfqId" :to="`/rfqs/${item.rfqId}`" class="text-primary text-decoration-none" @click.stop>
        {{ item.rfqName || `RFQ #${item.rfqId}` }}
      </nuxt-link>
      <span v-else class="text-medium-emphasis">—</span>
    </template>

    <template #item.customerName="{ item }">
      <span
        v-if="item.customerName"
        class="text-primary text-decoration-none"
        style="cursor:pointer;"
        @click.stop="router.push(`/catalog/customers?search=${encodeURIComponent(item.customerName)}`)"
      >
        <template v-if="isAdmin">{{ item.customerName }}<span v-if="item.customerCode" class="text-medium-emphasis ml-1">({{ item.customerCode }})</span></template>
        <template v-else>{{ item.customerCode || '—' }}</template>
      </span>
      <span v-else class="text-medium-emphasis">—</span>
    </template>

    <template #item.status="{ item }">
      <v-menu v-if="isAdmin" :close-on-content-click="true">
        <template #activator="{ props: mp }">
          <v-chip
            :color="statusColor(item.status)"
            v-bind="mp"
            size="small"
            class="cursor-pointer"
            append-icon="mdi-chevron-down"
            @click.stop
          >
            {{ item.status }}
          </v-chip>
        </template>
        <v-list density="compact" style="min-width: 160px">
          <v-list-subheader>Change Status</v-list-subheader>
          <v-list-item
            v-for="s in quoteStatuses"
            :key="s.value"
            :active="item.status === s.value"
            @click="onStatusClick(item, s.value)"
          >
            <template #prepend>
              <v-icon :icon="s.icon" :color="s.color" size="18" />
            </template>
            <v-list-item-title>{{ s.label }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
      <StatusChip v-else :status="item.status" />
    </template>

    <template #item.totalAmount="{ item }">
      ${{ formatPrice(item.totalAmount) }}
    </template>

    <template #item.createdAt="{ item }">
      {{ item.createdAt ? new Date(item.createdAt).toLocaleDateString() : '—' }}
    </template>

    <template #item.sentAt="{ item }">
      {{ item.sentAt ? new Date(item.sentAt).toLocaleString() : '—' }}
    </template>

    <template #item.assignedUsers="{ item }">
      <div class="d-flex flex-wrap gap-1 py-1">
        <v-chip
          v-for="u in item.assignedUsers"
          :key="u.id"
          size="x-small"
          color="indigo"
          variant="tonal"
        >{{ u.name }}</v-chip>
        <span v-if="!item.assignedUsers?.length" class="text-medium-emphasis">—</span>
      </div>
    </template>

    <template #item.actions="{ item }">
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/quotes/${item.id}`" />
    </template>

  </DataListPage>

  <BulkPermissionManager v-model="showBulkPerms" entity-name="Quote" />
  <BulkQuoteDownload v-model="showBulkDownload" />

  <!-- Rejection dialog -->
  <v-dialog v-model="showRejectDialog" max-width="450">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-close-circle" color="error" class="mr-2" />
        Reject Quote
      </v-card-title>
      <v-card-text class="pa-4">
        <v-textarea
          v-model="rejectionNote"
          label="Rejection reason"
          rows="3"
          auto-grow
          variant="outlined"
        />
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
        <v-btn color="error" variant="tonal" :loading="statusSaving" @click="confirmReject">Reject</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Under $1000 Warning Dialog -->
  <v-dialog v-model="showUnder1000Warning" max-width="480" persistent>
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-alert-circle-outline" color="warning" class="mr-2" />
        Low Price Warning
      </v-card-title>
      <v-card-text class="pa-4">
        <div class="text-body-1 mb-3">The following items have a Total Price under <strong>$1,000</strong>:</div>
        <v-list density="compact" class="mb-3" bg-color="transparent">
          <v-list-item
            v-for="qi in under1000Items"
            :key="qi.id ?? qi.rfqItemId"
            :title="qi.partNumberName || 'Unknown part'"
            :subtitle="'Total: $' + formatPrice(qi.totalPrice)"
            prepend-icon="mdi-alert"
            color="warning"
          />
        </v-list>
        <div class="text-body-2 text-medium-emphasis">Are you sure you want to Accept this quote?</div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="cancelUnder1000">No, Cancel</v-btn>
        <v-btn color="warning" variant="flat" :loading="statusSaving" @click="confirmUnder1000Accept">Yes, Accept Anyway</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
    {{ snackbarText }}
  </v-snackbar>
</template>

<script setup lang="ts">
const router = useRouter()
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const isAmir = computed(() => authStore.isAmir)
const { statusColor } = useStatusColor()

const quoteStatuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Accepted', label: 'Accepted', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle', color: 'error' },
]

const showRejectDialog = ref(false)
const rejectingItem = ref<any>(null)
const rejectionNote = ref('')
const statusSaving = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const showUnder1000Warning = ref(false)
const under1000Items = ref<any[]>([])
const under1000PendingItem = ref<any>(null)

function onStatusClick(item: any, status: string) {
  if (status === item.status) return
  if (status === 'Rejected') {
    rejectingItem.value = item
    rejectionNote.value = ''
    showRejectDialog.value = true
    return
  }
  if (status === 'Accepted') {
    checkUnder1000AndAccept(item)
    return
  }
  doChangeStatus(item, status)
}

async function checkUnder1000AndAccept(item: any) {
  try {
    const quote = await api.get<any>(`/quotes/${item.id}`)
    const cheapItems = (quote.items || []).filter((qi: any) =>
      qi.totalPrice != null && Number(qi.totalPrice) < 1000
    )
    if (cheapItems.length > 0) {
      under1000Items.value = cheapItems
      under1000PendingItem.value = item
      showUnder1000Warning.value = true
      return
    }
  } catch {}
  doChangeStatus(item, 'Accepted')
}

async function confirmUnder1000Accept() {
  showUnder1000Warning.value = false
  if (under1000PendingItem.value) {
    await doChangeStatus(under1000PendingItem.value, 'Accepted')
    under1000PendingItem.value = null
  }
}

function cancelUnder1000() {
  showUnder1000Warning.value = false
  under1000PendingItem.value = null
}

async function doChangeStatus(item: any, status: string, note?: string) {
  statusSaving.value = true
  try {
    await api.patch(`/quotes/${item.id}/status`, { status, rejectionNote: note || null })
    item.status = status
    item.rejectionNote = note || null
    snackbarText.value = `Status changed to ${status}`
    snackbarColor.value = 'success'
    snackbar.value = true
  } catch {
    snackbarText.value = 'Failed to change status'
    snackbarColor.value = 'error'
    snackbar.value = true
  } finally {
    statusSaving.value = false
  }
}

async function confirmReject() {
  if (rejectingItem.value) {
    await doChangeStatus(rejectingItem.value, 'Rejected', rejectionNote.value)
  }
  showRejectDialog.value = false
  rejectingItem.value = null
}
const showBulkPerms = ref(false)
const showBulkDownload = ref(false)

const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('quotes', {
  search: '',
  status: [] as string[],
  user: [] as string[],
  customer: [] as string[],
  rfq: [] as string[],
  pnSearch: '',
  quoteNumber: [] as string[],
})
const userFilter = pf.user
const customerFilter = pf.customer
const statusFilter = pf.status
const rfqFilter = pf.rfq
const quoteNumberFilter = pf.quoteNumber

// P/N Search — passed as server-side filter param
const pnSearch = pf.pnSearch

const allQuotes = ref<any[]>([])
const quoteStatusOptions = ['Draft', 'Sent', 'Accepted', 'Rejected']

onMounted(async () => {
  try {
    const res = await api.get<any>('/quotes?pageSize=9999')
    allQuotes.value = Array.isArray(res) ? res : (res.items || res.Items || [])
  } catch {}
})

// Helper to filter items based on a set of active filters
function filterQuotesBy(quotes: any[], activeFilters: { customer?: string[], status?: string[], user?: string[], rfq?: string[], quoteNumber?: string[] }) {
  let result = quotes;
  
  if (activeFilters.customer?.length) {
    result = result.filter(q => 
      activeFilters.customer.includes(q.customerName) || 
      (q.customerCode && activeFilters.customer.includes(q.customerCode))
    );
  }
  
  if (activeFilters.status?.length) {
    result = result.filter(q => activeFilters.status.includes(q.status));
  }
  
  if (activeFilters.user?.length) {
    result = result.filter(q => 
      (q.assignedUsers || []).some((u: any) => activeFilters.user.includes(u.name))
    );
  }
  
  if (activeFilters.rfq?.length) {
    result = result.filter(q => {
      const displayName = q.rfqName || (q.rfqId ? `RFQ #${q.rfqId}` : '');
      return activeFilters.rfq.includes(displayName);
    });
  }

  if (activeFilters.quoteNumber?.length) {
    result = result.filter(q => activeFilters.quoteNumber.includes(q.quoteNumber));
  }
  
  return result;
}

// ── Cascading Dropdown Options (Non-self-filtering to preserve multi-select checkboxes) ──

const customerColOptions = computed(() => {
  const matchingQuotes = filterQuotesBy(allQuotes.value, {
    status: statusFilter.value,
    user: userFilter.value,
    rfq: rfqFilter.value,
    quoteNumber: quoteNumberFilter.value
  });
  
  const map = new Map<string, string>(); // name → code
  for (const q of matchingQuotes) {
    if (q.customerName && !map.has(q.customerName)) {
      map.set(q.customerName, q.customerCode || '');
    }
  }
  
  return Array.from(map.entries())
    .map(([name, code]) => ({ title: code || '-', value: name }))
    .sort((a, b) => a.title.localeCompare(b.title));
});

const userColOptions = computed(() => {
  const matchingQuotes = filterQuotesBy(allQuotes.value, {
    customer: customerFilter.value,
    status: statusFilter.value,
    rfq: rfqFilter.value,
    quoteNumber: quoteNumberFilter.value
  });
  
  const names = new Set<string>();
  for (const q of matchingQuotes) {
    for (const u of q.assignedUsers || []) {
      if (u.name) names.add(u.name);
    }
  }
  return [...names].sort();
});

const statusColOptions = computed(() => {
  const matchingQuotes = filterQuotesBy(allQuotes.value, {
    customer: customerFilter.value,
    user: userFilter.value,
    rfq: rfqFilter.value,
    quoteNumber: quoteNumberFilter.value
  });
  
  const statuses = new Set<string>();
  for (const q of matchingQuotes) {
    if (q.status) statuses.add(q.status);
  }
  
  return quoteStatusOptions.filter(s => statuses.has(s));
});

const rfqColOptions = computed(() => {
  const matchingQuotes = filterQuotesBy(allQuotes.value, {
    customer: customerFilter.value,
    status: statusFilter.value,
    user: userFilter.value,
    quoteNumber: quoteNumberFilter.value
  });
  
  const names = new Set<string>();
  for (const q of matchingQuotes) {
    if (q.rfqName) {
      names.add(q.rfqName);
    } else if (q.rfqId) {
      names.add(`RFQ #${q.rfqId}`);
    }
  }
  return [...names].sort();
});

const quoteNumberColOptions = computed(() => {
  const matchingQuotes = filterQuotesBy(allQuotes.value, {
    customer: customerFilter.value,
    status: statusFilter.value,
    user: userFilter.value,
    rfq: rfqFilter.value
  });
  
  const numbers = new Set<string>();
  for (const q of matchingQuotes) {
    if (q.quoteNumber) numbers.add(q.quoteNumber);
  }
  return [...numbers].sort();
});

// Top-bar autocomplete options mapped to the cascading column options
const userOptions = computed(() => userColOptions.value);
const customerOptions = computed(() => customerColOptions.value);

const extraParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}
  if (pnSearch.value) p.pnSearch = pnSearch.value
  if (userFilter.value?.length) p.assignedUserNames = userFilter.value
  if (customerFilter.value?.length) p.customerNames = customerFilter.value
  if (rfqFilter.value?.length) p.rfqNames = rfqFilter.value
  if (quoteNumberFilter.value?.length) p.quoteNumbers = quoteNumberFilter.value
  return p
})

// ── Excel-style column filters — server-side ──
// These refs feed directly into extraParams / DataListPage's status param,
// so toggling a checkbox triggers a real backend fetch with the filter applied.
const colSearch = reactive<Record<string, string>>({ customer: '', status: '', user: '', rfq: '', quoteNumber: '' })

/** Toggle a value in a reactive string array (add if absent, remove if present).
 *  Receives the unwrapped array — as Vue auto-unwraps refs in templates. */
function toggleColFilter(arr: string[], val: string) {
  const idx = arr.indexOf(val)
  if (idx >= 0) arr.splice(idx, 1)
  else arr.push(val)
}

/** Customer column options filtered by the in-dropdown search box only. */
const filteredCustomerColOptions = computed(() => {
  const s = colSearch.customer.toLowerCase()
  return customerColOptions.value.filter(o => !s || o.title.toLowerCase().includes(s) || o.value.toLowerCase().includes(s))
})

/** Status column options — static list, filtered by search box. */
const filteredStatusColOptions = computed(() => {
  const s = colSearch.status.toLowerCase()
  return statusColOptions.value.filter(v => !s || v.toLowerCase().includes(s))
})

/** User column options filtered by the in-dropdown search box only. */
const filteredUserColOptions = computed(() => {
  const s = colSearch.user.toLowerCase()
  return userColOptions.value.filter(v => !s || v.toLowerCase().includes(s))
})

/** RFQ column options filtered by the in-dropdown search box only. */
const filteredRfqColOptions = computed(() => {
  const s = colSearch.rfq.toLowerCase()
  return rfqColOptions.value.filter(v => !s || v.toLowerCase().includes(s))
})

/** Quote# column options filtered by the in-dropdown search box only. */
const filteredQuoteNumberColOptions = computed(() => {
  const s = colSearch.quoteNumber.toLowerCase()
  return quoteNumberColOptions.value.filter(v => !s || v.toLowerCase().includes(s))
})

const headers = [
  { title: 'Quote #', key: 'quoteNumber' },
  { title: 'RFQ Name', key: 'rfqName' },
  { title: 'Customer', key: 'customerCode' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  { title: 'Sent At', key: 'sentAt' },
  { title: 'Created', key: 'createdAt' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>

<style scoped>
.q-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.q-filter-btn { opacity: 0.5; flex-shrink: 0; }
.q-filter-btn:hover, .q-filter-btn.v-btn--active { opacity: 1; }
</style>
