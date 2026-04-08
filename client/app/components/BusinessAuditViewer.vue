<template>
  <v-card class="glass-card">
    <v-card-title class="d-flex align-center justify-space-between py-3 px-4">
      <div class="d-flex align-center gap-2">
        <v-icon icon="mdi-timeline-clock-outline" color="primary" />
        <span class="text-h6">Activity Timeline</span>
      </div>
      <div class="d-flex align-center gap-2">
        <!-- Search -->
        <v-text-field
          v-model="searchQuery"
          label="Search"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="max-width: 200px"
        />
        <!-- Entity Type Filter -->
        <v-select
          v-model="filterEntityType"
          :items="entityTypeOptions"
          label="Entity Type"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          style="max-width: 140px"
          @update:model-value="loadLogs"
        />
        <!-- Action Category Filter -->
        <v-select
          v-model="filterActionCategory"
          :items="actionCategoryOptions"
          label="Action"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          style="max-width: 140px"
          @update:model-value="loadLogs"
        />
        <!-- Toggle switches -->
        <v-switch
          v-model="hideNoisyEntities"
          label="Hide noisy"
          density="compact"
          hide-details
          color="primary"
        />
        <v-switch
          v-model="groupByEntity"
          label="Group by entity"
          density="compact"
          hide-details
          color="primary"
        />
        <v-btn icon="mdi-refresh" variant="text" density="comfortable" @click="loadLogs" :loading="loading" />
      </div>
    </v-card-title>

    <v-divider />

    <v-card-text class="pa-0">
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">
        {{ error }}
      </v-alert>

      <v-alert v-if="displayLogs.length === 0 && !loading" type="info" variant="tonal" density="compact" class="ma-3">
        No activity recorded yet.
      </v-alert>

      <!-- Timeline View -->
      <div v-else class="pa-4">
        <v-timeline side="start" density="compact">
          <v-timeline-item
            v-for="log in displayLogs"
            :key="log.id"
            :dot-color="log.isGroup ? 'grey' : getActionColor(getDisplayActionCategory(log))"
            size="small"
          >
            <template #icon>
              <v-icon :icon="log.isGroup ? 'mdi-folder-multiple' : getActionIcon(getDisplayActionCategory(log))" size="16" />
            </template>

            <div class="d-flex align-start gap-3">
              <!-- User Avatar -->
              <v-avatar size="32" color="surface-variant" class="mt-1">
                <span class="text-body-2 font-weight-bold">{{ (log.userName || log.userId || 'System').toString().charAt(0).toUpperCase() }}</span>
              </v-avatar>

              <!-- Content -->
              <div class="flex-grow-1">
                <!-- Header -->
                <div class="d-flex align-center gap-2 flex-wrap">
                  <span class="text-body-2 font-weight-bold">{{ log.userName || log.userId || 'System' }}</span>
                  <v-chip :color="getActionColor(getDisplayActionCategory(log))" size="x-small" variant="tonal">
                    {{ getDisplayActionCategory(log) }}
                  </v-chip>
                  <v-chip size="x-small" variant="outlined" color="grey">
                    {{ getDisplayEntityType(log) }}
                  </v-chip>
                </div>

                <!-- Description -->
                <div class="text-body-2 mt-1">{{ getDisplayDescription(log) }}</div>

                <!-- Entity Link -->
                <div v-if="canNavigateToEntity(log)" class="mt-1">
                  <v-btn
                    :to="getEntityLink(log)"
                    size="x-small"
                    variant="text"
                    color="primary"
                    class="px-0"
                  >
                    <v-icon icon="mdi-open-in-new" size="14" class="mr-1" />
                    View {{ getDisplayEntityName(log) }}
                  </v-btn>
                </div>

                <!-- Timestamp -->
                <div class="text-caption text-medium-emphasis mt-1">
                  {{ formatTimestamp(log.timestamp) }}
                  <span v-if="log.ipAddress" class="ml-2">• {{ log.ipAddress }}</span>
                </div>

                <!-- Related Entity -->
                <div v-if="log.relatedEntityType && log.relatedEntityId" class="mt-2">
                  <v-chip size="x-small" variant="tonal" color="surface-variant">
                    <v-icon icon="mdi-link-variant" size="12" class="mr-1" />
                    Related: {{ log.relatedEntityType }} #{{ log.relatedEntityId }}
                  </v-chip>
                </div>

                <!-- Expand for Details (for old format) -->
                <div class="mt-2">
                  <!-- Show group expansion for grouped entries -->
                  <v-btn v-if="log.isGroup" size="x-small" variant="text" @click="expandedLogId = expandedLogId === log.id ? null : log.id">
                    {{ expandedLogId === log.id ? `Hide ${log.groupCount} actions` : `Show ${log.groupCount} actions` }}
                  </v-btn>
                  <!-- Show details for single entries -->
                  <v-btn v-else size="x-small" variant="text" @click="expandedLogId = expandedLogId === log.id ? null : log.id">
                    {{ expandedLogId === log.id ? 'Hide' : 'Show' }} details
                  </v-btn>
                  <v-expand-transition>
                    <div v-if="expandedLogId === log.id" class="mt-2 pa-3 bg-surface-lighten-1 rounded">
                      <!-- Show grouped logs -->
                      <div v-if="log.isGroup && log.groupLogs" class="d-flex flex-column gap-2">
                        <div v-for="(gLog, idx) in log.groupLogs" :key="idx" class="pa-2 border rounded">
                          <div class="d-flex align-center gap-2 mb-1">
                            <v-chip size="x-small" :color="getActionColor(getDisplayActionCategory(gLog))">
                              {{ gLog.action }}
                            </v-chip>
                            <span class="text-caption text-medium-emphasis">{{ formatTimestamp(gLog.timestamp) }}</span>
                          </div>
                          <div v-if="gLog.details" class="text-caption">{{ gLog.details }}</div>
                        </div>
                      </div>
                      
                      <!-- Details text - prioritize this as it's already human-readable -->
                      <div v-if="!log.isGroup && log.details" class="text-caption mb-3">
                        <div class="font-weight-bold mb-1">Description:</div>
                        <div>{{ log.details }}</div>
                      </div>
                      
                      <!-- OldValues/NewValues display - show only if no good details or for deeper inspection -->
                      <div v-if="!log.isGroup && (log.oldValues || log.newValues) && (!log.details || log.details.length < 50)" class="mt-2">
                        <div class="text-caption font-weight-bold mb-2">Field Changes:</div>
                        <div v-for="(value, key) in parseChanges(log)" :key="key" class="mb-2">
                          <div class="text-caption font-weight-medium text-primary">{{ formatPropertyName(key) }}:</div>
                          <div v-if="value.old !== value.new" class="d-flex align-center gap-2 text-caption">
                            <span class="text-error">{{ formatValue(value.old, key) }}</span>
                            <v-icon icon="mdi-arrow-right" size="12" color="grey" />
                            <span class="text-success">{{ formatValue(value.new, key) }}</span>
                          </div>
                          <div v-else class="text-caption text-medium-emphasis">
                            {{ formatValue(value.new, key) }}
                          </div>
                        </div>
                      </div>
                      
                      <!-- Affected columns -->
                      <div v-if="!log.isGroup && log.affectedColumns" class="mt-2 text-caption text-medium-emphasis">
                        Affected fields: {{ formatAffectedColumns(log.affectedColumns) }}
                      </div>
                    </div>
                  </v-expand-transition>
                </div>
              </div>
            </div>
          </v-timeline-item>
        </v-timeline>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="d-flex align-center justify-center pa-4 gap-4">
        <v-btn
          icon="mdi-chevron-left"
          variant="outlined"
          density="comfortable"
          :disabled="currentPage === 1"
          @click="currentPage--"
        />
        <span class="text-body-2">Page {{ currentPage }} of {{ totalPages }}</span>
        <v-btn
          icon="mdi-chevron-right"
          variant="outlined"
          density="comfortable"
          :disabled="currentPage === totalPages"
          @click="currentPage++"
        />
        <v-select
          v-model="pageSize"
          :items="[25, 50, 100, 200]"
          label="Per page"
          variant="outlined"
          density="compact"
          hide-details
          style="max-width: 100px"
          @update:model-value="currentPage = 1"
        />
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'

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
const filterEntityType = ref(props.entityName || null)
const filterActionCategory = ref<string | null>(null)
const expandedLogId = ref<number | null>(null)
const hideNoisyEntities = ref(true)
const groupByEntity = ref(false)
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = ref(50)

