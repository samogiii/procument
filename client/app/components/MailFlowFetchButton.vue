<template>
  <!-- The robot floats just left of the button (see DancingRobot), so it needs this anchor -->
  <div class="mailflow-fetch">
    <DancingRobot :show="robot.show" :dancing="running" :message="robot.message" :error="robot.error" @close="hideRobot" />
    <v-menu v-model="menu" :close-on-content-click="false" location="bottom end">
      <template #activator="{ props: activator }">
        <v-btn
          v-bind="activator"
          color="primary"
          variant="tonal"
          size="small"
          prepend-icon="mdi-robot-happy-outline"
          :loading="running"
        >
          Fetch from MailFlow
        </v-btn>
      </template>

      <v-card min-width="300" class="pa-3">
        <div class="text-subtitle-2 font-weight-bold mb-1">Read {{ noun }} emails now</div>
        <div class="text-caption text-medium-emphasis mb-3">
          MailFlow downloads the emails, reads the {{ noun }}s and sends them to Pending.
        </div>
        <v-alert v-if="loadError" type="error" variant="tonal" density="compact" class="mb-3 text-caption">{{ loadError }}</v-alert>
        <v-select
          v-model="form.accountId"
          :items="accounts"
          item-title="label"
          item-value="id"
          label="Mailbox"
          density="compact"
          variant="outlined"
          hide-details
          class="mb-3"
          :disabled="!accounts.length"
        />
        <v-btn-toggle v-model="form.mode" mandatory density="compact" color="primary" variant="outlined" divided class="mb-3 w-100">
          <v-btn value="new" class="flex-grow-1" size="small">New mail only</v-btn>
          <v-btn value="all" class="flex-grow-1" size="small">All mail</v-btn>
        </v-btn-toggle>
        <v-text-field
          v-model.number="form.maxEmails"
          type="number"
          min="0"
          label="Max emails (0 = no limit)"
          density="compact"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-btn block color="primary" :loading="starting" :disabled="running || !accounts.length" @click="start">
          {{ running ? 'Already running…' : 'Start' }}
        </v-btn>
      </v-card>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
/**
 * Email Quotes / Email RFQs pages: ask MailFlow to run its Quote Export (kind "quote") or CSV Export
 * (kind "rfq") now, through our API (/mailflow/quote-export or /mailflow/csv-export proxy to MailFlow),
 * with a dancing robot while it works. The lines arrive in Pending as usual; `finished` tells the page to reload it.
 */
const props = withDefaults(defineProps<{ kind?: 'quote' | 'rfq' }>(), { kind: 'quote' })
const emit = defineEmits<{ finished: [] }>()

const endpoint = computed(() => props.kind === 'rfq' ? '/mailflow/csv-export' : '/mailflow/quote-export')
const noun = computed(() => props.kind === 'rfq' ? 'RFQ' : 'quote')

const api = useApi()
const menu = ref(false)
const accounts = ref<{ id: number; label: string; kind?: string }[]>([])
const loadError = ref('')
const starting = ref(false)
const running = ref(false)
const form = reactive({ accountId: null as number | null, mode: 'new', maxEmails: 50 })
const robot = reactive({ show: false, message: '', error: false })

const POLL_MS = 3000
let pollTimer: ReturnType<typeof setTimeout> | undefined
let hideTimer: ReturnType<typeof setTimeout> | undefined

function errorText(e: any, fallback: string) {
  return e?.data?.message || fallback
}

function progressText(s: any) {
  const emails = s.results?.length ?? 0
  return emails
    ? `Reading emails… ${emails} ${noun.value} email${emails === 1 ? '' : 's'}, ${s.itemCount} line${s.itemCount === 1 ? '' : 's'} so far`
    : 'Reading emails…'
}

async function loadStatus() {
  const s = await api.get<any>(endpoint.value)
  accounts.value = s.accounts ?? []
  // Default: the mailbox set to this export on MailFlow's Accounts page.
  if (form.accountId == null || !accounts.value.some(a => a.id === form.accountId))
    form.accountId = (accounts.value.find(a => a.kind === props.kind) ?? accounts.value[0])?.id ?? null
  return s
}

async function poll() {
  try {
    const s = await loadStatus()
    if (s.running) {
      robot.message = progressText(s)
      pollTimer = setTimeout(poll, POLL_MS)
      return
    }
    running.value = false
    const emails = s.results?.length ?? 0
    robot.message = emails
      ? `Done! ${emails} ${noun.value} email${emails === 1 ? '' : 's'}, ${s.itemCount} line${s.itemCount === 1 ? '' : 's'} sent to Pending.`
      : `Done! No new ${noun.value} emails this time.`
    if (!s.autoImport) robot.message += ' (MailFlow auto-import is off — nothing was sent.)'
    emit('finished')
    hideTimer = setTimeout(hideRobot, 8000)
  } catch (e) {
    running.value = false
    showError(errorText(e, 'Lost contact with MailFlow.'))
  }
}

async function start() {
  starting.value = true
  clearTimeout(hideTimer)
  try {
    await api.post(`${endpoint.value}/run`, { ...form })
    follow('Reading emails…')
  } catch (e: any) {
    // Someone else's run is already going — dance along with that one.
    if ((e?.statusCode ?? e?.status) === 409) follow(e?.data ? progressText(e.data) : 'Reading emails…')
    else loadError.value = errorText(e, 'Could not start MailFlow.')
  } finally {
    starting.value = false
  }
}

function follow(message: string) {
  menu.value = false
  running.value = true
  Object.assign(robot, { show: true, error: false, message })
  clearTimeout(pollTimer)
  pollTimer = setTimeout(poll, POLL_MS)
}

function showError(message: string) {
  Object.assign(robot, { show: true, error: true, message })
  hideTimer = setTimeout(hideRobot, 10000)
}

function hideRobot() {
  clearTimeout(hideTimer)
  if (!running.value) robot.show = false
}

// Accounts for the menu; a run started elsewhere (or before a reload) brings the robot back.
watch(menu, async (open) => {
  if (!open) return
  loadError.value = ''
  try { await loadStatus() } catch (e) { loadError.value = errorText(e, 'MailFlow is not reachable.') }
})

onMounted(async () => {
  try {
    const s = await loadStatus()
    if (s.running) follow(progressText(s))
  } catch { /* shown when the menu opens */ }
})

onBeforeUnmount(() => {
  clearTimeout(pollTimer)
  clearTimeout(hideTimer)
})
</script>

<style scoped>
.mailflow-fetch {
  position: relative;
  display: inline-flex;
}
</style>
