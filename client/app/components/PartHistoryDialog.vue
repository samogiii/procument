<template>
  <v-dialog v-model="model" max-width="1400" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center gap-2 pa-4">
        <v-icon icon="mdi-history" color="primary" />
        <div class="flex-grow-1" style="min-width: 0;">
          <div class="text-h6 text-truncate">
            Part History
            <span v-if="data?.partNumberName" class="text-medium-emphasis">— {{ data.partNumberName }}</span>
          </div>
          <div class="text-caption text-medium-emphasis text-truncate">
            <span v-if="data?.description">{{ data.description }} · </span>
            <span v-if="data?.partCreatedAt">Part added {{ fmtDate(data.partCreatedAt) }} · </span>
            {{ data?.totalRfqCount || 0 }} RFQ(s) · {{ data?.totalRecordCount || 0 }} supplier record(s)
          </div>
        </div>
        <v-btn icon="mdi-close" size="small" variant="text" @click="model = false" />
      </v-card-title>

      <div v-if="data?.relatedPartNumbers?.length" class="px-4 pb-2 d-flex flex-wrap align-center gap-1">
        <span class="text-caption text-medium-emphasis mr-1">
          <v-icon icon="mdi-link-variant" size="13" class="mr-1" />Merged alternatives:
        </span>
        <v-chip v-for="n in data.relatedPartNumbers" :key="n" size="x-small" variant="tonal" color="blue-grey">
          {{ n }}
        </v-chip>
      </div>

      <v-tabs v-model="tab" density="compact" color="primary">
        <v-tab value="records">
          <v-icon icon="mdi-table-clock" size="16" class="mr-1" />
          Cost Records
          <v-chip size="x-small" variant="tonal" class="ml-2">{{ data?.records?.length || 0 }}</v-chip>
        </v-tab>
        <v-tab value="suppliers">
          <v-icon icon="mdi-domain" size="16" class="mr-1" />
          Suppliers
          <v-chip size="x-small" variant="tonal" class="ml-2">{{ data?.suppliers?.length || 0 }}</v-chip>
        </v-tab>
        <v-tab value="experts">
          <v-icon icon="mdi-account-group-outline" size="16" class="mr-1" />
          Experts
          <v-chip size="x-small" variant="tonal" class="ml-2">{{ data?.experts?.length || 0 }}</v-chip>
        </v-tab>
      </v-tabs>
      <v-divider />

      <v-card-text class="pa-0" style="min-height: 320px;">
        <v-progress-linear v-if="loading" indeterminate color="primary" />

        <div v-if="error" class="text-center text-error pa-8">{{ error }}</div>

        <template v-else-if="data">
          <!-- ── Cost Records ── -->
          <div v-show="tab === 'records'">
            <div class="d-flex flex-wrap align-center gap-2 pa-3">
              <v-text-field
                v-model="search"
                density="compact"
                variant="outlined"
                hide-details
                clearable
                placeholder="Search supplier, RFQ, cert, note…"
                prepend-inner-icon="mdi-magnify"
                style="max-width: 340px;"
              />
              <v-select
                v-model="supplierFilter"
                :items="supplierFilterItems"
                density="compact"
                variant="outlined"
                hide-details
                clearable
                multiple
                placeholder="Supplier"
                style="max-width: 240px;"
              />
              <v-select
                v-model="conditionFilter"
                :items="conditionFilterItems"
                density="compact"
                variant="outlined"
                hide-details
                clearable
                multiple
                placeholder="Condition"
                style="max-width: 180px;"
              />
              <v-spacer />
              <span class="text-caption text-medium-emphasis">
                {{ filteredRecords.length }} of {{ data.records.length }} shown
              </span>
            </div>

            <v-alert
              v-if="data.truncated"
              type="info"
              variant="tonal"
              density="compact"
              class="mx-3 mb-2 text-caption"
            >
              Showing the {{ data.records.length }} most recently touched of {{ data.totalRecordCount }} records.
              Supplier and expert totals below still cover all of them.
            </v-alert>

            <div v-if="!filteredRecords.length" class="text-center text-medium-emphasis pa-8">
              No supplier cost records found for this part.
            </div>

            <div v-else class="history-scroll">
              <table class="history-grid">
                <thead>
                  <tr>
                    <th class="sticky-col">Supplier</th>
                    <th>Cond</th>
                    <th>Alt P/N</th>
                    <th class="text-right">Qty</th>
                    <th class="text-right">Price</th>
                    <th>Cert</th>
                    <th>Tag Date</th>
                    <th>Lead</th>
                    <th class="text-right">Shipping</th>
                    <th>Ship Point</th>
                    <th>Recorded</th>
                    <th>Updated</th>
                    <th>Entered By</th>
                    <th>RFQ</th>
                    <th>Worked On By</th>
                    <th>Note</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="r in filteredRecords" :key="r.id" :class="{ 'shop-row': r.type === 'Shop' }">
                    <td class="sticky-col">
                      <div class="d-flex align-center gap-1">
                        <v-icon
                          v-if="r.type === 'Shop'"
                          icon="mdi-subdirectory-arrow-right"
                          size="13"
                          class="text-medium-emphasis"
                          title="Shop row"
                        />
                        <span class="font-weight-medium">{{ r.supplierName || '—' }}</span>
                        <v-icon
                          v-if="r.supplierDependency === 'Certificated'"
                          icon="mdi-certificate"
                          size="13"
                          color="success"
                          title="Certificated supplier"
                        />
                        <v-icon
                          v-if="r.supplierStatus === 'Pending'"
                          icon="mdi-clock-alert-outline"
                          size="13"
                          color="warning"
                          title="Supplier pending approval"
                        />
                        <v-icon
                          v-else-if="r.supplierStatus === 'Rejected'"
                          icon="mdi-alert-circle-outline"
                          size="13"
                          color="error"
                          title="Supplier rejected"
                        />
                      </div>
                      <div v-if="r.supplierCreatedAt" class="text-caption text-medium-emphasis">
                        Supplier since {{ fmtDate(r.supplierCreatedAt) }}
                      </div>
                    </td>
                    <td>{{ r.condition || '—' }}</td>
                    <td>{{ r.alt || '—' }}</td>
                    <td class="text-right">{{ r.qty ?? '—' }}<span v-if="r.unit" class="text-caption text-medium-emphasis ml-1">{{ r.unit }}</span></td>
                    <td class="text-right">
                      <span class="font-weight-medium">${{ fmtPrice(r.price) }}</span>
                      <div v-if="isStale(r)" class="text-caption" style="color:#ef5350;" title="Price older than 14 days">stale</div>
                    </td>
                    <td>{{ r.certName || '—' }}</td>
                    <td>{{ r.tagDate || '—' }}</td>
                    <td>{{ r.leadTime || '—' }}</td>
                    <td class="text-right">{{ r.shippingCost != null ? '$' + fmtPrice(r.shippingCost) : '—' }}</td>
                    <td>{{ r.shippingPoint || '—' }}</td>
                    <td class="nowrap">{{ fmtDateTime(r.createdAt) }}</td>
                    <td class="nowrap">{{ r.updatedAt ? fmtDateTime(r.updatedAt) : '—' }}</td>
                    <td>{{ r.enteredByName || '—' }}</td>
                    <td>
                      <NuxtLink :to="`/rfqs/${r.rfqId}`" class="rfq-link">{{ r.rfqName || `#${r.rfqId}` }}</NuxtLink>
                      <div class="text-caption text-medium-emphasis">{{ r.rfqStatus }} · {{ fmtDate(r.rfqCreatedAt) }}</div>
                    </td>
                    <td>
                      <div v-if="r.rfqOwnerName" class="text-caption">
                        <v-icon icon="mdi-account-star-outline" size="12" class="mr-1" />{{ r.rfqOwnerName }}
                      </div>
                      <div v-if="r.assignedUsers?.length" class="text-caption text-medium-emphasis">
                        {{ r.assignedUsers.join(', ') }}
                      </div>
                      <span v-if="!r.rfqOwnerName && !r.assignedUsers?.length">—</span>
                    </td>
                    <td class="note-cell">
                      <div v-if="r.note">{{ r.note }}</div>
                      <div v-if="r.myNotes" class="text-caption text-medium-emphasis font-italic">{{ r.myNotes }}</div>
                      <span v-if="!r.note && !r.myNotes">—</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- ── Suppliers ── -->
          <div v-show="tab === 'suppliers'" class="pa-3">
            <div v-if="!data.suppliers.length" class="text-center text-medium-emphasis pa-8">
              No suppliers recorded for this part yet.
            </div>
            <v-row v-else dense>
              <v-col v-for="s in data.suppliers" :key="s.supplierId" cols="12" md="6" lg="4">
                <v-card variant="outlined" class="pa-3 h-100">
                  <div class="d-flex align-center gap-1 mb-1">
                    <span class="text-body-2 font-weight-medium text-truncate">{{ s.supplierName }}</span>
                    <v-icon
                      v-if="s.dependency === 'Certificated'"
                      icon="mdi-certificate"
                      size="14"
                      color="success"
                      title="Certificated supplier"
                    />
                    <v-spacer />
                    <v-chip size="x-small" variant="tonal" :color="statusColor(s.status)">{{ s.status }}</v-chip>
                  </div>

                  <div class="text-caption text-medium-emphasis mb-2">
                    <div v-if="s.supplierCreatedAt">
                      <v-icon icon="mdi-calendar-plus" size="12" class="mr-1" />
                      Supplier created {{ fmtDate(s.supplierCreatedAt) }}
                    </div>
                    <div v-if="s.isLinked && s.linkedAt">
                      <v-icon icon="mdi-link-variant" size="12" class="mr-1" />
                      Linked to this part {{ fmtDate(s.linkedAt) }}
                    </div>
                    <div v-if="s.firstQuotedAt">
                      <v-icon icon="mdi-history" size="12" class="mr-1" />
                      Quoted {{ fmtDate(s.firstQuotedAt) }} → {{ fmtDate(s.lastQuotedAt) }}
                    </div>
                    <div v-else>
                      <v-icon icon="mdi-information-outline" size="12" class="mr-1" />
                      Linked supplier — never quoted for this part
                    </div>
                  </div>

                  <div class="d-flex flex-wrap gap-1 mb-2">
                    <v-chip v-for="c in s.conditions" :key="'c-' + c" size="x-small" variant="tonal" color="blue-grey">
                      {{ c }}
                    </v-chip>
                    <v-chip v-for="c in s.certs" :key="'ct-' + c" size="x-small" variant="tonal" color="teal">
                      <v-icon icon="mdi-file-certificate-outline" size="11" start />{{ c }}
                    </v-chip>
                  </div>

                  <div class="d-flex flex-wrap gap-3 text-caption">
                    <span><span class="text-medium-emphasis">Records</span> {{ s.recordCount }}</span>
                    <span v-if="s.lastPrice != null">
                      <span class="text-medium-emphasis">Last</span> ${{ fmtPrice(s.lastPrice) }}
                    </span>
                    <span v-if="s.minPrice != null">
                      <span class="text-medium-emphasis">Range</span>
                      ${{ fmtPrice(s.minPrice) }} – ${{ fmtPrice(s.maxPrice) }}
                    </span>
                  </div>
                </v-card>
              </v-col>
            </v-row>
          </div>

          <!-- ── Experts ── -->
          <div v-show="tab === 'experts'" class="pa-3">
            <div v-if="!data.experts.length" class="text-center text-medium-emphasis pa-8">
              No expert has been assigned to an RFQ containing this part.
            </div>
            <v-table v-else density="compact">
              <thead>
                <tr>
                  <th>Expert</th>
                  <th>Role</th>
                  <th class="text-right">RFQs owned</th>
                  <th class="text-right">RFQs assigned</th>
                  <th class="text-right">Costs entered</th>
                  <th>First activity</th>
                  <th>Last activity</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="e in data.experts" :key="e.userId">
                  <td class="font-weight-medium">
                    <v-icon icon="mdi-account-circle-outline" size="16" class="mr-1 text-medium-emphasis" />
                    {{ e.userName }}
                  </td>
                  <td><v-chip size="x-small" variant="tonal">{{ e.role || '—' }}</v-chip></td>
                  <td class="text-right">{{ e.ownedRfqCount }}</td>
                  <td class="text-right">{{ e.assignedRfqCount }}</td>
                  <td class="text-right">{{ e.recordCount }}</td>
                  <td class="nowrap">{{ fmtDate(e.firstActivity) }}</td>
                  <td class="nowrap">{{ fmtDate(e.lastActivity) }}</td>
                </tr>
              </tbody>
            </v-table>
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  partNumberId: number | null
}>()

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const api = useApi()