// Entity grouping: map child entities to their parent display name
const entityGroupMap: Record<string, string> = {
  'RFQItem': 'RFQ',
  'RFQHeader': 'RFQ',
  'RFQUserRead': 'RFQ',
  'QuoteItem': 'Quote',
  'Quote': 'Quote',
  'InvoiceItem': 'Invoice',
  'Invoice': 'Invoice',
  'POItem': 'PO',
  'PO': 'PO',
  'Alternative': 'Part Number',
  'PartNumberSupplier': 'Part Number',
  'PartNumber': 'Part Number',
  'Supplier': 'Supplier',
  'Customer': 'Customer',
  'User': 'User',
  'ProcumentRecord': 'Document',
  'EntityPermission': 'Permission',
  'Notification': 'Notification'
}

// Noisy entity types that can be hidden
const noisyEntityTypes = ['RFQUserRead', 'BulkSave', 'Login', 'Assign', 'Revoke', 'EntityPermission', 'Notification', 'Permission']

const entityTypeOptions = ['RFQ', 'Quote', 'Invoice', 'PO', 'Part Number', 'Supplier', 'Customer', 'User', 'Document', 'Permission']
const actionCategoryOptions = ['Creation', 'StatusChange', 'Update', 'ItemChange', 'Deletion']

// Filter logs to hide noisy entities
const filteredLogs = computed(() => {
  let result = logs.value
  
  // Hide noisy entities
  if (hideNoisyEntities.value) {
    result = result.filter(log => !noisyEntityTypes.includes(log.entityName) && !noisyEntityTypes.includes(log.action))
  }
  
  // Search filter
  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(log => {
      const searchableText = [
        log.userName,
        log.entityName,
        log.action,
        log.details,
        log.entityId?.toString()
      ].join(' ').toLowerCase()
      return searchableText.includes(query)
    })
  }
  
  return result
})

