<template>
  <div>
    <PageHeader title="RFQs">
      <template #actions>
        <v-btn
          v-if="isAdmin"
          prepend-icon="mdi-shield-account"
          variant="tonal"
          @click="showBulkPerms = true"
          size="small"
        >
          Perms
        </v-btn>
        <v-btn v-if="isAmir" variant="tonal" color="secondary" prepend-icon="mdi-table-arrow-down" @click="openBulkImport" size="small">
          Bulk Import
        </v-btn>
        <v-btn v-if="isAdmin || addRFQ" color="primary" prepend-icon="mdi-plus" @click="openCreateModal" size="small">
          New RFQ
        </v-btn>
      </template>
    </PageHeader>

    <!-- RFQ List -->
    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search RFQs..."
            single-line
            hide-details
            class="flex-grow-1 mx-2"
            style="min-width: 180px;"

          />
          <v-select
            v-model="statusFilter"
            :items="rfqStatusOptions"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mr-2"
            style="min-width: 120px; max-width: 200px;"
          />
          <v-text-field
            v-model="pnSearch"
            label="Search by P/N"
            prepend-inner-icon="mdi-cog-outline"
            hide-details
            clearable
            density="compact"
            variant="outlined"
            class="mx-2"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-autocomplete
            v-model="userFilter"
            :items="userOptions"
            item-title="name"
            item-value="id"
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
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
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
        </div>
        <v-data-table
          :headers="headers"
          :items="filteredItems"
          :search="search"
          :loading="loading"
          :items-per-page="50"
          hover
          :row-props="getRowProps"
          @click:row="goToRfq"
        >
          <template #item.id="{ item }">
            <div class="d-flex align-center gap-1">
              <v-icon v-if="item.isUnread" icon="mdi-circle" size="10" color="blue" />
              {{ item.id }}
            </div>
          </template>
          <template #item.receivedDate="{ item }">
            {{ item.receivedDate && new Date(item.receivedDate).getFullYear() > 2000 ? new Date(item.receivedDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.leadTime="{ item }">
            <span :class="{ 'text-error font-weight-bold': (item.status == 'Open' || item.status == 'In Progress') && isLeadTimeExpired(item.leadTime) }">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="isLeadTimeUrgent(item.leadTime) && (item.status == 'Open' || item.status == 'In Progress')" icon="mdi-alert" size="16" color="warning" class="ml-1" title="Lead time expires within 3 days" />
              <v-icon v-else-if="isLeadTimeExpired(item.leadTime) && (item.status == 'Open' || item.status == 'In Progress')" icon="mdi-alert-circle" size="16" color="error" class="ml-1" title="Lead time has expired" />
            </span>
          </template>
          <!-- <template #item.priority="{ item }">
            <v-chip
              size="small"
              :color="item.priority === 'AOG' ? 'error' : item.priority === 'Urgent' ? 'warning' : 'secondary'"
              variant="tonal"
            >
              {{ item.priority || 'Normal' }}
            </v-chip>
          </template> -->
          <template #item.customerName="{ item }">
            <template v-if="isAdmin">{{ item.customerName }}<span v-if="item.customerCode" class="text-medium-emphasis ml-1">({{ item.customerCode }})</span></template>
            <template v-else>{{ item.customerCode || '—' }}</template>
          </template>
          <template #item.status="{ item }">
            <v-tooltip v-if="['No Quote', 'Waiting For Admin'].includes(item.status) && item.noQuoteReason" location="bottom">
              <template #activator="{ props: tp }">
                <v-chip
                  v-bind="tp"
                  size="small"
                  :color="rfqStatusColor(item.status)"
                  variant="tonal"
                >
                  {{ item.status }}
                  <v-icon icon="mdi-information-outline" size="14" class="ml-1" />
                </v-chip>
              </template>
              <span>{{ item.noQuoteReason }}</span>
            </v-tooltip>
            <v-chip
              v-else
              size="small"
              :color="rfqStatusColor(item.status || 'Open')"
              variant="tonal"
            >
              {{ item.status || 'Open' }}
            </v-chip>
          </template>
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip
                v-for="user in [...(item.views || []), ...(item.edits || [])].filter((u, i, arr) => arr.findIndex(x => x.id === u.id) === i)"
                :key="user.id"
                size="x-small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-account"
              >
                {{ user.name }}
              </v-chip>
              <span v-if="!(item.views?.length || item.edits?.length)" class="text-medium-emphasis">—</span>
            </div>
          </template>
          <template #item.itemCount="{ item }">
            <v-chip size="small" color="secondary">{{ item.items?.length || 0 }} parts</v-chip>
          </template>
          <!-- <template #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`/rfqs/${item.id}`" />
          </template> -->
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- ═══════════ Create RFQ Modal ═══════════ -->
    <v-dialog v-model="showCreate" max-width="1200" persistent scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-file-document-plus-outline" color="primary" class="mr-2" />
          Create New RFQ
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showCreate = false" />
        </v-card-title>

        <v-divider />

        <v-card-text class="pa-4" style="max-height: 70vh; overflow-y: auto;">
          <v-form ref="formRef">
            <!-- RFQ Name -->
            <v-text-field
              v-model="form.name"
              label="RFQ Name *"
              prepend-inner-icon="mdi-file-document-outline"
              :rules="[rules.required]"
              class="mb-3"
            />

            <!-- Customer Name (autocomplete) -->
            <v-combobox
              v-model="form.customerName"
              :items="customerSuggestions"
              item-title="name"
              item-value="name"
              label="Customer Name *"
              prepend-inner-icon="mdi-domain"
              :rules="[rules.required]"
              :loading="customerLoading"
              no-filter
              clearable
              return-object
              class="mb-3"
              @update:search="searchCustomers"
            >
              <template #no-data>
                <v-list-item>
                  <v-list-item-title v-if="customerSearchText.length < 3" class="text-medium-emphasis">
                    Type at least 3 characters...
                  </v-list-item-title>
                  <v-list-item-title v-else class="text-medium-emphasis">
                    "{{ customerSearchText }}" — will be created automatically
                  </v-list-item-title>
                </v-list-item>
              </template>
            </v-combobox>
            <!-- Received Date -->
            <v-text-field
              v-model="form.date"
              label="Received Date *"
              prepend-inner-icon="mdi-calendar"
              type="date"
              :max="today"
              :rules="[rules.required]"
              class="mb-3"
            />
            <!-- Deadline -->
            <v-text-field
              v-model="form.leadTime"
              label="Deadline *"
              prepend-inner-icon="mdi-calendar-clock"
              type="date"
              :min="today"
              :rules="[rules.required]"
              class="mb-3"
            />

            <!-- Priority -->
            

            <!-- Ex Type -->
            <v-select
              v-model="form.exType"
              :items="exTypeOptions"
              item-title="label"
              item-value="value"
              label="Ex Type"
              prepend-inner-icon="mdi-swap-horizontal"
              clearable
              class="mb-3"
            />

            <!-- Notes -->
            <v-textarea
              v-model="form.notes"
              label="Notes"
              prepend-inner-icon="mdi-note-text-outline"
              rows="2"
              auto-grow
              class="mb-3"
            />

            <!-- ── Part Numbers Section ── -->
            <v-divider class="mb-4" />

            <div class="d-flex flex-wrap align-center gap-2 mb-3">
              <div class="d-flex align-center">
                <v-icon icon="mdi-cog-outline" color="secondary" class="mr-2" size="20" />
                <span class="text-subtitle-1 font-weight-medium">RFQ Items</span>
              </div>
              <v-spacer />
              <div class="d-flex align-center gap-2">
                <v-btn
                  prepend-icon="mdi-content-paste"
                  variant="tonal"
                  size="small"
                  color="info"
                  @click="showPasteBox = !showPasteBox"
                >
                  {{ showPasteBox ? 'Hide Paste' : 'Paste Excel' }}
                </v-btn>
                <v-text-field
                  v-model.number="partCount"
                  type="number"
                  label="# of fields"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 80px"
                  min="1"
                  max="100"
                  @update:model-value="adjustPartFields"
                />
                <v-btn
                  icon="mdi-plus"
                  color="primary"
                  size="small"
                  variant="outlined"
                  @click="addPartRow"
                />
              </div>
            </div>

            <!-- Paste from Excel -->
            <div v-if="showPasteBox" class="mb-4">
              <p class="text-caption text-medium-emphasis mb-2">
                Copy rows from Excel and paste below. Expected columns (tab-separated):
                <strong>PartNumber &nbsp; Description &nbsp; Qty &nbsp; Condition &nbsp; Priority &nbsp; Remark &nbsp; Alt</strong>
              </p>
              <v-textarea
                v-model="pasteText"
                placeholder="Paste rows from Excel here (Ctrl+V)..."
                variant="outlined"
                density="compact"
                rows="5"
                hide-details
                class="mb-2"
                style="font-family: 'JetBrains Mono', 'Cascadia Code', monospace; font-size: 12px;"
              />
              <div class="d-flex align-center gap-2">
                <v-btn
                  prepend-icon="mdi-import"
                  color="primary"
                  size="small"
                  :loading="pasteImporting"
                  @click="importFromPaste"
                >
                  Import &amp; Check
                </v-btn>
                <v-btn
                  variant="text"
                  size="small"
                  @click="pasteText = ''; showPasteBox = false"
                >
                  Cancel
                </v-btn>
                <span v-if="pasteStatus" class="text-caption" :class="pasteStatusColor">{{ pasteStatus }}</span>
              </div>
            </div>

            <p class="text-caption text-medium-emphasis mb-3">
              Search part numbers (3+ chars). If found, description &amp; alternatives auto-fill. Otherwise a new part will be created.
            </p>

            <div
              v-for="(row, index) in form.items"
              :key="index"
              class="d-flex flex-wrap align-center gap-2 mb-3 rfq-item-row"
            >
              <span class="text-caption text-medium-emphasis" style="min-width: 24px">{{ index + 1 }}.</span>

              <!-- Part Number -->
              <v-combobox
                v-model="row.partNumber"
                :items="partSuggestions[index] || []"
                item-title="name"
                item-value="name"
                label="Part Number *"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                style="min-width: 160px; flex: 2;"
                :loading="partLoading[index]"
                @update:search="(val: string) => searchParts(val, index)"
                @update:model-value="(val: any) => onPartPicked(val, index)"
              >
                <template #item="{ item: suggestion, props: itemProps }">
                  <v-list-item v-bind="itemProps">
                    <template #subtitle>
                      <span v-if="suggestion.raw.description">{{ suggestion.raw.description }}</span>
                      <span v-else class="font-italic text-medium-emphasis">No description</span>
                    </template>
                  </v-list-item>
                </template>
                <template #no-data>
                  <v-list-item>
                    <v-list-item-title
                      v-if="(partSearchTexts[index]?.length || 0) < 3"
                      class="text-medium-emphasis text-caption"
                    >
                      Type 3+ chars to search...
                    </v-list-item-title>
                    <v-list-item-title v-else class="text-medium-emphasis text-caption">
                      "{{ partSearchTexts[index] }}" — will be created
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-combobox>

              <!-- Description -->
              <v-text-field
                v-model="row.description"
                label="Description"
                density="compact"
                variant="outlined"
                hide-details
                style="min-width: 140px; flex: 2;"
              />

              <!-- Qty -->
              <v-text-field
                v-model.number="row.qty"
                label="Qty"
                type="number"
                min="1"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 70px;"
              />

              <!-- Condition -->
              <v-select
                v-model="row.condition"
                :items="['NE', 'OH', 'SV', 'AR']"
                label="Cond."
                density="compact"
                variant="outlined"
                hide-details
                clearable
                style="max-width: 140px;"
              />

              <v-select
              v-model="row.priority"
              :items="['Normal', 'Urgent', 'AOG']"
              label="Priority"
              density="compact"
              variant="outlined"
              hide-details

              clearable
            />

              <!-- Remark -->
              <v-text-field
                v-model="row.remark"
                label="Remark"
                density="compact"
                variant="outlined"
                hide-details
                style="min-width: 100px; flex: 1;"
              />

              <!-- Alternatives -->
              <div class="d-flex flex-wrap align-center gap-1" style="min-width: 160px; flex: 2;">
                <v-chip
                  v-for="(alt, aIdx) in row.alternatives"
                  :key="aIdx"
                  size="x-small"
                  color="warning"
                  variant="tonal"
                  closable
                  @click:close="row.alternatives.splice(aIdx, 1)"
                >
                  {{ alt }}
                </v-chip>
                <v-text-field
                  v-model="row.altInput"
                  placeholder="+ alt"
                  density="compact"
                  variant="plain"
                  hide-details
                  single-line
                  style="max-width: 80px; font-size: 11px;"
                  @keydown.enter.prevent="pushAlt(index)"
                />
              </div>

              <!-- Status chip -->
              <v-chip
                v-if="row.isExisting"
                size="x-small" color="success" variant="tonal"
              >DB</v-chip>
              <v-chip
                v-else-if="row.partNumber"
                size="x-small" color="info" variant="tonal"
              >New</v-chip>
              <span v-else style="width: 32px;"></span>

              <!-- Remove -->
              <v-btn
                v-if="form.items.length > 1"
                icon="mdi-close"
                size="x-small"
                variant="text"
                color="error"
                @click="removePartRow(index)"
              />
            </div>
          </v-form>

          <!-- Validation error -->
          <v-alert v-if="submitError" type="error" density="compact" class="mt-3">
            {{ submitError }}
          </v-alert>
        </v-card-text>

        <v-divider />

        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showCreate = false">Cancel</v-btn>
          <v-btn color="primary" :loading="submitting" @click="submitRfq">
            Create RFQ
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ═══════════ Bulk Import RFQs Modal ═══════════ -->
    <v-dialog v-model="showBulkImport" max-width="1100" persistent scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-table-arrow-down" color="secondary" class="mr-2" />
          Bulk Import RFQs
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showBulkImport = false" />
        </v-card-title>
        <v-divider />

        <v-card-text class="pa-4" style="max-height:78vh; overflow-y:auto;">
          <!-- Customer only (dates come from paste) -->
          <v-row dense class="mb-3">
            <v-col cols="12" md="5">
              <v-combobox
                v-model="bulkDefaults.customerName"
                :items="bulkCustomerSuggestions"
                item-title="name"
                item-value="name"
                label="Customer Name *"
                density="compact"
                variant="outlined"
                hide-details
                prepend-inner-icon="mdi-domain"
                no-filter
                clearable
                return-object
                :loading="bulkCustomerLoading"
                @update:search="searchBulkCustomers"
              />
            </v-col>
            <v-col cols="12" md="7">
              <v-alert type="info" density="compact" variant="tonal" class="text-caption">
                Received Date &amp; Deadline are read from the paste data (columns 9 &amp; 10).
              </v-alert>
            </v-col>
          </v-row>

          <!-- Paste area -->
          <p class="text-caption text-medium-emphasis mb-1">
            Paste rows from Excel (tab-separated). Columns:
            <strong>RFQ Name &nbsp;|&nbsp; # &nbsp;|&nbsp; Part Number &nbsp;|&nbsp; Description &nbsp;|&nbsp; Qty &nbsp;|&nbsp; Condition &nbsp;|&nbsp; Priority &nbsp;|&nbsp; Alt &nbsp;|&nbsp; Received Date &nbsp;|&nbsp; Deadline</strong>
            — Alt values separated by comma (,). Rows with the same RFQ Name are grouped into one RFQ.
          </p>
          <v-textarea
            v-model="bulkPasteText"
            placeholder="Paste your Excel rows here (Ctrl+V)..."
            variant="outlined"
            density="compact"
            rows="6"
            hide-details
            class="mb-3"
            style="font-family: 'JetBrains Mono', 'Cascadia Code', monospace; font-size:12px;"
          />
          <div class="d-flex align-center gap-2 mb-4">
            <v-btn prepend-icon="mdi-table-search" color="primary" size="small" @click="parseBulkRFQs">
              Parse &amp; Preview
            </v-btn>
            <v-btn variant="text" size="small" @click="bulkPasteText = ''; bulkGroups = []">Clear</v-btn>
            <span v-if="bulkParseMsg" class="text-caption" :class="bulkParseMsgColor">{{ bulkParseMsg }}</span>
          </div>

          <!-- Preview -->
          <div v-if="bulkGroups.length">
            <div class="text-subtitle-2 mb-2">
              Preview — {{ bulkGroups.filter(g => !g.isDuplicate).length }} RFQ(s) will be created,
              <span class="text-error">{{ bulkGroups.filter(g => g.isDuplicate).length }} skipped (duplicate name)</span>
            </div>

            <v-expansion-panels variant="accordion" multiple>
              <v-expansion-panel
                v-for="(group, gi) in bulkGroups"
                :key="gi"
              >
                <v-expansion-panel-title>
                  <div class="d-flex align-center gap-2 w-100 flex-wrap">
                    <v-icon
                      :icon="group.isDuplicate ? 'mdi-alert-circle' : 'mdi-check-circle'"
                      :color="group.isDuplicate ? 'error' : 'success'"
                      size="18"
                    />
                    <span class="font-weight-medium">{{ group.rfqName }}</span>
                    <v-chip size="x-small" color="secondary" variant="tonal">{{ group.items.length }} parts</v-chip>
                    <span v-if="group.receivedDate" class="text-caption text-medium-emphasis">
                      {{ group.receivedDate }} → {{ group.deadline }}
                    </span>
                    <v-chip v-if="group.isDuplicate" size="x-small" color="error" variant="tonal">DUPLICATE — will be skipped</v-chip>
                  </div>
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-table density="compact" class="text-caption">
                    <thead>
                      <tr>
                        <th>#</th>
                        <th>Part Number</th>
                        <th>Description</th>
                        <th>Qty</th>
                        <th>Condition</th>
                        <th>Priority</th>
                        <th>Remark</th>
                        <th>Alternatives</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(row, ri) in group.items" :key="ri">
                        <td>{{ ri + 1 }}</td>
                        <td class="font-weight-medium">{{ row.partNumber }}</td>
                        <td>{{ row.description || '—' }}</td>
                        <td>{{ row.qty }}</td>
                        <td>{{ row.condition || '—' }}</td>
                        <td>{{ row.priority || '—' }}</td>
                        <td>{{ row.remark || '—' }}</td>
                        <td>
                          <div class="d-flex flex-wrap gap-1">
                            <v-chip v-for="alt in row.alternatives" :key="alt" size="x-small" color="warning" variant="tonal">{{ alt }}</v-chip>
                            <span v-if="!row.alternatives.length" class="text-medium-emphasis">—</span>
                          </div>
                        </td>
                      </tr>
                    </tbody>
                  </v-table>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </div>

          <v-alert v-if="bulkSubmitError" type="error" density="compact" class="mt-3">{{ bulkSubmitError }}</v-alert>
        </v-card-text>

        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showBulkImport = false">Cancel</v-btn>
          <v-btn
            color="primary"
            :loading="bulkImporting"
            :disabled="!bulkGroups.filter(g => !g.isDuplicate).length"
            prepend-icon="mdi-cloud-upload"
            @click="submitBulkRFQs"
          >
            Import {{ bulkGroups.filter(g => !g.isDuplicate).length }} RFQ(s)
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Bulk Permission Manager -->
    <BulkPermissionManager v-model="showBulkPerms" entity-name="RFQ" @update:model-value="(v) => !v && loadItems()" />
  </div>
</template>

<script setup lang="ts">
import { VTextField } from 'vuetify/components'

const api = useApi()
const authStore = useAuthStore()
const router = useRouter()
const route = useRoute()

const today = new Date().toISOString().split('T')[0]
const { statusColor: rfqStatusColor } = useStatusColor()
const rfqStatusOptions = ['Open', 'In Progress', 'Waiting For Admin', 'Ready To Quote', 'Sent', 'Accepted', 'Rejected', 'No Quote']
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('rfqs', {
  search: '',
  status: [] as string[],
  user: [] as number[],
  customer: [] as string[],
  partNumber: [] as string[],
  pnSearch: '',
})
// If URL has ?status=X, apply it once on load
if (route.query.status && pf.status.value.length === 0) {
  pf.status.value = [route.query.status as string]
}

function isLeadTimeUrgent(dateStr: string) {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  const daysLeft = diff / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 3
}

function isLeadTimeExpired(dateStr: string) {
  if (!dateStr) return false
  return new Date(dateStr).getTime() < Date.now()
}

function getRowProps({ item }: { item: any }) {
  const classes: string[] = []
  if (item.isUnread) classes.push('unread-rfq-row')
  if (isLeadTimeExpired(item.leadTime) && (item.status == 'Open' || item.status == 'In Progress')) classes.push('lead-time-expired-row')
  return classes.length ? { class: classes.join(' ') } : {}
}

const isAdmin = computed(() => authStore.isAdmin)
const addRFQ = computed(() => authStore.newRFQ)
const isAmir = computed(() => authStore.isAmir)
const showBulkPerms = ref(false)

// ── List state ──
const search = pf.search
const loading = ref(false)
const items = ref<any[]>([])
const statusFilter = pf.status
const userFilter = pf.user
const customerFilter = pf.customer
const partNumberFilter = pf.partNumber
const pnSearch = pf.pnSearch
const dateFrom = ref<string | null>(null)
const dateTo = ref<string | null>(null)

const userOptions = computed(() => {
  const map = new Map<number, string>()
  
  // Filter items used for options by selected status if any
  let sourceItems = items.value
  if (statusFilter.value?.length) {
    sourceItems = sourceItems.filter((item: any) => statusFilter.value.includes(item.status || 'Open'))
  }

  sourceItems.forEach((item: any) => {
    ;[...(item.views || []), ...(item.edits || [])].forEach((u: any) => {
      if (u.id && u.name) map.set(u.id, u.name)
    })
  })
  return Array.from(map, ([id, name]) => ({ id, name }))
})

const customerOptions = computed(() => {
  const set = new Set<string>()
  
  // Filter items used for options by selected status if any
  let sourceItems = items.value
  if (statusFilter.value?.length) {
    sourceItems = sourceItems.filter((item: any) => statusFilter.value.includes(item.status || 'Open'))
  }

  sourceItems.forEach((item: any) => { if (item.customerName) set.add(item.customerName) })
  return Array.from(set).sort()
})

const partNumberOptions = computed(() => {
  const set = new Set<string>()
  items.value.forEach((item: any) => {
    ;(item.items || []).forEach((ri: any) => {
      if (ri.partNumberName) set.add(ri.partNumberName)
    })
  })
  return Array.from(set).sort()
})

const filteredItems = computed(() => {
  let result = items.value
  if (statusFilter.value?.length) {
    result = result.filter((item: any) => statusFilter.value.includes(item.status || 'Open'))
  }
  if (userFilter.value?.length) {
    result = result.filter((item: any) => {
      const allUsers = [...(item.views || []), ...(item.edits || [])]
      return allUsers.some((u: any) => userFilter.value.includes(u.id))
    })
  }
  if (customerFilter.value?.length) {
    result = result.filter((item: any) => customerFilter.value.includes(item.customerName))
  }
  if (partNumberFilter.value?.length) {
    result = result.filter((item: any) =>
      (item.items || []).some((ri: any) => partNumberFilter.value.includes(ri.partNumberName))
    )
  }
  if (pnSearch.value?.trim()) {
    const q = pnSearch.value.trim().toLowerCase()
    result = result.filter((item: any) =>
      (item.partNumbers || '').toLowerCase().includes(q) ||
      (item.altPartNumbers || '').toLowerCase().includes(q)
    )
  }
  if (dateFrom.value) {
    const from = new Date(dateFrom.value).getTime()
    result = result.filter((item: any) => new Date(item.receivedDate).getTime() >= from)
  }
  if (dateTo.value) {
    const to = new Date(dateTo.value).getTime() + 86400000
    result = result.filter((item: any) => new Date(item.receivedDate).getTime() < to)
  }
  return result
})

const headers = [
  { title: 'ID', key: 'id', width: '80px' },
  { title: 'Name', key: 'name' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Status', key: 'status', width: '110px' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  // { title: 'Priority', key: 'priority', width: '100px' },
  { title: 'Deadline', key: 'leadTime' },
  { title: 'Parts', key: 'itemCount', sortable: false },
  { title: 'Received Date', key: 'receivedDate' },
]

onMounted(() => loadItems())

async function loadItems() {
  loading.value = true
  try {
    const res = await api.get<any[]>('/rfqs')
    items.value = (res || []).map((rfq: any) => ({
      ...rfq,
      partNumbers: (rfq.items || []).map((ri: any) => ri.partNumberName).filter(Boolean).join(', '),
      altPartNumbers: (rfq.items || []).flatMap((ri: any) => (ri.alternatives || []).map((a: any) => a.name)).filter(Boolean).join(', '),
    }))
  } catch {}
  finally { loading.value = false }
}

function goToRfq(pointerEvent: Event, rowData: { item: any }) {
  if (rowData && rowData.item && rowData.item.id) {
    navigateTo(`/rfqs/${rowData.item.id}`)
  }
}

// ── Create Modal state ──
const showCreate = ref(false)
const formRef = ref<any>(null)
const submitting = ref(false)
const submitError = ref('')
const partCount = ref(10)

interface ItemRow {
  partNumber: any
  description: string
  qty: number
  condition: string
  remark: string
  priority: string
  alternatives: string[]
  altInput: string
  isExisting: boolean
}

function makeEmptyRow(): ItemRow {
  return { partNumber: null, description: '', qty: 1, condition: '', remark: '',priority: '' as string, alternatives: [], altInput: '', isExisting: false }
}

const exTypeOptions = [
  { label: 'Ex Warehouse', value: 0 },
  { label: 'Ex Vendor', value: 1 },
  { label: 'Ex Customer', value: 2 },
]

const form = ref({
  name: '',
  customerName: null as any,
  leadTime: '',
  date: '',
  notes: '',
  exType: null as number | null,
  items: Array.from({ length: 10 }, makeEmptyRow) as ItemRow[],
})

const rules = {
  required: (v: any) => !!v || 'This field is required',
}

// ── Customer Autocomplete ──
const customerSuggestions = ref<any[]>([])
const customerLoading = ref(false)
const customerSearchText = ref('')
let customerDebounce: any = null

function searchCustomers(val: string) {
  customerSearchText.value = val || ''
  clearTimeout(customerDebounce)
  if (!val || val.length < 1) {
    customerSuggestions.value = []
    return
  }
  customerDebounce = setTimeout(async () => {
    customerLoading.value = true
    try {
      customerSuggestions.value = await api.get<any[]>(`/customers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { customerLoading.value = false }
  }, 300)
}

// ── Part Number Autocomplete ──
const partSuggestions = ref<Record<number, any[]>>({})
const partLoading = ref<Record<number, boolean>>({})
const partSearchTexts = ref<Record<number, string>>({})
const partDebounces: Record<number, any> = {}

function searchParts(val: string, index: number) {
  partSearchTexts.value[index] = val || ''
  clearTimeout(partDebounces[index])
  if (!val || val.length < 1) {
    partSuggestions.value[index] = []
    return
  }
  partDebounces[index] = setTimeout(async () => {
    partLoading.value[index] = true
    try {
      partSuggestions.value[index] = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { partLoading.value[index] = false }
  }, 300)
}

function onPartPicked(val: any, index: number) {
  const row = form.value.items[index]
  if (!row) return
  if (!val) {
    row.isExisting = false
    row.description = ''
    row.remark = ''
    row.alternatives = []
    return
  }
  if (typeof val === 'object' && val.id) {
    row.isExisting = true
    row.description = val.description || ''
    row.remark = val.remark || ''
    row.alternatives = (val.alternatives || []).map((a: any) => a.name)
  } else {
    row.isExisting = false
  }
}

function pushAlt(index: number) {
  const row = form.value.items[index]
  if (!row) return
  const val = row.altInput.trim()
  if (!val) return
  if (!row.alternatives.includes(val)) {
    row.alternatives.push(val)
  }
  row.altInput = ''
}

// ── Part row management ──
function adjustPartFields(val: string | number) {
  val = Number(val)
  const count = Math.max(1, Math.min(100, val || 1))
  partCount.value = count
  const current = form.value.items.length
  if (count > current) {
    for (let i = 0; i < count - current; i++) form.value.items.push(makeEmptyRow())
  } else if (count < current) {
    form.value.items.splice(count)
  }
}

function addPartRow() {
  form.value.items.push(makeEmptyRow())
  partCount.value = form.value.items.length
}

function removePartRow(index: number) {
  form.value.items.splice(index, 1)
  partCount.value = form.value.items.length
}

// ── Paste from Excel ──
const showPasteBox = ref(false)
const pasteText = ref('')
const pasteImporting = ref(false)
const pasteStatus = ref('')
const pasteStatusColor = ref('text-medium-emphasis')

async function importFromPaste() {
  const text = pasteText.value.trim()
  if (!text) {
    pasteStatus.value = 'Nothing to import — paste some rows first.'
    pasteStatusColor.value = 'text-warning'
    return
  }

  pasteImporting.value = true
  pasteStatus.value = 'Parsing...'
  pasteStatusColor.value = 'text-medium-emphasis'

  try {
    const lines = text.split(/\r?\n/).filter(l => l.trim())
    if (lines.length === 0) {
      pasteStatus.value = 'No valid rows found.'
      pasteStatusColor.value = 'text-error'
      return
    }

    // Detect header row
    const firstLine = (lines.length > 0 ? (lines[0] || '') : '').toLowerCase()
    const hasHeader = firstLine.includes('partnumber') || firstLine.includes('part number') || firstLine.includes('part_number') || firstLine.includes('p/n')
    const startIdx = hasHeader ? 1 : 0

    const rows: ItemRow[] = []
    for (let i = startIdx; i < lines.length; i++) {
      const line = lines[i]
      if (!line) continue
      // Excel copies as tab-separated
      const cols = line.split('\t')
      if (cols.length === 0 || !(cols[0] || '').trim()) continue

      const partNumber = (cols[0] || '').trim()
      const description = (cols[1] || '').trim()
      const qty = parseInt((cols[2] || '').trim()) || 1
      const condition = (cols[3] || '').trim().toUpperCase()
      const priority = (cols[4] || '').trim()
      const remark = (cols[5] || '').trim()
      const alt = (cols[6] || '').trim()

      const alternatives = alt ? alt.split(';').map(a => a.trim()).filter(Boolean) : []

      rows.push({
        partNumber,
        description,
        qty,
        condition,
        remark,
        priority,
        alternatives,
        altInput: '',
        isExisting: false,
      })
    }

    if (rows.length === 0) {
      pasteStatus.value = 'No valid rows found.'
      pasteStatusColor.value = 'text-error'
      return
    }

    // Search each part number in the DB and enrich
    pasteStatus.value = `Checking ${rows.length} part(s) against database...`
    let foundCount = 0

    for (const row of rows) {
      const pn = typeof row.partNumber === 'string' ? row.partNumber : ''
      if (!pn || pn.length < 2) continue

      try {
        const results = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(pn)}`)
        // Find exact match (case-insensitive)
        const match = (results || []).find((r: any) => r.name?.toLowerCase() === pn.toLowerCase())

        if (match) {
          foundCount++
          row.partNumber = { id: match.id, name: match.name, description: match.description, remark: match.remark, alternatives: match.alternatives }
          row.isExisting = true

          // Import description from DB if the pasted description is empty
          if (!row.description && match.description) {
            row.description = match.description
          }


          // Import remark from DB if the pasted remark is empty
          if (!row.remark && match.remark) {
            row.remark = match.remark
          }

          // Merge alternatives: keep pasted ones + add DB ones that aren't already listed
          const existingAlts = (match.alternatives || []).map((a: any) => a.name)
          for (const dbAlt of existingAlts) {
            if (!row.alternatives.some(a => a.toLowerCase() === dbAlt.toLowerCase())) {
              row.alternatives.push(dbAlt)
            }
          }
        }
      } catch {
        // Search failed for this part, leave as new
      }
    }

    form.value.items = rows
    partCount.value = rows.length
    submitError.value = ''

    pasteStatus.value = `Imported ${rows.length} row(s) — ${foundCount} found in DB, ${rows.length - foundCount} new.`
    pasteStatusColor.value = 'text-success'

    // Auto-hide paste box after successful import
    setTimeout(() => { showPasteBox.value = false }, 2000)
  } catch (e) {
    pasteStatus.value = 'Import failed.'
    pasteStatusColor.value = 'text-error'
  } finally {
    pasteImporting.value = false
  }
}

// ── Bulk Import RFQs ──
const showBulkImport = ref(false)
const bulkPasteText = ref('')

interface BulkRFQItem { partNumber: string; description: string; qty: number; condition: string; priority: string; remark: string; alternatives: string[] }
interface BulkRFQGroup { rfqName: string; isDuplicate: boolean; receivedDate: string; deadline: string; items: BulkRFQItem[] }

const bulkGroups = ref<BulkRFQGroup[]>([])
const bulkImporting = ref(false)
const bulkParseMsg = ref('')
const bulkParseMsgColor = ref('text-medium-emphasis')
const bulkSubmitError = ref('')

const bulkDefaults = ref({ customerName: null as any })
const bulkCustomerSuggestions = ref<any[]>([])
const bulkCustomerLoading = ref(false)
let bulkCustomerDebounce: any = null

function searchBulkCustomers(val: string) {
  clearTimeout(bulkCustomerDebounce)
  if (!val || val.length < 1) { bulkCustomerSuggestions.value = []; return }
  bulkCustomerDebounce = setTimeout(async () => {
    bulkCustomerLoading.value = true
    try { bulkCustomerSuggestions.value = await api.get<any[]>(`/customers/search?q=${encodeURIComponent(val)}`) }
    catch {} finally { bulkCustomerLoading.value = false }
  }, 300)
}

function openBulkImport() {
  bulkPasteText.value = ''
  bulkGroups.value = []
  bulkParseMsg.value = ''
  bulkSubmitError.value = ''
  bulkDefaults.value = { customerName: null }
  showBulkImport.value = true
}

function parseDateToISO(raw: string): string {
  // Handles M/D/YYYY or MM/DD/YYYY
  if (!raw) return ''
  const parts = raw.split('/')
  if (parts.length === 3) {
    const [m, d, y] = parts
    return `${y.trim()}-${m.trim().padStart(2, '0')}-${d.trim().padStart(2, '0')}`
  }
  return raw
}

function parseBulkRFQs() {
  bulkParseMsg.value = ''
  bulkGroups.value = []
  const text = bulkPasteText.value.trim()
  if (!text) {
    bulkParseMsg.value = 'Nothing to parse — paste some rows first.'
    bulkParseMsgColor.value = 'text-warning'
    return
  }

  const lines = text.split(/\r?\n/).filter(l => l.trim())
  // Detect header row
  const first = (lines[0] || '').toLowerCase()
  const hasHeader = first.includes('rfq') || first.includes('part') || first.includes('p/n')
  const startIdx = hasHeader ? 1 : 0

  // Column layout:
  // 0: RFQ Name | 1: # (row order, skip) | 2: Part Number | 3: Description
  // 4: Qty | 5: Condition | 6: Priority | 7: Alt | 8: Received Date | 9: Deadline
  interface GroupMeta { items: BulkRFQItem[]; receivedDate: string; deadline: string }
  const groupMap = new Map<string, GroupMeta>()

  for (let i = startIdx; i < lines.length; i++) {
    const cols = lines[i].split('\t')
    const rfqName = (cols[0] || '').trim()
    const partNumber = (cols[2] || '').trim()
    if (!rfqName || !partNumber) continue

    const description = (cols[3] || '').trim()
    const qty = Math.max(1, parseInt((cols[4] || '').trim()) || 1)
    const condition = (cols[5] || '').trim().toUpperCase()
    const priority = (cols[6] || '').trim()
    const altRaw = (cols[7] || '').trim()
    const alternatives = altRaw ? altRaw.split(',').map(a => a.trim()).filter(Boolean) : []
    const receivedDate = parseDateToISO((cols[8] || '').trim())
    const deadline = parseDateToISO((cols[9] || '').trim())

    if (!groupMap.has(rfqName)) {
      groupMap.set(rfqName, { items: [], receivedDate, deadline })
    }
    groupMap.get(rfqName)!.items.push({ partNumber, description, qty, condition, priority, remark: '', alternatives })
    // Always use last-seen dates (they're the same per RFQ)
    if (receivedDate) groupMap.get(rfqName)!.receivedDate = receivedDate
    if (deadline) groupMap.get(rfqName)!.deadline = deadline
  }

  if (groupMap.size === 0) {
    bulkParseMsg.value = 'No valid rows found. Expected: RFQ Name [tab] # [tab] Part Number ...'
    bulkParseMsgColor.value = 'text-error'
    return
  }

  const existingNames = new Set(items.value.map((r: any) => r.name?.toLowerCase()))
  bulkGroups.value = Array.from(groupMap.entries()).map(([rfqName, meta]) => ({
    rfqName,
    items: meta.items,
    receivedDate: meta.receivedDate,
    deadline: meta.deadline,
    isDuplicate: existingNames.has(rfqName.toLowerCase()),
  }))

  const newCount = bulkGroups.value.filter(g => !g.isDuplicate).length
  const dupCount = bulkGroups.value.filter(g => g.isDuplicate).length
  bulkParseMsg.value = `Found ${bulkGroups.value.length} RFQ(s) — ${newCount} new, ${dupCount} duplicate(s).`
  bulkParseMsgColor.value = newCount > 0 ? 'text-success' : 'text-warning'
}

async function submitBulkRFQs() {
  bulkSubmitError.value = ''
  const customerName = typeof bulkDefaults.value.customerName === 'object'
    ? bulkDefaults.value.customerName?.name
    : bulkDefaults.value.customerName

  if (!customerName?.trim()) { bulkSubmitError.value = 'Customer Name is required.'; return }

  const toCreate = bulkGroups.value.filter(g => !g.isDuplicate)
  if (!toCreate.length) return

  bulkImporting.value = true
  try {
    for (const group of toCreate) {
      const partNumbers = group.items.map(it => it.partNumber)
      const leadTimeISO = group.deadline ? new Date(group.deadline).toISOString() : new Date().toISOString()
      const receivedDateISO = group.receivedDate ? new Date(group.receivedDate).toISOString() : new Date().toISOString()
      const created = await api.post<any>('/rfqs', {
        name: group.rfqName,
        customerName: customerName.trim(),
        leadTime: leadTimeISO,
        receivedDate: receivedDateISO,
        userId: authStore.user?.id || 0,
        notes: null,
        exType: null,
        partNumbers,
      })

      // Update items: qty, condition, priority + description/remark on partnumber + alternatives
      if (created?.items?.length) {
        const updatePromises = created.items.map((serverItem: any, idx: number) => {
          const local = group.items[idx]
          if (!local) return Promise.resolve()
          const promises: Promise<any>[] = []

          promises.push(api.put(`/rfqs/items/${serverItem.id}`, {
            alt: null,
            qty: local.qty,
            condition: local.condition || null,
            priority: local.priority || null,
            note: local.remark || null,
          }))

          if (local.description || local.remark) {
            promises.push(api.put(`/partnumbers/${serverItem.partNumberId}`, {
              name: serverItem.partNumberName,
              description: local.description || null,
              remark: local.remark || null,
              supplierId: null,
            }))
          }

          for (const altName of local.alternatives) {
            const existing = (serverItem.alternatives || []).find((a: any) => a.name === altName)
            if (!existing) {
              promises.push(api.post(`/partnumbers/${serverItem.partNumberId}/alternatives`, { name: altName }))
            }
          }

          return Promise.all(promises)
        })
        await Promise.all(updatePromises)
      }
    }

    showBulkImport.value = false
    await loadItems()
  } catch (e: any) {
    bulkSubmitError.value = e?.data?.message || 'Bulk import failed.'
  } finally {
    bulkImporting.value = false
  }
}

function openCreateModal() {
  form.value = {
    name: '',
    customerName: null,
    leadTime: '',
    date: '',
    notes: '',
    exType: null,
    items: Array.from({ length: 10 }, makeEmptyRow),
  }
  partCount.value = 10
  submitError.value = ''
  partSuggestions.value = {}
  partSearchTexts.value = {}
  showCreate.value = true
}

// ── Submit ──
async function submitRfq() {
  submitError.value = ''

  if (!form.value.name.trim()) {
    submitError.value = 'RFQ Name is required.'
    return
  }

  const customerName = typeof form.value.customerName === 'object'
    ? form.value.customerName?.name
    : form.value.customerName

  if (!customerName?.trim()) {
    submitError.value = 'Customer Name is required.'
    return
  }

  if (!form.value.leadTime) {
    submitError.value = 'Deadline is required.'
    return
  }

  // Collect non-empty items
  const validItems = form.value.items.filter(row => {
    const name = typeof row.partNumber === 'object' ? row.partNumber?.name : row.partNumber
    return name && name.trim()
  })

  if (validItems.length === 0) {
    submitError.value = 'At least 1 Part Number is required.'
    return
  }

  const partNumbers = validItems.map(row => {
    const name = typeof row.partNumber === 'object' ? row.partNumber.name : row.partNumber
    return name.trim()
  })

  submitting.value = true
  try {
    // 1. Create the RFQ with part numbers
    const payload = {
      name: form.value.name.trim(),
      customerName: customerName.trim(),
      leadTime: new Date(form.value.leadTime).toISOString(),
      receivedDate: form.value.date ? new Date(form.value.date).toISOString() : new Date().toISOString(),
      userId: authStore.user?.id || 0,
      notes: form.value.notes || null,
      exType: form.value.exType,
      partNumbers,
    }

    const created = await api.post<any>('/rfqs', payload)

    // 2. Update each item with qty, condition, alternatives
    if (created?.items?.length) {
      const updatePromises = created.items.map((serverItem: any, idx: number) => {
        const localRow = validItems[idx]
        if (!localRow) return Promise.resolve()

        const promises: Promise<any>[] = []

        // Update item fields (qty, condition)
        promises.push(api.put(`/rfqs/items/${serverItem.id}`, {
          alt: null,
          qty: localRow.qty || 1,
          condition: localRow.condition || null,
        }))

        // Update description, remark on part number if any provided
        if (localRow.description ||  localRow.remark) {
          promises.push(api.put(`/partnumbers/${serverItem.partNumberId}`, {
            name: serverItem.partNumberName,
            description: localRow.description || null,
            remark: localRow.remark || null,
            supplierId: null,
          }))
        }

        // Add alternatives
        for (const altName of localRow.alternatives) {
          const existing = (serverItem.alternatives || []).find((a: any) => a.name === altName)
          if (!existing) {
            promises.push(api.post(`/partnumbers/${serverItem.partNumberId}/alternatives`, { name: altName }))
          }
        }

        return Promise.all(promises)
      })
      await Promise.all(updatePromises)
    }

    showCreate.value = false
    await loadItems()
  } catch (e: any) {
    submitError.value = e?.data?.message || 'Failed to create RFQ.'
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
:deep(.unread-rfq-row) {
  background-color: rgba(66, 165, 245, 0.12) !important;
  border-left: 3px solid #42a5f5;
}
:deep(.unread-rfq-row:hover) {
  background-color: rgba(66, 165, 245, 0.2) !important;
}
:deep(.unread-rfq-row td:first-child) {
  font-weight: 700;
}
:deep(.lead-time-expired-row) {
  background-color: rgba(239, 68, 68, 0.12) !important;
  border-left: 3px solid #ef4444;
}
:deep(.lead-time-expired-row) td {
  color: #ef4444 !important;
}
:deep(.lead-time-expired-row:hover) {
  background-color: rgba(239, 68, 68, 0.2) !important;
}
</style>