const model = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
})

const data = ref<any>(null)
const loading = ref(false)
const error = ref('')
const tab = ref('records')
const search = ref('')
const supplierFilter = ref<string[]>([])
const conditionFilter = ref<string[]>([])

const STALE_DAYS = 14

function fmtDate(d?: string | null) {
  if (!d) return '—'
  const dt = new Date(d)
  return isNaN(dt.getTime()) ? '—' : dt.toLocaleDateString()
}

function fmtDateTime(d?: string | null) {
  if (!d) return '—'
  const dt = new Date(d)
  if (isNaN(dt.getTime())) return '—'
  return `${dt.toLocaleDateString()} ${dt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`
}

function fmtPrice(v: any) {
  const n = Number(v)
  return isNaN(n) ? '0.00' : n.toFixed(2)
}

// A cost is "stale" when the row itself was last touched more than 14 days ago —
// same rule the suggestion chips use to hide prices.
function isStale(r: any) {
  const touched = new Date(r.updatedAt || r.createdAt)
  if (isNaN(touched.getTime())) return false
  return (Date.now() - touched.getTime()) / 86400000 > STALE_DAYS
}

function statusColor(status: string) {
  if (status === 'Pending') return 'warning'
  if (status === 'Rejected') return 'error'
  return 'success'
}

