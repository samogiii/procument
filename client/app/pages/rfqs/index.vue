<template>
  <div>
    <PageHeader title="RFQs">
      <template #actions>
        <v-btn
          v-if="isAdmin"
          prepend-icon="mdi-shield-account"
          variant="tonal"
          @click="showBulkPerms = true"
        >
          Manage Permissions
        </v-btn>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreateModal">
          New RFQ
        </v-btn>
      </template>
    </PageHeader>

    <!-- RFQ List -->
    <v-card class="glass-card">
      <v-card-text>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search RFQs..."
          single-line
          hide-details
          class="mb-4"
        />
        <v-data-table-server
          :headers="headers"
          :items="items"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="10"
          @update:options="loadItems"
          @click:row="goToRfq"
        >
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>
          <template #item.leadTime="{ item }">
            {{ new Date(item.leadTime).toLocaleDateString() }}
          </template>
          <template #item.itemCount="{ item }">
            <v-chip size="small" color="secondary">{{ item.items?.length || 0 }} parts</v-chip>
          </template>
          <!-- <template #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`/rfqs/${item.id}`" />
          </template> -->
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <!-- ═══════════ Create RFQ Modal ═══════════ -->
    <v-dialog v-model="showCreate" max-width="800" persistent scrollable>
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
            <!-- Date -->
            <v-text-field
              v-model="form.date"
              label="Date *"
              prepend-inner-icon="mdi-calendar"
              type="date"
              :rules="[rules.required]"
              class="mb-3"
            />
            <!-- Lead Time -->
            <v-text-field
              v-model="form.leadTime"
              label="Lead Time *"
              prepend-inner-icon="mdi-calendar"
              type="date"
              :rules="[rules.required]"
              class="mb-3"
            />

            <!-- ── Part Numbers Section ── -->
            <v-divider class="mb-4" />

            <div class="d-flex align-center mb-3">
              <v-icon icon="mdi-cog-outline" color="secondary" class="mr-2" size="20" />
              <span class="text-subtitle-1 font-weight-medium">Part Numbers</span>
              <v-spacer />
              <v-text-field
                v-model.number="partCount"
                type="number"
                label="# of fields"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 100px"
                min="1"
                max="100"
                @update:model-value="adjustPartFields"
              />
              <v-btn
                icon="mdi-plus"
                color="primary"
                size="small"
                variant="outlined"
                class="ml-2"
                @click="addPartField"
              />
            </div>

            <p class="text-caption text-medium-emphasis mb-3">
              Enter part numbers below. At least 1 is required. Empty fields will be ignored.
              Type 3+ characters to search existing parts.
            </p>

            <div
              v-for="(pn, index) in form.partNumbers"
              :key="index"
              class="d-flex align-center mb-2"
            >
              <span class="text-caption text-medium-emphasis mr-2" style="min-width: 28px">
                {{ index + 1 }}.
              </span>
              <v-combobox
                v-model="form.partNumbers[index]"
                :items="partSuggestions[index] || []"
                item-title="name"
                item-value="name"
                :label="`Part Number ${index + 1}`"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                class="flex-grow-1"
                :loading="partLoading[index]"
                @update:search="(val: string) => searchParts(val, index)"
              >
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
              <v-btn
                v-if="form.partNumbers.length > 1"
                icon="mdi-close"
                size="x-small"
                variant="text"
                color="error"
                class="ml-1"
                @click="removePartField(index)"
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

    <!-- Bulk Permission Manager -->
    <BulkPermissionManager v-model="showBulkPerms" entity-name="RFQ" />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const router = useRouter()

const isAdmin = computed(() => authStore.isAdmin)
const showBulkPerms = ref(false)

// ── List state ──
const search = ref('')
const loading = ref(false)
const items = ref<any[]>([])
const totalItems = ref(0)

const headers = [
  { title: 'ID', key: 'id', width: '80px' },
  { title: 'Name', key: 'name' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Lead Time', key: 'leadTime' },
  { title: 'Parts', key: 'itemCount', sortable: false },
  { title: 'Created', key: 'createdAt' },
  // { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function loadItems(options: any) {
  loading.value = true
  try {
    const res = await api.get<any>('/rfqs')
    items.value = res || []
    totalItems.value = res?.length || 0
  } catch {}
  finally { loading.value = false }
}

const goToRfq = (pointerEvent, rowData) => {
  // rowData contains { index, item, internalItem, columns }
  const item = rowData.item; 
  
  // console.log("Clicked ID:", item.id);
  router.push(`/rfqs/${item.id}`);
};

// ── Create Modal state ──
const showCreate = ref(false)
const formRef = ref<any>(null)
const submitting = ref(false)
const submitError = ref('')
const partCount = ref(10)

const form = ref({
  name: '',
  customerName: null as any,
  leadTime: '',
  partNumbers: Array(10).fill(null) as (any | null)[],
  date: '',
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
  if (!val || val.length < 3) {
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
  if (!val || val.length < 3) {
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

// ── Part field management ──
function adjustPartFields(val: number) {
  const count = Math.max(1, Math.min(100, val || 1))
  partCount.value = count
  const current = form.value.partNumbers.length
  if (count > current) {
    form.value.partNumbers.push(...Array(count - current).fill(null))
  } else if (count < current) {
    form.value.partNumbers.splice(count)
  }
}

function addPartField() {
  form.value.partNumbers.push(null)
  partCount.value = form.value.partNumbers.length
}

function removePartField(index: number) {
  form.value.partNumbers.splice(index, 1)
  partCount.value = form.value.partNumbers.length
}

function openCreateModal() {
  form.value = {
    name: '',
    customerName: null,
    leadTime: '',
    partNumbers: Array(10).fill(null),
  }
  partCount.value = 10
  submitError.value = ''
  showCreate.value = true
}

// ── Submit ──
async function submitRfq() {
  submitError.value = ''

  // Validate required fields
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
    submitError.value = 'Lead Time is required.'
    return
  }

  // Collect non-empty part numbers
  const partNumbers = form.value.partNumbers
    .map((pn: any) => {
      if (!pn) return null
      if (typeof pn === 'object') return pn.name
      if (typeof pn === 'string' && pn.trim()) return pn.trim()
      return null
    })
    .filter(Boolean) as string[]

  if (partNumbers.length === 0) {
    submitError.value = 'At least 1 Part Number is required.'
    return
  }

  submitting.value = true
  try {
    const payload = {
      name: form.value.name.trim(),
      customerName: customerName.trim(),
      leadTime: new Date(form.value.leadTime).toISOString(),
      userId: authStore.user?.id || 0,
      partNumbers,
    }

    await api.post('/rfqs', payload)
    showCreate.value = false
    // Refresh list
    await loadItems({})
  } catch (e: any) {
    submitError.value = e?.data?.message || 'Failed to create RFQ.'
  } finally {
    submitting.value = false
  }
}
</script>
