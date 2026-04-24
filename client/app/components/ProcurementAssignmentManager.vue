<template>
  <v-dialog v-model="model" max-width="800">
    <v-card>
      <v-toolbar color="primary" density="compact">
        <v-toolbar-title class="text-body-1 font-weight-bold">Manage Procurement Assignments</v-toolbar-title>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="model = false" />
      </v-toolbar>

      <v-card-text class="pa-0">
        <v-data-table
          :headers="headers"
          :items="assignments"
          :loading="loading"
          density="comfortable"
          class="bg-transparent"
        >
          <template #item.level="{ item }">
            <v-chip size="x-small" :color="item.level === 'Header' ? 'primary' : 'secondary'" variant="tonal" class="text-uppercase font-weight-bold">
              {{ item.level }}
            </v-chip>
          </template>

          <template #item.target="{ item }">
            <div class="d-flex flex-column">
              <span class="font-weight-bold text-body-2">{{ item.procurementNumber }}</span>
              <span v-if="item.level === 'Item'" class="text-caption text-medium-emphasis">
                {{ item.partNumber }} (ID: {{ item.entityId }})
              </span>
            </div>
          </template>

          <template #item.user="{ item }">
            <div class="d-flex align-center gap-2">
              <v-avatar size="24" color="grey-lighten-3">
                <v-icon icon="mdi-account" size="16" color="grey-darken-1" />
              </v-avatar>
              <div class="d-flex flex-column">
                <span class="text-body-2">{{ item.userName }}</span>
                <span class="text-caption text-medium-emphasis">{{ item.userEmail }}</span>
              </div>
            </div>
          </template>

          <template #item.permission="{ item }">
            <v-chip size="x-small" :color="item.permission === 'Edit' ? 'success' : 'info'" variant="flat">
              {{ item.permission }}
            </v-chip>
          </template>

          <template #item.actions="{ item }">
            <v-btn
              icon="mdi-delete-outline"
              variant="text"
              size="small"
              color="error"
              :loading="deleting === item.id"
              @click="deleteAssignment(item)"
            />
          </template>

          <template #no-data>
            <div class="pa-8 text-center">
              <v-icon icon="mdi-account-off-outline" size="48" color="grey-lighten-1" class="mb-2" />
              <div class="text-body-1 text-medium-emphasis">No active assignments found.</div>
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000">
      {{ snackbarText }}
    </v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
const model = defineModel<boolean>({ default: false })
const api = useApi()

const loading = ref(false)
const deleting = ref<number | null>(null)
const assignments = ref<any[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const headers = [
  { title: 'Level', key: 'level', width: '100px' },
  { title: 'Target Procurement', key: 'target' },
  { title: 'Assigned User', key: 'user' },
  { title: 'Type', key: 'permission', width: '100px' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]

async function loadData() {
  loading.value = true
  try {
    // 1. Fetch all Procurement permissions
    const perms = await api.get<any[]>('/permissions/Procurement')
    
    // 2. Fetch all Procurements to map IDs to numbers/parts
    // Note: fetching pageSize=999 to get everything for mapping
    const procsRes = await api.get<any>('/procurements?pageSize=999')
    const procs = procsRes.items || []
    
    // Build a map of Procurement ID -> { number, items: { id -> partName } }
    const procMap = new Map()
    const itemToProcMap = new Map()
    
    for (const p of procs) {
      // For each procurement, we might need its items if this is an item-level perm
      // The basic /procurements list might not have items, but let's check.
      // If it doesn't, we'll have to fetch detail for the ones missing.
      procMap.set(String(p.id), p)
      
      if (p.items) {
        for (const it of p.items) {
          itemToProcMap.set(String(it.id), { 
            procurementNumber: p.procurementNumber, 
            partNumber: it.partNumberName 
          })
        }
      }
    }
    
    // 3. Process permissions
    const result = []
    for (const p of perms) {
      const eId = String(p.entityId)
      let level = 'Header'
      let procNum = 'Unknown'
      let partName = ''
      
      if (procMap.has(eId)) {
        level = 'Header'
        procNum = procMap.get(eId).procurementNumber
      } else if (itemToProcMap.has(eId)) {
        level = 'Item'
        const info = itemToProcMap.get(eId)
        procNum = info.procurementNumber
        partName = info.partNumber
      } else {
        // Might be an item that wasn't in the list? 
        // Or a legacy/deleted procurement.
        // Try to fetch specifically if it looks like an item
        // We can distinguish by trying to fetch procurement detail.
        // But for now, let's just label it unknown.
        level = 'Item/Unknown'
        procNum = `ID: ${eId}`
      }
      
      result.push({
        id: p.id, // permission id
        entityId: p.entityId,
        level,
        procurementNumber: procNum,
        partNumber: partName,
        userId: p.userId,
        userName: p.user?.name || 'Unknown',
        userEmail: p.user?.email || '',
        permission: p.permission
      })
    }
    
    assignments.value = result
  } catch (e) {
    console.error('[ProcurementAssignmentManager] Failed to load', e)
    showSnack('Failed to load assignments', 'error')
  } finally {
    loading.value = false
  }
}

async function deleteAssignment(item: any) {
  if (!confirm(`Are you sure you want to remove ${item.userName} from ${item.procurementNumber}?`)) return
  
  deleting.value = item.id
  try {
    await api.del(`/permissions/${item.id}`)
    assignments.value = assignments.value.filter(a => a.id !== item.id)
    showSnack('Assignment removed', 'success')
  } catch (e) {
    showSnack('Failed to remove assignment', 'error')
  } finally {
    deleting.value = null
  }
}

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

watch(model, (val) => {
  if (val) loadData()
})
</script>
