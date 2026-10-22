<template>
  <tr :class="{ 'cfu-row-closed': closed, 'cfu-row-new': isNew, 'cfu-row-unread': row.hasUnreadMessages }">
    <td
      v-for="key in columns"
      :key="key"
      :class="['cfu-cell', `cfu-cell-${COLUMN[key].type}`, { 'cfu-editing': editingKey === key, 'cfu-overdue': key === 'deadline' && overdue }]"
      :style="cellStyles[key]"
      :title="key === 'id' ? auditTitle : undefined"
      @click="onCellClick(key)"
    >
      <template v-if="editingKey === key">
        <SheetComboInput
          v-if="COLUMN[key].type === 'combo' || COLUMN[key].type === 'select'"
          :model-value="row[key] as string | null"
          :items="options[key] ?? []"
          :strict="COLUMN[key].type === 'select'"
          @commit="(value, move) => commit(key, value, move)"
          @cancel="emit('cancel')"
        />
        <input
          v-else-if="COLUMN[key].type === 'date'"
          :ref="focusEditor"
          type="date"
          class="sheet-editor-input"
          :value="draft"
          @input="draft = ($event.target as HTMLInputElement).value"
          @change="onDateChange($event, key)"
          @blur="commit(key, ($event.target as HTMLInputElement).value, 0)"
          @keydown="onEditorKeydown($event, key)"
        >
        <textarea
          v-else
          :ref="focusEditor"
          class="sheet-editor-input sheet-editor-textarea"
          :value="draft"
          @input="draft = ($event.target as HTMLTextAreaElement).value"
          rows="3"
          @blur="commit(key, ($event.target as HTMLTextAreaElement).value, 0)"
          @keydown="onEditorKeydown($event, key)"
        />
      </template>
      <template v-else-if="key === 'id'">{{ row.id }}</template>
      <template v-else-if="COLUMN[key].type === 'docs'">
        <span v-if="row.documentCount" class="cfu-docs" title="Open documents">
          <v-icon icon="mdi-paperclip" size="15" />{{ documentCountText(row.documentCount) }}
        </span>
        <v-icon v-else icon="mdi-paperclip" size="14" class="cfu-empty-icon" title="Upload documents" />
      </template>
      <template v-else-if="COLUMN[key].type === 'chat'">
        <span v-if="row.messageCount || row.hasUnreadMessages" class="cfu-chat" :class="{ 'cfu-chat-unread': row.hasUnreadMessages }" :title="row.hasUnreadMessages ? 'New messages — open to read' : 'Open messages'">
          <v-icon icon="mdi-chat-processing-outline" size="15" />{{ row.messageCount }}
          <span v-if="row.hasUnreadMessages" class="cfu-chat-dot" />
        </span>
        <v-icon v-else icon="mdi-chat-plus-outline" size="14" class="cfu-empty-icon" title="Start a conversation" />
      </template>
      <template v-else-if="COLUMN[key].type === 'rfq'">
        <span v-if="row.rfq" class="cfu-rfq">
          <NuxtLink :to="rfqPath(row.rfq.id)" target="_blank" class="cfu-link" :title="`Open RFQ ${row.rfq.name} (${row.rfq.status ?? 'Open'})`" @click.stop>
            <v-icon icon="mdi-file-search-outline" size="14" />{{ row.rfq.name }}
          </NuxtLink>
          <span v-if="row.rfq.status && row.rfq.status !== 'Open'" class="cfu-rfq-status">{{ row.rfq.status }}</span>
        </span>
        <v-icon v-else icon="mdi-link-variant-plus" size="14" class="cfu-empty-icon" :class="{ 'cfu-rfq-wanted': isRfqCategory }" :title="isRfqCategory ? 'Select or create the RFQ' : 'Link an RFQ (optional)'" />
      </template>
      <template v-else-if="COLUMN[key].type === 'refs'">
        <span v-if="row.refs.length" class="cfu-refs">
          <NuxtLink
            v-for="ref in row.refs"
            :key="`${ref.type}:${ref.id}`"
            :to="FOLLOW_UP_REF_TYPE[ref.type].path(ref.id)"
            target="_blank"
            class="cfu-ref-chip"
            :class="{ 'cfu-ref-missing': ref.missing }"
            :title="ref.missing ? 'This document no longer exists' : `Open ${FOLLOW_UP_REF_TYPE[ref.type].tab.replace(/s$/, '')} ${ref.label}`"
            @click.stop
          >{{ followUpRefText(ref) }}</NuxtLink>
        </span>
        <v-icon v-else icon="mdi-link-variant-plus" size="14" class="cfu-empty-icon" title="Add purchase orders, quotes, invoices…" />
      </template>
      <template v-else-if="key === 'procurementStatus' || key === 'salesStatus' || key === 'issueStatus'">
        <span v-if="row[key]" class="cfu-status" :class="`bg-${followUpStatusColor(row[key])}`">{{ row[key] }}</span>
      </template>
      <template v-else-if="COLUMN[key].type === 'date'">
        <span v-if="row[key]">{{ row[key] }}</span>
        <v-icon v-else icon="mdi-calendar-blank-outline" size="14" class="cfu-empty-icon" />
      </template>
      <template v-else>{{ row[key] }}</template>
    </td>
    <td class="cfu-actions">
      <button type="button" class="cfu-delete-btn" title="Delete row" @click.stop="emit('delete')">
        <v-icon icon="mdi-delete-outline" size="16" />
      </button>
    </td>
  </tr>
