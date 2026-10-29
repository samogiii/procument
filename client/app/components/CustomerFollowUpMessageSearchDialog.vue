<template>
  <v-dialog
    :model-value="open"
    max-width="860"
    scrollable
    @update:model-value="(value) => !value && emit('close')"
  >
    <v-card class="msearch-card">
      <header class="msearch-header">
        <div class="msearch-header-icon">
          <v-icon icon="mdi-text-search" size="22" />
        </div>
        <div class="msearch-header-text">
          <div class="msearch-title">Search messages</div>
          <div class="msearch-subtitle">Searches the text and file names of every row's chat</div>
        </div>
        <button type="button" class="msearch-close" title="Close" @click="emit('close')">
          <v-icon icon="mdi-close" size="20" />
        </button>
      </header>

      <form class="msearch-bar" @submit.prevent="runSearch">
        <v-text-field
          v-model="query"
          prepend-inner-icon="mdi-magnify"
          placeholder="Keyword in any message…"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          autofocus
          maxlength="100"
        />
        <v-btn
          type="submit"
          color="primary"
          size="large"
          prepend-icon="mdi-magnify"
          :loading="searching"
          :disabled="(query ?? '').trim().length < 2"
        >
          Search
        </v-btn>
      </form>

      <v-card-text class="msearch-body">
        <div v-if="!searchedTerm && !searching" class="msearch-empty">
          <v-icon icon="mdi-forum-outline" size="40" class="mb-2" />
          <div>Type a keyword (at least 2 characters) and click Search.</div>
        </div>

        <div v-else-if="searching && !groups.length" class="d-flex justify-center pa-10">
          <v-progress-circular indeterminate color="primary" />
        </div>

        <div v-else-if="!groups.length" class="msearch-empty">
          <v-icon icon="mdi-message-off-outline" size="40" class="mb-2" />
          <div>No message contains “{{ searchedTerm }}”.</div>
        </div>

        <template v-else>
          <div class="msearch-summary">
            {{ total }} message{{ total === 1 ? '' : 's' }} in {{ groups.length }} row{{ groups.length === 1 ? '' : 's' }}
            <span v-if="total > hitCount"> — showing the newest {{ hitCount }}, refine the keyword to see the rest</span>
          </div>

          <section v-for="group in groups" :key="group.rowId" class="msearch-group">
            <div class="msearch-row">
              <span class="msearch-row-id">#{{ group.rowId }}</span>
              <span class="msearch-row-text">
                {{ group.row?.customerCode || 'No customer' }}<span v-if="group.row?.partNumber"> · {{ group.row.partNumber }}</span>
                <span v-if="group.row?.requestedItem" class="text-medium-emphasis"> · {{ group.row.requestedItem }}</span>
              </span>
              <v-chip v-if="group.row?.issueStatus" size="x-small" variant="tonal" class="ml-1">{{ group.row.issueStatus }}</v-chip>
              <v-spacer />
              <span class="msearch-row-count">{{ group.hits.length }} match{{ group.hits.length === 1 ? '' : 'es' }}</span>
            </div>

            <button
              v-for="hit in group.hits"
              :key="hit.message.id"
              type="button"
              class="msearch-hit"
              :style="{ '--c': colorOf(hit.message.sentBy) }"
              :disabled="!group.row"
              :title="group.row ? 'Open this conversation' : 'This row is no longer in the sheet — refresh the page'"
              @click="group.row && emit('open', group.row, hit.message.id)"
            >
              <span class="msearch-avatar">{{ senderInitials(hit.message.sentBy) }}</span>
              <span class="msearch-hit-body">
                <span class="msearch-hit-meta">
                  <strong>{{ hit.message.sentBy }}</strong>
                  <span>{{ formatDateTime(hit.message.sentAt) }}</span>
                </span>
                <span v-if="hit.message.text" class="msearch-hit-text">
                  <template v-for="(part, index) in highlightParts(hit.message.text, searchedTerm, 80)" :key="index">
                    <mark v-if="part.match">{{ part.text }}</mark><template v-else>{{ part.text }}</template>
                  </template>
                </span>
                <span v-if="hit.message.documentName" class="msearch-hit-file">
                  <v-icon icon="mdi-paperclip" size="13" />
                  <template v-for="(part, index) in highlightParts(hit.message.documentName, searchedTerm)" :key="index">
                    <mark v-if="part.match">{{ part.text }}</mark><template v-else>{{ part.text }}</template>
                  </template>
                </span>
              </span>
              <v-icon icon="mdi-chevron-right" size="20" class="msearch-hit-go" />
            </button>
          </section>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { highlightParts, senderColors, senderInitials, type FollowUpRow } from '~/utils/customerFollowUps'

interface SearchMessage {
  id: number
  text: string | null
  sentBy: string
  sentAt: string
  documentName: string | null
}

interface SearchHit {
  rowId: number
  message: SearchMessage
}

/** Search across every row's chat. Clicking a hit asks the page to open that row's chat at the message. */
const props = defineProps<{
  open: boolean
  /** The sheet's rows, to name each hit's row (looked up by id, never rendered as a list). */
  rows: FollowUpRow[]
}>()

const emit = defineEmits<{
  close: []
  open: [row: FollowUpRow, messageId: number]
  notify: [text: string, color: string]
}>()

const api = useApi()

/** Null after the field's clear button. */
const query = ref<string | null>('')
const searchedTerm = ref('')
const searching = ref(false)
const hits = ref<SearchHit[]>([])
const total = ref(0)
const hitCount = computed(() => hits.value.length)
let requestId = 0

