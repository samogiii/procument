<template>
  <v-dialog v-model="model" max-width="750" scrollable>
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-shield-account-outline" color="primary" class="mr-2" />
        Bulk Assign Permissions — {{ entityLabel }}s
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="model = false" />
      </v-card-title>

      <v-divider />

      <v-card-text class="pa-4" style="max-height: 70vh;">
        <!-- Step 1: Select Users -->
        <p class="text-subtitle-2 font-weight-medium mb-2">
          <v-icon icon="mdi-numeric-1-circle" size="18" color="primary" class="mr-1" />
          Select Users
        </p>
        <v-autocomplete
          v-model="selectedUsers"
          :items="users"
          item-title="name"
          item-value="id"
          label="Choose users"
          variant="outlined"
          density="compact"
          hide-details
          multiple
          chips
          closable-chips
          class="mb-5"
          :loading="loadingUsers"
        >
          <template #chip="{ props: chipProps, item }">
            <v-chip v-bind="chipProps" color="primary" variant="tonal" size="small">
              {{ item.title }}
            </v-chip>
          </template>
        </v-autocomplete>

        <!-- Step 2: Select Role -->
        <p class="text-subtitle-2 font-weight-medium mb-2">
          <v-icon icon="mdi-numeric-2-circle" size="18" color="primary" class="mr-1" />
          Select Role
        </p>
        <v-btn-toggle v-model="selectedRole" mandatory color="primary" class="mb-5" density="compact">
          <v-btn value="Checker" prepend-icon="mdi-eye-check">Checker</v-btn>
          <v-btn value="Procurer" prepend-icon="mdi-cart-check">Procurer</v-btn>
        </v-btn-toggle>

        <!-- Step 3: Select Entity IDs -->
        <p class="text-subtitle-2 font-weight-medium mb-2">
          <v-icon icon="mdi-numeric-3-circle" size="18" color="primary" class="mr-1" />
          Select {{ entityLabel }}s
        </p>
        <v-autocomplete
          v-model="selectedEntityIds"
          :items="entityItems"
          :item-title="entityTitleKey"
          item-value="id"
          :label="`Choose ${entityLabel}s`"
          variant="outlined"
          density="compact"
          hide-details
          multiple
          chips
          closable-chips
          class="mb-4"
          :loading="loadingEntities"
        >
          <template #chip="{ props: chipProps, item }">
            <v-chip v-bind="chipProps" color="secondary" variant="tonal" size="small">
              {{ item.title }}
            </v-chip>
          </template>
        </v-autocomplete>

        <!-- Summary -->
        <v-alert
          v-if="selectedUsers.length && selectedEntityIds.length"
          type="info"
          variant="tonal"
          density="compact"
          class="mt-2"
        >
          Assigning <strong>{{ selectedRole }}</strong> to
          <strong>{{ selectedUsers.length }}</strong> user(s) on
          <strong>{{ selectedEntityIds.length }}</strong> {{ entityLabel.toLowerCase() }}(s).
        </v-alert>

        <v-alert v-if="resultMessage" :type="resultType" variant="tonal" density="compact" class="mt-3">
          {{ resultMessage }}
        </v-alert>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="model = false">Cancel</v-btn>
        <v-btn
          color="primary"
          :loading="assigning"
          :disabled="!canAssign"
          @click="assignAll"
        >
          Assign Permissions
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  entityName: 'RFQ' | 'Quote'
}>()

const model = defineModel<boolean>({ default: false })

const api = useApi()

// State
const users = ref<any[]>([])
const entityItems = ref<any[]>([])
const selectedUsers = ref<number[]>([])
const selectedEntityIds = ref<number[]>([])
const selectedRole = ref<string>('Checker')
const loadingUsers = ref(false)
const loadingEntities = ref(false)
const assigning = ref(false)
const resultMessage = ref('')
const resultType = ref<'success' | 'error'>('success')

const entityLabel = computed(() => props.entityName === 'RFQ' ? 'RFQ' : 'Quote')
const entityTitleKey = computed(() => props.entityName === 'RFQ' ? 'name' : 'quoteNumber')
const canAssign = computed(() => selectedUsers.value.length > 0 && selectedEntityIds.value.length > 0)

// Load data when dialog opens
watch(model, async (open) => {
  if (open) {
    resultMessage.value = ''
    await Promise.all([loadUsers(), loadEntities()])
  }
})

async function loadUsers() {
  loadingUsers.value = true
  try {
    users.value = await api.get<any[]>('/users')
  } catch { users.value = [] }
  finally { loadingUsers.value = false }
}

async function loadEntities() {
  loadingEntities.value = true
  try {
    if (props.entityName === 'RFQ') {
      const res = await api.get<any>('/rfqs')
      entityItems.value = res || []
    } else {
      const res = await api.get<any>('/quotes?page=1&pageSize=500')
      entityItems.value = res.items || []
    }
  } catch { entityItems.value = [] }
  finally { loadingEntities.value = false }
}

async function assignAll() {
  assigning.value = true
  resultMessage.value = ''
  let successCount = 0
  let errorCount = 0

  for (const userId of selectedUsers.value) {
    for (const entityId of selectedEntityIds.value) {
      try {
        await api.post('/permissions/assign', {
          userId,
          entityName: props.entityName,
          entityId: String(entityId),
          permission: selectedRole.value,
        })
        successCount++
      } catch {
        errorCount++
      }
    }
  }

  if (errorCount === 0) {
    resultType.value = 'success'
    resultMessage.value = `Successfully assigned ${successCount} permission(s).`
    // Reset selections
    selectedUsers.value = []
    selectedEntityIds.value = []
  } else {
    resultType.value = 'error'
    resultMessage.value = `${successCount} succeeded, ${errorCount} failed.`
  }

  assigning.value = false
}
</script>

<style scoped>
.glass-card {
  background: rgba(30, 41, 59, 0.92) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
  backdrop-filter: blur(10px);
}
</style>
