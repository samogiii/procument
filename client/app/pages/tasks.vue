<template>
  <div class="tasks-page pa-6">
    <div class="d-flex align-center justify-space-between mb-6">
      <div>
        <h1 class="text-h4 font-weight-bold mb-1">Task Manager</h1>
        <p class="text-subtitle-1 text-medium-emphasis">Organize and track your assigned tasks</p>
      </div>
      <v-btn
        v-if="isAdmin"
        color="primary"
        prepend-icon="mdi-plus"
        @click="openCreateDialog"
        class="text-none"
        rounded="lg"
        elevation="2"
      >
        Create Task
      </v-btn>
    </div>

    <!-- Kanban Board -->
    <v-row v-if="!loading" class="kanban-board" dense>
      <v-col v-for="column in columns" :key="column.status" cols="12" md="4">
        <v-card class="kanban-column" rounded="xl" min-height="70vh">
          <v-card-title class="d-flex align-center pa-4">
            <v-chip
              :color="column.color"
              size="small"
              class="mr-2 font-weight-bold"
              variant="elevated"
            >
              {{ getTasksByStatus(column.status).length }}
            </v-chip>
            <span class="text-h6 font-weight-bold">{{ column.title }}</span>
          </v-card-title>

          <v-divider></v-divider>

          <v-container class="pa-3 task-list">
            <v-card
              v-for="task in getTasksByStatus(column.status)"
              :key="task.id"
              class="task-card mb-3 pa-4"
              elevation="1"
              rounded="lg"
            >
              <div class="d-flex justify-space-between align-start mb-2">
                <h3 class="text-subtitle-1 font-weight-bold">{{ task.title }}</h3>
                <v-btn
                  v-if="isAdmin"
                  icon="mdi-delete-outline"
                  variant="text"
                  color="error"
                  size="x-small"
                  @click="deleteTask(task)"
                ></v-btn>
              </div>
              
              <p class="text-body-2 text-medium-emphasis mb-4" style="white-space: pre-wrap;">{{ task.description }}</p>

              <v-divider class="mb-3"></v-divider>

              <div class="d-flex align-center justify-space-between">
                <div class="d-flex align-center gap-2">
                  <v-avatar size="24" color="secondary" variant="tonal">
                    <span class="text-caption font-weight-bold">{{ task.assignedTo.substring(0, 2) }}</span>
                  </v-avatar>
                  <span class="text-caption font-weight-medium">{{ task.assignedTo }}</span>
                </div>
                <div class="d-flex align-center gap-1">
                   <v-tooltip text="Move back" v-if="task.status > 0">
                    <template v-slot:activator="{ props }">
                      <v-btn
                        v-bind="props"
                        icon="mdi-chevron-left"
                        size="x-small"
                        variant="tonal"
                        @click="moveTask(task, task.status - 1)"
                      ></v-btn>
                    </template>
                  </v-tooltip>
                  <v-tooltip text="Move forward" v-if="task.status < 2">
                    <template v-slot:activator="{ props }">
                      <v-btn
                        v-bind="props"
                        icon="mdi-chevron-right"
                        size="x-small"
                        variant="tonal"
                        @click="moveTask(task, task.status + 1)"
                      ></v-btn>
                    </template>
                  </v-tooltip>
                </div>
              </div>
              
              <div class="mt-2 d-flex align-center justify-space-between">
                <span class="text-caption text-disabled">By: {{ task.createdByCode }}</span>
                <span class="text-caption text-disabled">{{ new Date(task.createdAt).toLocaleDateString() }}</span>
              </div>
            </v-card>

            <div v-if="getTasksByStatus(column.status).length === 0" class="empty-state d-flex flex-column align-center justify-center py-10 text-disabled">
              <v-icon icon="mdi-tray-blank" size="48" class="mb-2"></v-icon>
              <span>No tasks here</span>
            </div>
          </v-container>
        </v-card>
      </v-col>
    </v-row>

    <div v-else class="d-flex justify-center align-center" style="height: 50vh;">
      <v-progress-circular indeterminate color="primary" size="64"></v-progress-circular>
    </div>

    <!-- Create Dialog -->
    <v-dialog v-model="createDialog" max-width="500">
      <v-card rounded="xl" class="pa-4">
        <v-card-title class="text-h5 font-weight-bold">Create New Task</v-card-title>
        <v-card-text>
          <v-form ref="form" v-model="formValid">
            <v-text-field
              v-model="newTask.title"
              label="Task Title"
              required
              variant="outlined"
              :rules="[v => !!v || 'Title is required']"
              class="mb-2"
            ></v-text-field>
            
            <v-textarea
              v-model="newTask.description"
              label="Description"
              variant="outlined"
              rows="4"
              class="mb-2"
            ></v-textarea>

            <v-select
              v-model="newTask.assignedTo"
              :items="allowedUsers"
              label="Assigned To"
              variant="outlined"
              required
              :rules="[v => !!v || 'User is required']"
            ></v-select>
          </v-form>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="createDialog = false" class="text-none px-6" rounded="lg">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="elevated"
            :loading="creating"
            :disabled="!formValid"
            @click="createTask"
            class="text-none px-6"
            rounded="lg"
          >Create</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const loading = ref(true)
const creating = ref(false)
const tasks = ref<any[]>([])
const createDialog = ref(false)
const formValid = ref(false)

const allowedUsers = ['GHS', 'SNP', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']

const newTask = ref({
  title: '',
  description: '',
  assignedTo: ''
})

const columns = [
  { status: 0, title: 'Not Started', color: 'grey-darken-1' },
  { status: 1, title: 'In Progress', color: 'info' },
  { status: 2, title: 'Done', color: 'success' }
]

async function loadTasks() {
  loading.value = true
  try {
    const data = await api.get('/tasks')
    tasks.value = data
  } catch (error) {
    console.error('Failed to load tasks', error)
  } finally {
    loading.value = false
  }
}

function getTasksByStatus(status: number) {
  return tasks.value.filter(t => t.status === status)
}

function openCreateDialog() {
  newTask.value = { title: '', description: '', assignedTo: '' }
  createDialog.value = true
}

async function createTask() {
  creating.value = true
  try {
    const task = await api.post('/tasks', newTask.value)
    tasks.value.unshift(task)
    createDialog.value = false
  } catch (error) {
    console.error('Failed to create task', error)
  } finally {
    creating.value = false
  }
}

async function moveTask(task: any, newStatus: number) {
  try {
    await api.patch(`/tasks/${task.id}/status`, { status: newStatus })
    task.status = newStatus
  } catch (error) {
    console.error('Failed to move task', error)
  }
}

async function deleteTask(task: any) {
  if (!confirm('Are you sure you want to delete this task?')) return
  try {
    await api.delete(`/tasks/${task.id}`)
    tasks.value = tasks.value.filter(t => t.id !== task.id)
  } catch (error) {
    console.error('Failed to delete task', error)
  }
}

onMounted(loadTasks)
</script>

<style scoped>
.tasks-page {
  background-color: var(--v-theme-background);
  min-height: 100vh;
}
.kanban-board {
  overflow-x: auto;
}
.kanban-column {
  transition: all 0.3s ease;
  background-color: rgba(var(--v-theme-on-surface), 0.04) !important;
}
.task-list {
  max-height: calc(100vh - 250px);
  overflow-y: auto;
}
.task-card {
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  border-left: 4px solid var(--v-theme-primary);
  background-color: rgb(var(--v-theme-surface)) !important;
}
.task-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.1) !important;
}
.gap-1 { gap: 4px; }
.gap-2 { gap: 8px; }
.gap-3 { gap: 12px; }
</style>