// Group logs by entity if enabled
const groupedLogs = computed(() => {
  if (!groupByEntity.value) return filteredLogs.value
  
  const groups: Record<string, any[]> = {}
  filteredLogs.value.forEach(log => {
    const key = `${log.entityName}-${log.entityId || 'none'}`
    if (!groups[key]) groups[key] = []
    groups[key].push(log)
  })
  
  return Object.entries(groups).flatMap(([key, groupLogs]) => {
    if (groupLogs.length === 1) return groupLogs
    // Create a summary entry for the group
    return [{
      ...groupLogs[0],
      isGroup: true,
      groupCount: groupLogs.length,
      groupLogs: groupLogs,
      details: `${groupLogs.length} actions on ${entityGroupMap[groupLogs[0].entityName] || groupLogs[0].entityName} #${groupLogs[0].entityId || ''}`
    }]
  })
})

// Pagination
const totalPages = computed(() => Math.ceil(groupedLogs.value.length / pageSize.value))
const paginatedLogs = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  const end = start + pageSize.value
  return groupedLogs.value.slice(start, end)
})

const displayLogs = computed(() => paginatedLogs.value)

onMounted(() => {
  loadLogs()
})

// Reverse mapping: parent entity -> list of child entity names in database
const parentToChildEntities: Record<string, string[]> = {
  'RFQ': ['RFQHeader', 'RFQItem', 'RFQUserRead'],
  'Quote': ['Quote', 'QuoteItem'],
  'Invoice': ['Invoice', 'InvoiceItem'],
  'PO': ['PO', 'POItem'],
  'Part Number': ['PartNumber', 'Alternative', 'PartNumberSupplier'],
  'Supplier': ['Supplier'],
  'Customer': ['Customer'],
  'User': ['User'],
  'Document': ['ProcumentRecord']
}

async function loadLogs() {
  loading.value = true
  error.value = ''
  try {
    // For system activity (no entity filter), fetch more logs to support pagination
    const limit = props.entityName ? 100 : 500
    const params: any = { limit }
    
    console.log('Loading audit logs with params:', params)
    
    if (filterEntityType.value) {
      // Map parent entity to child entities for database query
      const childEntities = parentToChildEntities[filterEntityType.value] || [filterEntityType.value]
      // Use the first child entity for the query (this is a limitation of the current API)
      params.entityName = childEntities[0]
    }
    
    if (props.entityId) {
      params.entityId = props.entityId
    }
    
    if (filterActionCategory.value) {
      params.actionCategory = filterActionCategory.value
    }
    
    const response = await api.get('/audit', { params }) as any
    console.log('Audit logs response:', response)
    // API might return array directly or wrapped in data property
    logs.value = Array.isArray(response) ? response : (response.data || [])
    console.log('Logs loaded:', logs.value.length)
  } catch (e: any) {
    console.error('Error loading audit logs:', e)
    error.value = e.response?.data?.message || e.message || 'Failed to load activity logs'
    console.log('Error set:', error.value)
  } finally {
    loading.value = false
  }
}

// Helper functions to display data with fallback for old format
function getDisplayActionCategory(log: any): string {
  if (log.actionCategory) return log.actionCategory
  // Infer from action field for old format
  const action = (log.action || '').toLowerCase()
  if (action.includes('create') || action.includes('add')) return 'Creation'
  if (action.includes('update') || action.includes('edit')) return 'Update'
  if (action.includes('delete') || action.includes('remove')) return 'Deletion'
  if (action.includes('status')) return 'StatusChange'
  return 'Update'
}