const supplierFilterItems = computed(() =>
  [...new Set((data.value?.records || []).map((r: any) => r.supplierName).filter(Boolean))].sort()
)

const conditionFilterItems = computed(() =>
  [...new Set((data.value?.records || []).map((r: any) => r.condition).filter(Boolean))].sort()
)

const filteredRecords = computed(() => {
  let rows = data.value?.records || []

  if (supplierFilter.value?.length)
    rows = rows.filter((r: any) => supplierFilter.value.includes(r.supplierName))

  if (conditionFilter.value?.length)
    rows = rows.filter((r: any) => conditionFilter.value.includes(r.condition))

  const q = (search.value || '').trim().toLowerCase()
  if (q) {
    rows = rows.filter((r: any) =>
      [
        r.supplierName, r.rfqName, r.certName,
        r.condition, r.alt, r.note, r.myNotes, r.enteredByName, r.rfqOwnerName,
        r.shippingPoint, ...(r.assignedUsers || []),
      ].some((f: any) => f && String(f).toLowerCase().includes(q))
    )
  }

  return rows
})

async function load() {
  if (!props.partNumberId) return
  loading.value = true
  error.value = ''
  data.value = null
  search.value = ''
  supplierFilter.value = []
  conditionFilter.value = []
  try {
    data.value = await api.get(`/procument-page/part-history?partNumberId=${props.partNumberId}`)
  } catch (e: any) {
    error.value = e?.data?.message || e?.data || 'Failed to load the part history.'
  } finally {
    loading.value = false
  }
}

