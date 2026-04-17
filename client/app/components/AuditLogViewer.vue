<template>
  <v-card class="glass-card">
    <v-card-title class="d-flex align-center justify-space-between py-3 px-4">
      <div class="d-flex align-center gap-2">
        <v-icon icon="mdi-history" color="primary" />
        <span class="text-h6">Audit History</span>
      </div>
      <div class="d-flex align-center gap-2">
        <v-btn-toggle v-if="!showAllOnly" v-model="viewMode" density="compact" mandatory color="primary" rounded="lg">
          <v-btn value="entity" size="small" variant="text">This {{ entityName }}</v-btn>
          <v-btn value="all" size="small" variant="text">All</v-btn>
        </v-btn-toggle>
        <v-btn icon="mdi-refresh" variant="text" density="comfortable" @click="loadLogs" :loading="loading" />
      </div>
    </v-card-title>

    <v-divider />

    <v-card-text class="pa-0">
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">
        {{ error }}
      </v-alert>

      <v-data-table
        :headers="headers"
        :items="logs"
        :loading="loading"
        density="compact"
        class="bg-transparent"
        expand-on-click
        item-value="id"
        :items-per-page="50"
      >
        <!-- User Column -->
        <template #item.userName="{ item }">
          <div class="d-flex align-center gap-2">
            <v-avatar size="24" color="surface-variant">
              <span class="text-caption">{{ (item.userName || '#').charAt(0).toUpperCase() }}</span>
            </v-avatar>
            <div class="d-flex flex-column">
              <span class="text-caption font-weight-bold">{{ item.userName || 'System' }}</span>
              <span class="text-caption text-medium-emphasis" v-if="item.ipAddress" style="font-size: 10px">{{ item.ipAddress }}</span>
            </div>
          </div>
        </template>

        <!-- Entity Column -->
        <template #item.entityName="{ item }">
          <div class="d-flex flex-column">
            <span class="text-caption font-weight-medium">{{ item.entityName }}</span>
            <span class="text-caption text-medium-emphasis">#{{ item.entityId }}</span>
          </div>
        </template>

        <!-- Action Column -->
        <template #item.action="{ item }">
          <v-chip :color="actionColor(item.action)" size="x-small" label class="font-weight-bold text-uppercase">
            {{ item.action }}
          </v-chip>
        </template>

        <!-- Timestamp Column -->
        <template #item.timestamp="{ item }">
          <span class="text-caption" :title="new Date(item.timestamp).toLocaleString()">
            {{ formatDate(item.timestamp) }}
          </span>
        </template>

        <!-- Detail Row (Expanded) -->
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="pa-4 bg-surface-lighten-4">
              <div class="d-flex flex-column gap-2">
                <!-- Details Text -->
                <div class="text-body-2 font-italic text-medium-emphasis mb-2 border-l-4 pl-3 py-1" style="border-color: rgba(var(--v-theme-primary), 0.5)">
                  {{ item.details || 'No specific details recorded.' }}
                </div>

                <!-- Changes Table -->
                <v-table v-if="hasChanges(item)" density="compact" class="rounded border" style="background: rgba(0,0,0,0.2)">
                  <thead>
                    <tr>
                      <th class="text-caption font-weight-bold">Property</th>
                      <th class="text-caption font-weight-bold text-error">Old Value</th>
                      <th class="text-caption font-weight-bold text-success">New Value</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="change in getChanges(item)" :key="change.prop">
                      <td class="text-caption font-weight-medium">{{ change.prop }}</td>
                      <td class="text-caption font-monospace text-error bg-error-lighten-5 bg-opacity-10">{{ change.oldVal }}</td>
                      <td class="text-caption font-monospace text-success bg-success-lighten-5 bg-opacity-10">{{ change.newVal }}</td>
                    </tr>
                  </tbody>
                </v-table>
                
                <div v-else class="text-caption text-center text-medium-emphasis">
                  No property-level changes captured.
                </div>
              </div>
            </td>
          </tr>
        </template>
      </v-data-table>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'