function getDisplayEntityName(log: any): string {
  if (log.entityDisplayName) return log.entityDisplayName
  const group = entityGroupMap[log.entityName] || log.entityName
  return `${group} #${log.entityId}`
}

function getDisplayEntityType(log: any): string {
  return entityGroupMap[log.entityName] || log.entityName
}

function getDisplayDescription(log: any): string {
  // If there's a good Details field, use it directly
  if (log.details && log.details.length > 10) {
    // Try to parse permission-related JSON details
    if (log.entityName === 'Permission' || log.action === 'Assign' || log.action === 'Revoke') {
      try {
        const parsed = JSON.parse(log.details)
        if (parsed.EntityName && parsed.EntityId && parsed.Permission && parsed.UserId) {
          const action = log.action === 'Revoke' ? 'Revoked' : 'Assigned'
          return `${action} ${parsed.Permission} permission on ${parsed.EntityName} #${parsed.EntityId} to User #${parsed.UserId}`
        }
      } catch {
        // If parsing fails, use original details
      }
    }
    return log.details
  }
  // Generate description from old format
  const action = log.action || 'Action'
  const entity = getDisplayEntityName(log)
  const originalEntity = log.entityName
  // Show original entity type if different from group
  if (originalEntity !== entityGroupMap[originalEntity] && entityGroupMap[originalEntity]) {
    return `${action} on ${originalEntity} (${entity})`
  }
  return `${action} on ${entity}`
}

function getActionColor(category: string): string {
  const map: Record<string, string> = {
    Creation: 'success',
    StatusChange: 'warning',
    Update: 'info',
    ItemChange: 'primary',
    Deletion: 'error'
  }
  return map[category] || 'grey'
}

function getActionIcon(category: string): string {
  const map: Record<string, string> = {
    Creation: 'mdi-plus-circle',
    StatusChange: 'mdi-swap-horizontal',
    Update: 'mdi-pencil',
    ItemChange: 'mdi-pencil-box',
    Deletion: 'mdi-delete'
  }
  return map[category] || 'mdi-information'
}

function formatTimestamp(ts: string): string {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const diffMs = now.getTime() - d.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMs / 3600000)
  const diffDays = Math.floor(diffMs / 86400000)

  if (diffMins < 1) return 'Just now'
  if (diffMins < 60) return `${diffMins}m ago`
  if (diffHours < 24) return `${diffHours}h ago`
  if (diffDays < 7) return `${diffDays}d ago`
  return d.toLocaleDateString()
}

function canNavigateToEntity(log: any): boolean {
  const entityType = entityGroupMap[log.entityName] || log.entityName
  return ['RFQ', 'Quote', 'Invoice', 'PO'].includes(entityType)
}

function getEntityLink(log: any): string {
  const entityType = entityGroupMap[log.entityName] || log.entityName
  const id = log.entityId
  switch (entityType) {
    case 'RFQ': return `/rfqs/${id}`
    case 'Quote': return `/quotes/${id}`
    case 'Invoice': return `/invoices/${id}`
    case 'PO': return `/pos/${id}`
    default: return '#'
  }
}

function parseChanges(log: any): Record<string, { old: any, new: any }> {
  const changes: Record<string, { old: any, new: any }> = {}
  const sensitiveFields = ['Password', 'HashedPassword', 'Salt', 'SecurityStamp']
  try {
    const oldV = log.oldValues ? JSON.parse(log.oldValues) : {}
    const newV = log.newValues ? JSON.parse(log.newValues) : {}
    const keys = new Set([...Object.keys(oldV), ...Object.keys(newV)])
    keys.forEach(k => {
      // Skip sensitive fields
      if (sensitiveFields.includes(k)) return
      changes[k] = { old: oldV[k], new: newV[k] }
    })
  } catch (e) {
    console.error('Error parsing changes', e)
  }
  return changes
}

function formatPropertyName(prop: string): string {
  // Convert camelCase to readable text
  return prop.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase())
}

function formatValue(val: any, key?: string): string {
  if (val === null || val === undefined) return '—'
  if (typeof val === 'boolean') return val ? 'Yes' : 'No'
  // Hide passwords
  if (key === 'Password' && val) return '*** (hidden)'
  // Format dates
  if (typeof val === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(val)) {
    return new Date(val).toLocaleString()
  }
  if (typeof val === 'object') return JSON.stringify(val)
  return String(val)
}

function formatAffectedColumns(columns: string): string {
  if (!columns) return ''
  try {
    const parsed = JSON.parse(columns)
    if (Array.isArray(parsed)) {
      return parsed.map(c => formatPropertyName(c)).join(', ')
    }
    return columns
  } catch {
    return columns
  }
}
</script>