watch(() => [props.modelValue, props.partNumberId], ([open]) => {
  if (open) load()
})
</script>

<style scoped>
.history-scroll {
  overflow: auto;
  max-height: 58vh;
}

.history-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.78rem;
  white-space: nowrap;
}

.history-grid th {
  position: sticky;
  top: 0;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
  text-align: left;
  font-weight: 600;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  opacity: 0.7;
  padding: 8px 10px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.25);
}

.history-grid td {
  padding: 6px 10px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.12);
  vertical-align: top;
}

.history-grid tbody tr:hover td {
  background: rgba(var(--v-theme-primary), 0.04);
}

.history-grid .sticky-col {
  position: sticky;
  left: 0;
  z-index: 3;
  background: rgb(var(--v-theme-surface));
  border-right: 1px solid rgba(var(--v-border-color), 0.25);
  min-width: 180px;
}

.history-grid thead .sticky-col {
  z-index: 4;
}

.history-grid tbody tr:hover .sticky-col {
  background: rgb(var(--v-theme-surface));
}

.history-grid .shop-row td {
  background: rgba(var(--v-theme-surface-variant), 0.25);
}

.history-grid .note-cell {
  white-space: normal;
  min-width: 200px;
  max-width: 280px;
}

.history-grid .nowrap {
  white-space: nowrap;
}

.rfq-link {
  color: rgb(var(--v-theme-primary));
  text-decoration: none;
}

.rfq-link:hover {
  text-decoration: underline;
}
</style>
