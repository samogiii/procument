<template>
  <div>
    <PageHeader title="Procurement Plus">
      <template #actions>
        <!-- Every user has their own parts; SuperAdmin can open anyone's -->
        <v-select
          v-if="isSuperAdmin"
          v-model="asUser"
          :items="userOptions"
          item-title="title"
          item-value="value"
          density="compact"
          variant="outlined"
          hide-details
          prepend-inner-icon="mdi-account-switch-outline"
          style="min-width: 200px; max-width: 240px"
        />
      </template>
    </PageHeader>

    <v-tabs v-model="tab" color="primary" class="mb-4">
      <v-tab value="parts" prepend-icon="mdi-format-list-bulleted">Parts</v-tab>
      <v-tab value="ils" prepend-icon="mdi-airplane-search">ILS</v-tab>
      <v-tab value="preferred" prepend-icon="mdi-star-circle-outline">Preferred suppliers</v-tab>
    </v-tabs>
    <p class="text-caption text-medium-emphasis mt-n2 mb-3">
      {{ tab === 'parts'
        ? `Parts assigned to you (PPE) from Email RFQs, and parts you add yourself — each covers every RFQ asking for it with a deadline in the last ${PPE_DEADLINE_DAYS} days. A price typed once goes onto all of them; editing a quote updates it on all of them. Your folders sort them; Public lists every part.`
        : tab === 'ils'
          ? 'Upload ILS files; picked suppliers are added under every part they list. Star a supplier to add it to your preferred suppliers.'
          : 'Your own preferred suppliers (the same list as Procurement ProMax), each with the items it lists for your parts in your current ILS files — price, condition, qty and its emails to copy.' }}
    </p>

    <!-- Both stay mounted: switching tabs keeps the expanded parts and the files -->
    <div v-show="tab === 'parts'">
      <PpePartsTable ref="partsTable" />
    </div>
    <!-- One ILS tab for both views: the files and their rows are loaded once -->
    <div v-show="tab !== 'parts'">
      <PpeIlsTab :view="tab === 'preferred' ? 'preferred' : 'all'" @changed="partsTable?.load()" />
    </div>
  </div>
</template>

<script setup lang="ts">
// Access: Menu Access "Procurement Plus" (route gate in middleware/auth.global.ts, nav in layouts/default.vue).
// The old page is Procurement ProMax (/procurement-promax, never in the menu).
import { PPE_DEADLINE_DAYS } from '~/utils/ppe'

const authStore = useAuthStore()
const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const route = useRoute()
const router = useRouter()

type Tab = 'parts' | 'ils' | 'preferred'
const tab = ref<Tab>(route.query.tab === 'ils' || route.query.tab === 'preferred' ? route.query.tab as Tab : 'parts')
watch(tab, (t) => {
  const query = { ...route.query }
  if (t === 'parts') delete query.tab
  else query.tab = t
  router.replace({ query })
})

const partsTable = ref<{ load: () => Promise<void> } | null>(null)

// ── SuperAdmin: whose page ──
const ppe = usePpeApi()
const plainApi = useApi()
const asUser = ppe.asUser
const users = ref<{ id: number; name: string; partCount: number }[]>([])
const userOptions = computed(() => [
  { title: 'My parts', value: null },
  ...users.value.filter(u => u.id !== authStore.user?.id).map(u => ({ title: `${u.name} (${u.partCount})`, value: u.id })),
])
onMounted(async () => {
  if (!isSuperAdmin.value) return
  try {
    users.value = await plainApi.get<{ id: number; name: string; partCount: number }[]>('/ppe/users')
  } catch {
    users.value = []
  }
})
onBeforeUnmount(() => { asUser.value = null })
</script>
