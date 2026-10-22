<template>
  <v-dialog :model-value="!!row" max-width="860" scrollable @update:model-value="(open) => !open && emit('close')">
    <v-card v-if="row">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-link-variant" color="primary" class="mr-2" />
        Other Refs — row #{{ row.id }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('close')" />
      </v-card-title>

      <!-- Current selection, across every tab -->
      <div class="px-4 pb-2 d-flex flex-wrap align-center gap-1" style="min-height: 36px;">
        <span class="text-body-2 text-medium-emphasis mr-1">Selected ({{ selected.size }}):</span>
        <v-chip
          v-for="ref in selectedList"
          :key="keyOf(ref.type, ref.id)"
          size="small"
          closable
          :color="ref.missing ? 'error' : 'primary'"
          variant="tonal"
          :prepend-icon="FOLLOW_UP_REF_TYPE[ref.type].icon"
          @click:close="selected.delete(keyOf(ref.type, ref.id))"
        >
          {{ followUpRefText(ref) }}
        </v-chip>
        <span v-if="!selected.size" class="text-caption text-medium-emphasis">nothing yet — tick documents below</span>
      </div>

      <v-tabs v-model="tab" color="primary" density="compact" class="px-4">
        <v-tab v-for="meta in FOLLOW_UP_REF_TYPES" :key="meta.type" :value="meta.type" :prepend-icon="meta.icon">
          {{ meta.tab }}
          <v-badge v-if="countByType[meta.type]" :content="countByType[meta.type]" color="primary" inline />
        </v-tab>
      </v-tabs>
      <v-divider />

      <v-card-text class="pa-4" style="height: 480px;">
        <!-- One lazily loaded list per tab: it loads the first time its tab opens -->
        <div v-for="meta in FOLLOW_UP_REF_TYPES" v-show="tab === meta.type" :key="meta.type" class="cfu-pane">
          <v-text-field
            v-model="searches[meta.type]"
            prepend-inner-icon="mdi-magnify"
            :placeholder="`Search ${meta.tab.toLowerCase()}...`"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            class="mb-3 flex-grow-0"
          />
          <div class="cfu-picker-list border rounded" @scroll="onScroll($event, meta.type)">
            <v-list density="compact" class="py-0">
              <v-list-item
                v-for="item in pickers[meta.type].items"
                :key="item.id"
                :active="selected.has(keyOf(meta.type, item.id))"
                color="primary"
                @click="toggle(meta.type, item)"
              >
                <template #prepend>
                  <v-checkbox-btn :model-value="selected.has(keyOf(meta.type, item.id))" density="compact" class="mr-1" @click.stop="toggle(meta.type, item)" />
                </template>
                <v-list-item-title class="font-weight-medium">
                  {{ item.label }}
                  <v-chip v-if="item.status" size="x-small" variant="tonal" class="ml-2">{{ item.status }}</v-chip>
                </v-list-item-title>
                <v-list-item-subtitle>{{ item.detail }}</v-list-item-subtitle>
                <template #append>
                  <span class="text-caption text-medium-emphasis mr-2">{{ new Date(item.createdAt).toLocaleDateString() }}</span>
                  <v-btn icon="mdi-open-in-new" size="x-small" variant="text" :href="router.resolve(meta.path(item.id)).href" target="_blank" title="Open" @click.stop />
                </template>
              </v-list-item>
            </v-list>
            <div v-if="pickers[meta.type].loading" class="d-flex justify-center pa-3"><v-progress-circular indeterminate size="22" width="2" /></div>
            <p v-else-if="pickers[meta.type].error" class="text-caption text-error text-center py-6 mb-0">{{ pickers[meta.type].error }}</p>
            <p v-else-if="!pickers[meta.type].items.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">Nothing found.</p>
            <div v-else-if="pickers[meta.type].hasMore" class="d-flex justify-center pa-2">
              <v-btn size="small" variant="text" @click="pickers[meta.type].loadMore()">Load more</v-btn>
            </div>
          </div>
        </div>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-3">
        <span class="text-caption text-medium-emphasis">Pick from several tabs, then save once.</span>
        <v-spacer />
        <v-btn variant="text" @click="emit('close')">Cancel</v-btn>
        <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :loading="saving" :disabled="!changed" @click="save">Save</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import {
  FOLLOW_UP_REF_TYPE,
  FOLLOW_UP_REF_TYPES,
  followUpRefText,
  type FollowUpRef,
  type FollowUpRefType,
  type FollowUpRow,
} from '~/utils/customerFollowUps'
import { useFollowUpRefPicker, type FollowUpRefOption } from '~/composables/useFollowUpRefPicker'