</template>

<script setup lang="ts">
import type { ComponentPublicInstance } from 'vue'
import {
  FOLLOW_UP_COLUMN_BY_KEY as COLUMN,
  FOLLOW_UP_REF_TYPE,
  documentCountText,
  followUpRefText,
  followUpStatusColor,
  rfqPath,
  isClosedFollowUp,
  type EditableFollowUpKey,
  type FollowUpKey,
  type FollowUpRow,
} from '~/utils/customerFollowUps'

/**
 * One Customer Follow Ups row. Cells show plain text; only the cell being edited mounts an
 * editor, and only the row whose `editingKey` changes re-renders.
 */
const props = defineProps<{
  row: FollowUpRow
  /** Visible column keys in the user's order (the same array for every row). */
  columns: FollowUpKey[]
  /** The column being edited in this row, or null. */
  editingKey: EditableFollowUpKey | null
  /** Dropdown items per combo column (the same object for every row). */
  options: Partial<Record<FollowUpKey, string[]>>
  /** Width styles per column (the same object for every row). */
  cellStyles: Record<string, Record<string, string>>
  /** yyyy-MM-dd — rows with a past deadline that are not closed are flagged. */
  today: string
  isNew?: boolean
}>()

const emit = defineEmits<{
  edit: [key: FollowUpKey]
  /** move: 0 = stay, 1 = next column (Tab), -1 = previous column (Shift+Tab). */
  commit: [key: EditableFollowUpKey, value: string, move: 0 | 1 | -1]
  cancel: []
  delete: []
  /** The Documents cell was clicked. */
  documents: []
  /** The Messages cell was clicked. */
  messages: []
  /** The RFQ Ref# cell was clicked (not its link). */
  rfq: []
  /** The Other Refs cell was clicked (not one of its links). */
  refs: []
}>()

/** Editors mount only in the cell whose key equals `editingKey`, which is always an editable column. */
function commit(key: FollowUpKey, value: string, move: 0 | 1 | -1) {
  emit('commit', key as EditableFollowUpKey, value, move)
}

function onCellClick(key: FollowUpKey) {
  if (key === 'id' || props.editingKey === key) return
  const type = COLUMN[key].type
  if (type === 'docs') emit('documents')
  else if (type === 'chat') emit('messages')
  else if (type === 'rfq') emit('rfq')
  else if (type === 'refs') emit('refs')
  else emit('edit', key)
}

const isRfqCategory = computed(() => (props.row.category ?? '').trim().toLowerCase() === 'rfq')

/**
 * The text being typed into a text / date editor. Kept here rather than bound to `row[key]`,
 * so a save of another cell of this row landing mid-edit does not reset what the user typed.
 */
const draft = ref('')
watch(() => props.editingKey, (key) => {
  draft.value = key ? (props.row[key] as string | null) ?? '' : ''
}, { immediate: true })

const closed = computed(() => isClosedFollowUp(props.row))
const overdue = computed(() => !!props.row.deadline && props.row.deadline < props.today && !closed.value)

const auditTitle = computed(() => {
  const parts = [`Created by ${props.row.createdBy ?? '—'} on ${new Date(props.row.createdAt).toLocaleString()}`]
  if (props.row.modifiedBy && props.row.modifyAt) {
    parts.push(`Last edit by ${props.row.modifiedBy} on ${new Date(props.row.modifyAt).toLocaleString()}`)
  }
  return parts.join('\n')
})

/**
 * Typing a date fires `change` on every keystroke that forms a valid date (year 0002, 0020…),
 * so only a pick from the calendar commits at once; typed dates commit on Enter, Tab or leaving the cell.
 */
let typingDate = false

function onDateChange(event: Event, key: FollowUpKey) {
  if (typingDate) return
  commit(key, (event.target as HTMLInputElement).value, 0)
}

/** Focuses a freshly mounted editor; a date editor also opens its calendar. */
function focusEditor(el: Element | ComponentPublicInstance | null) {
  if (!(el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement)) return
  if (document.activeElement === el) return
  typingDate = false
  el.focus()
  if (el instanceof HTMLInputElement && el.type === 'date') {
    try { el.showPicker() } catch {}
  } else {
    el.setSelectionRange(el.value.length, el.value.length)
  }
}

function onEditorKeydown(event: KeyboardEvent, key: FollowUpKey) {
  const target = event.target as HTMLInputElement | HTMLTextAreaElement
  if (event.key === 'Escape') {
    event.preventDefault()
    emit('cancel')
  } else if (event.key === 'Tab') {
    event.preventDefault()
    commit(key, target.value, event.shiftKey ? -1 : 1)
  } else if (event.key === 'Enter' && !event.shiftKey) {
    // Shift+Enter adds a line break in text cells.
    event.preventDefault()
    commit(key, target.value, 0)
  } else {
    typingDate = true
  }
}
</script>