const props = withDefaults(defineProps<{
  entityName?: string
  entityId?: string
  showAllOnly?: boolean
}>(), {
  showAllOnly: false,
  entityName: '',
  entityId: ''
})

const api = useApi()
const logs = ref<any[]>([])
const loading = ref(false)
const error = ref('')
const viewMode = ref<'entity' | 'all'>(props.showAllOnly ? 'all' : 'all')

const headers = [
  { title: 'User', key: 'userName', width: '25%' },
  { title: 'Action', key: 'action', width: '15%' },
  { title: 'Entity', key: 'entityName', width: '20%' },
  { title: 'Changes', key: 'details', width: '25%' }, // Summary
  { title: 'Time', key: 'timestamp', align: 'end' as const, width: '15%' },
]

watch(viewMode, () => loadLogs())

onMounted(() => {
  loadLogs()
})

async function loadLogs() {
  loading.value = true
  error.value = ''
  try {
    let url = '/audit?limit=200'
    // If specific entity view requested (and we have IDs)
    if (viewMode.value === 'entity' && props.entityName && props.entityId) {
      // url = `/audit/${props.entityName}/${props.entityId}`
      // Actually backend might not support this filter on /audit path yet?
      // Wait, AuditController has GetLogs(entityName, entityId) ?
      // No, strictly specific endpoint /api/audit is GetAllLogs (Admin only).
      // We need to implement filtering on frontend or backend.
      // The current AuditController in Program.cs only has:
      // GET /api/audit (GetAllLogs)
      
      // But wait! UsersController in AuthController.cs?? No.
      // AuditController.cs:
      // [HttpGet] GetAllLogs()
      
      // Did I implement filtering?
    }
    
    // For now, load all and filter in frontend if needed, assuming admin access.
    // If not admin, the API might return 403.
    // Let's assume user is admin as they requested "admin can see".
    
    const data = await api.get<any[]>(url)
    
    if (viewMode.value === 'entity' && props.entityName && props.entityId) {
       logs.value = data.filter((l: any) => l.entityName == props.entityName && l.entityId == props.entityId)
    } else {
       logs.value = data
    }

  } catch (e: any) {
    console.error('Failed to load logs', e)
    error.value = 'Failed to load logs. Ensure you are Admin.'
  } finally {
    loading.value = false
  }
}

function hasChanges(item: any) {
  return !!(item.oldValues || item.newValues)
}

function getChanges(item: any) {
  const changes: { prop: string, oldVal: any, newVal: any }[] = []
  
  if (!item.oldValues && !item.newValues) return []

  try {
    const oldV = item.oldValues ? JSON.parse(item.oldValues) : {}
    const newV = item.newValues ? JSON.parse(item.newValues) : {}

    // Merge keys
    const keys = new Set([...Object.keys(oldV), ...Object.keys(newV)])

    keys.forEach(k => {
      changes.push({
        prop: k,
        oldVal: formatVal(oldV[k]),
        newVal: formatVal(newV[k])
      })
    })
  } catch (e) {
    console.error('Error parsing values', e)
  }
  return changes
}

function formatVal(v: any): string {
  if (v === null || v === undefined) return '—'
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}

function actionColor(action: string) {
  if (!action) return 'grey'
  const a = action.toLowerCase()
  if (a.includes('create') || a.includes('add') || a === 'added') return 'success'
  if (a.includes('update') || a.includes('edit') || a.includes('change') || a === 'modified') return 'warning'
  if (a.includes('delete') || a.includes('remove') || a === 'deleted') return 'error'
  if (a.includes('login') || a.includes('assign')) return 'info'
  return 'default'
}

function formatDate(ts: string) {
  if (!ts) return ''
  const d = new Date(ts)
  // return d.toLocaleString() // Full
  // Smart format
  const now = new Date()
  if (d.toDateString() === now.toDateString()) return d.toLocaleTimeString()
  return d.toLocaleDateString()
}
</script>