function errorMessage(error: any, fallback: string) {
  return error?.data?.message || error?.response?._data?.message || fallback
}

async function runSearch() {
  const term = query.value?.trim() ?? ''
  if (term.length < 2) return
  const current = ++requestId
  searching.value = true
  try {
    const result = await api.get<{ total: number; hits: SearchHit[] }>(`/customer-follow-ups/messages/search?q=${encodeURIComponent(term)}`)
    if (current !== requestId) return
    hits.value = result.hits
    total.value = result.total
    searchedTerm.value = term
  } catch (error) {
    if (current === requestId) emit('notify', errorMessage(error, 'Could not search the messages.'), 'error')
  } finally {
    if (current === requestId) searching.value = false
  }
}

/** Hits grouped by row; rows keep the order of their newest hit. */
const groups = computed(() => {
  const rowById = new Map(props.rows.map(row => [row.id, row]))
  const byRow = new Map<number, { rowId: number; row: FollowUpRow | null; hits: SearchHit[] }>()
  for (const hit of hits.value) {
    let group = byRow.get(hit.rowId)
    if (!group) {
      group = { rowId: hit.rowId, row: rowById.get(hit.rowId) ?? null, hits: [] }
      byRow.set(hit.rowId, group)
    }
    group.hits.push(hit)
  }
  return [...byRow.values()]
})

const colors = computed(() => senderColors(hits.value.map(hit => hit.message.sentBy)))

function colorOf(name: string) {
  return colors.value.get(name.toLowerCase()) ?? '#6366f1'
}

function formatDateTime(value: string) {
  const date = new Date(value)
  return date.toLocaleString(undefined, {
    day: 'numeric',
    month: 'short',
    year: date.getFullYear() === new Date().getFullYear() ? undefined : 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<style scoped>
.msearch-card {
  border-radius: 20px !important;
  overflow: hidden;
}

.msearch-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px 14px 18px;
  color: #fff;
  background:
    radial-gradient(120% 140% at 0% 0%, rgba(255, 255, 255, 0.18), transparent 55%),
    linear-gradient(120deg, #6366f1, #8b5cf6 60%, #d946ef);
}

.msearch-header-icon {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  flex-shrink: 0;
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.18);
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.25);
}

.msearch-header-text { flex: 1; min-width: 0; }
.msearch-title { font-size: 16px; font-weight: 700; }
.msearch-subtitle { font-size: 12px; opacity: 0.85; }

.msearch-close {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border: 0;
  border-radius: 50%;
  color: #fff;
  background: rgba(255, 255, 255, 0.14);
  cursor: pointer;
  transition: background 0.15s, transform 0.15s;
}
.msearch-close:hover { background: rgba(255, 255, 255, 0.28); transform: rotate(90deg); }

.msearch-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.12);
}

.msearch-body {
  max-height: 62vh;
  padding: 12px 16px 16px !important;
}

.msearch-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 40px 16px;
  text-align: center;
  color: rgba(var(--v-theme-on-surface), 0.6);
}

.msearch-summary {
  margin-bottom: 10px;
  font-size: 12px;
  font-weight: 600;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.msearch-group {
  margin-bottom: 12px;
  border-radius: 14px;
  overflow: hidden;
  box-shadow: inset 0 0 0 1px rgba(var(--v-border-color), 0.14);
}

.msearch-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  font-size: 13px;
  background: rgba(var(--v-theme-primary), 0.06);
}
.msearch-row-id { font-weight: 800; color: rgb(var(--v-theme-primary)); }
.msearch-row-text { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.msearch-row-count { flex-shrink: 0; font-size: 11px; color: rgba(var(--v-theme-on-surface), 0.6); }

.msearch-hit {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  width: 100%;
  padding: 10px 12px;
  border: 0;
  border-top: 1px solid rgba(var(--v-border-color), 0.08);
  font: inherit;
  text-align: left;
  color: inherit;
  background: transparent;
  cursor: pointer;
  transition: background 0.12s;
}
.msearch-hit:hover:not(:disabled) { background: color-mix(in srgb, var(--c) 9%, transparent); }
.msearch-hit:hover:not(:disabled) .msearch-hit-go { opacity: 1; transform: translateX(2px); }
.msearch-hit:disabled { cursor: default; opacity: 0.6; }

.msearch-avatar {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  flex-shrink: 0;
  border-radius: 50%;
  font-size: 10px;
  font-weight: 800;
  color: #fff;
  background: linear-gradient(140deg, var(--c), color-mix(in srgb, var(--c) 65%, #000));
}

.msearch-hit-body { display: flex; flex-direction: column; gap: 2px; flex: 1; min-width: 0; }
.msearch-hit-meta { display: flex; gap: 8px; font-size: 12px; }
.msearch-hit-meta strong { color: var(--c); }
.msearch-hit-meta span { color: rgba(var(--v-theme-on-surface), 0.55); }
.msearch-hit-text { font-size: 13px; white-space: pre-wrap; overflow-wrap: anywhere; }
.msearch-hit-file { display: inline-flex; align-items: center; gap: 3px; font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.7); }
.msearch-hit-go { align-self: center; opacity: 0.35; transition: opacity 0.12s, transform 0.12s; }

.msearch-hit mark {
  padding: 0 2px;
  border-radius: 3px;
  color: inherit;
  background: rgba(250, 204, 21, 0.55);
}
</style>