/** Other Refs of a Customer Follow Ups row: any number of purchase orders, quotes, invoices and final invoices. */
const props = defineProps<{ row: FollowUpRow | null }>()

const emit = defineEmits<{
  close: []
  /** The saved row as the server returned it. */
  saved: [row: any]
  notify: [text: string, color: string]
}>()

const api = useApi()
const router = useRouter()
const tab = ref<FollowUpRefType>('PurchaseOrder')
const saving = ref(false)

const searches = reactive(Object.fromEntries(FOLLOW_UP_REF_TYPES.map(meta => [meta.type, '']))) as Record<FollowUpRefType, string>
const pickers = Object.fromEntries(FOLLOW_UP_REF_TYPES.map(meta =>
  [meta.type, useFollowUpRefPicker(meta.type, toRef(searches, meta.type))])) as Record<FollowUpRefType, ReturnType<typeof useFollowUpRefPicker>>

function keyOf(type: FollowUpRefType, id: number) {
  return `${type}:${id}`
}

/** Picked refs by "type:id", in the order they were picked. */
const selected = reactive(new Map<string, FollowUpRef>())

const selectedList = computed(() => [...selected.values()])

const countByType = computed(() => {
  const counts: Partial<Record<FollowUpRefType, number>> = {}
  for (const ref of selected.values()) counts[ref.type] = (counts[ref.type] ?? 0) + 1
  return counts
})

const changed = computed(() => {
  const saved = props.row?.refs ?? []
  return saved.length !== selected.size || saved.some(ref => !selected.has(keyOf(ref.type, ref.id)))
})

function toggle(type: FollowUpRefType, item: FollowUpRefOption) {
  const key = keyOf(type, item.id)
  if (selected.has(key)) selected.delete(key)
  else selected.set(key, { type, id: item.id, label: item.label, missing: false })
}

function onScroll(event: Event, type: FollowUpRefType) {
  const el = event.target as HTMLElement
  if (el.scrollTop + el.clientHeight >= el.scrollHeight - 60) pickers[type].loadMore()
}

async function save() {
  if (!props.row || saving.value) return
  saving.value = true
  try {
    const saved = await api.put<any>(`/customer-follow-ups/${props.row.id}/refs`, {
      refs: selectedList.value.map(ref => ({ type: ref.type, id: ref.id })),
    })
    emit('saved', saved)
    emit('notify', `Other Refs of row #${props.row.id} saved.`, 'success')
    emit('close')
  } catch (error: any) {
    emit('notify', error?.data?.message || 'Could not save the references.', 'error')
  } finally {
    saving.value = false
  }
}

/** A tab's list loads the first time that tab is shown in this opening. */
function activate(type: FollowUpRefType) {
  if (!pickers[type].active) pickers[type].reset()
}

watch(() => props.row?.id, (id) => {
  if (id === undefined) return
  selected.clear()
  for (const ref of props.row!.refs) selected.set(keyOf(ref.type, ref.id), { ...ref })
  for (const meta of FOLLOW_UP_REF_TYPES) {
    searches[meta.type] = ''
    pickers[meta.type].active = false
    pickers[meta.type].items = []
  }
  tab.value = props.row!.refs[0]?.type ?? 'PurchaseOrder'
  activate(tab.value)
}, { immediate: true })

watch(tab, activate)
</script>

<style scoped>
/* Not d-flex: its display !important would beat v-show and keep hidden tabs visible. */
.cfu-pane {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.cfu-picker-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}
</style>
