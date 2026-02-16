<template>
  <v-card class="glass-card">
    <v-card-title class="d-flex align-center justify-space-between">
      <span>Audit History</span>
      <v-btn icon="mdi-refresh" variant="text" size="small" @click="loadLogs" :loading="loading" />
    </v-card-title>

    <v-card-text class="pa-0">
      <div class="audit-list">
        <div v-if="logs.length === 0 && !loading" class="text-center pa-4 text-caption text-medium-emphasis">
          No history available.
        </div>
        
        <div v-for="log in logs" :key="log.id" class="audit-item pa-3 border-b border-opacity-25">
          <div class="d-flex align-center justify-space-between mb-1">
            <span class="text-caption font-weight-bold text-primary">{{ log.action }}</span>
            <span class="text-caption text-medium-emphasis">{{ new Date(log.timestamp).toLocaleString() }}</span>
          </div>
          <div class="d-flex align-center gap-2 mb-1">
            <v-icon icon="mdi-account" size="12" class="text-medium-emphasis" />
            <span class="text-caption">{{ getUserName(log.userId) }}</span>
          </div>
          <div v-if="log.details" class="text-caption text-grey-lighten-1 bg-black bg-opacity-25 rounded pa-2 mt-1 font-monospace">
            {{ log.details }}
          </div>
        </div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
const props = defineProps<{
  entityName: string
  entityId: string
}>()

const api = useApi()
const logs = ref<any[]>([])
const loading = ref(false)
const users = ref<Map<number, string>>(new Map()) // Cache for user names if ID only

onMounted(async () => {
  await loadLogs()
  // If logs have UserIds, we might want to fetch user names if not provided in log view model?
  // AuditLog entity has UserId but no navigation include in the Controller usually unless we did it.
  // The Controller GetLogs returns List<AuditLog>. AuditLog has no User navigation property usually serializable or included?
  // Let's check AuditLog entity. It has `public long? UserId { get; set; }`. It DOES NOT have User navigation property in Shared entity usually?
  // Wait, I created AuditLog.cs in Shared/Entities. Let's check if I added navigation.
})

async function loadLogs() {
  loading.value = true
  try {
    logs.value = await api.get<any[]>(`/audit/${props.entityName}/${props.entityId}`)
    // If we need user names, we might need to fetch them or if the API includes them.
    // For now, displaying UserId if name not available.
    // Or we can pre-fetch users if we have the list from PermissionManager or cached.
  } catch (e) {
    console.error('Failed to load audit logs', e)
  } finally {
    loading.value = false
  }
}

function getUserName(userId?: number) {
  if (!userId) return 'System'
  // In a real app, I'd look this up from a store or the log might include UserName if I projected it in DTO.
  // Currently just showing ID effectively unless I fetch users.
  return `User #${userId}`
}
</script>

<style scoped>
.glass-card {
  background: rgba(30, 41, 59, 0.7) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
  backdrop-filter: blur(10px);
}
.audit-list {
  max-height: 400px;
  overflow-y: auto;
}
.audit-item:last-child {
  border-bottom: none !important;
}
.font-monospace {
  font-family: 'JetBrains Mono', monospace;
}
</style>
