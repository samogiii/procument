<template>
  <v-dialog
    :model-value="!!row"
    max-width="780"
    content-class="chat-dialog"
    @update:model-value="(open) => !open && emit('close')"
  >
    <div
      v-if="row"
      class="chat-shell"
      @dragenter.prevent="onDragEnter"
      @dragover.prevent
      @dragleave.prevent="onDragLeave"
      @drop.prevent="onDrop"
    >
      <!-- Header -->
      <header class="chat-header">
        <div class="chat-header-icon">
          <v-icon icon="mdi-forum-outline" size="22" />
        </div>
        <div class="chat-header-text">
          <div class="chat-title">Messages · Row #{{ row.id }}</div>
          <div class="chat-subtitle">
            {{ row.customerCode || 'No customer' }}<span v-if="row.partNumber"> · {{ row.partNumber }}</span>
            <span v-if="row.requestedItem"> · {{ row.requestedItem }}</span>
          </div>
        </div>
        <div v-if="participants.length" class="chat-participants">
          <span
            v-for="name in participants.slice(0, 5)"
            :key="name"
            class="chat-avatar chat-avatar-sm"
            :style="{ '--c': colorOf(name) }"
            :title="name"
          >{{ senderInitials(name) }}</span>
          <span v-if="participants.length > 5" class="chat-avatar chat-avatar-sm chat-avatar-more">+{{ participants.length - 5 }}</span>
        </div>
        <button
          type="button"
          class="chat-unread-btn"
          title="Mark as unread — the row stays highlighted for you until you open it again"
          :disabled="markingUnread"
          @click="markUnread"
        >
          <v-icon icon="mdi-message-badge-outline" size="18" />
          <span>Mark unread</span>
        </button>
        <button type="button" class="chat-close" title="Close" @click="emit('close')">
          <v-icon icon="mdi-close" size="20" />
        </button>
      </header>

      <!-- Messages -->
      <div ref="scroller" class="chat-body" @scroll.passive="onScroll">
        <div v-if="loading && !messages.length" class="chat-skeleton">
          <div v-for="i in 4" :key="i" class="chat-skeleton-bubble" :class="{ 'is-mine': i % 2 === 0 }" />
        </div>

        <div v-else-if="!messages.length" class="chat-empty">
          <div class="chat-empty-icon"><v-icon icon="mdi-chat-outline" size="34" /></div>
          <div class="chat-empty-title">No messages yet</div>
          <div class="chat-empty-text">
            Start the conversation about this follow up. Photos and PDFs you send here are saved in the Documents column too.
          </div>
        </div>

        <TransitionGroup v-else name="chat-msg" tag="div" class="chat-list">
          <div
            v-for="item in timeline"
            :key="item.key"
            :class="itemClass(item)"
            :style="item.type === 'msg' ? { '--c': item.color } : undefined"
          >
            <span v-if="item.type === 'day'">{{ item.label }}</span>
            <template v-else>
              <span v-if="item.last" class="chat-avatar" :title="item.message.sentBy">{{ senderInitials(item.message.sentBy) }}</span>
              <span v-else class="chat-avatar-gap" />

              <div class="chat-bubble">
                <div v-if="item.first" class="chat-sender">{{ item.message.sentBy }}</div>

                <div v-if="item.message.text" class="chat-text">{{ item.message.text }}</div>

                <!-- Photo: the picture itself, opened full size on click -->
                <div
                  v-if="photoSrc(item.message)"
                  class="chat-photo"
                  :class="{ 'is-uploading': item.message.pending }"
                >
                  <button
                    type="button"
                    class="chat-photo-open"
                    :title="item.message.document?.originalFileName ?? item.message.documentName ?? ''"
                    :disabled="!item.message.document"
                    @click="item.message.document && preview(item.message.document)"
                  >
                    <img :src="photoSrc(item.message)!" :alt="item.message.documentName ?? 'Photo'" draggable="false">
                  </button>
                  <span v-if="item.message.pending" class="chat-photo-progress">
                    <v-progress-circular indeterminate size="28" width="3" color="white" />
                  </span>
                  <button
                    v-else-if="item.message.document"
                    type="button"
                    class="chat-photo-download"
                    title="Download"
                    @click="download(item.message.document)"
                  >
                    <v-icon icon="mdi-download" size="17" />
                  </button>
                </div>
                <div v-else-if="item.message.document && isImageMime(item.message.document.mimeType)" class="chat-photo chat-photo-placeholder">
                  <v-progress-circular indeterminate size="24" width="2" />
                </div>

                <!-- PDF card -->
                <button
                  v-else-if="item.message.document"
                  type="button"
                  class="chat-file"
                  title="Open PDF"
                  @click="preview(item.message.document)"
                >
                  <span class="chat-file-icon"><v-icon icon="mdi-file-pdf-box" size="22" /></span>
                  <span class="chat-file-info">
                    <span class="chat-file-name">{{ item.message.document.originalFileName }}</span>
                    <span class="chat-file-meta">PDF · {{ formatFileSize(item.message.document.fileSizeBytes) }}</span>
                  </span>
                  <span class="chat-file-action" title="Download" @click.stop="download(item.message.document)">
                    <v-icon icon="mdi-download" size="18" />
                  </span>
                </button>
                <div v-else-if="item.message.documentName" class="chat-file chat-file-removed">
                  <span class="chat-file-icon"><v-icon :icon="item.message.pending ? 'mdi-upload' : 'mdi-file-remove-outline'" size="20" /></span>
                  <span class="chat-file-info">
                    <span class="chat-file-name">{{ item.message.documentName }}</span>
                    <span class="chat-file-meta">{{ item.message.pending ? 'Uploading…' : 'Removed from Documents' }}</span>
                  </span>
                </div>

                <div class="chat-time">
                  <v-icon v-if="item.message.pending" icon="mdi-clock-outline" size="11" />
                  <v-icon v-else-if="item.mine" icon="mdi-check" size="12" />
                  {{ formatTime(item.message.sentAt) }}
                </div>
              </div>

              <v-menu v-if="item.mine && !item.message.pending" location="top" :close-on-content-click="false">
                <template #activator="{ props: menuProps }">
                  <button v-bind="menuProps" type="button" class="chat-msg-delete" title="Delete message">
                    <v-icon icon="mdi-trash-can-outline" size="16" />
                  </button>
                </template>
                <template #default="{ isActive }">
                  <div class="chat-confirm">
                    <div class="chat-confirm-title">Delete this message?</div>
                    <div v-if="item.message.document" class="chat-confirm-text">Its file is removed from Documents too.</div>
                    <div class="chat-confirm-actions">
                      <v-btn size="small" variant="text" @click="isActive.value = false">Cancel</v-btn>
                      <v-btn
                        size="small"
                        color="error"
                        variant="flat"
                        :loading="deletingId === item.message.id"
                        @click="deleteMessage(item.message, () => (isActive.value = false))"
                      >
                        Delete
                      </v-btn>
                    </div>
                  </div>
                </template>
              </v-menu>
            </template>
          </div>
        </TransitionGroup>
      </div>

      <Transition name="chat-fade">
        <button v-if="unseen > 0" type="button" class="chat-new-pill" @click="scrollToBottom(true)">
          <v-icon icon="mdi-arrow-down" size="16" /> {{ unseen }} new message{{ unseen === 1 ? '' : 's' }}
        </button>
      </Transition>

      <!-- Composer -->
      <footer class="chat-composer">
        <div v-if="attachments.length" class="chat-attachments">
          <span
            v-for="(attachment, index) in attachments"
            :key="`${attachment.file.name}-${index}`"
            class="chat-attachment"
            :class="{ 'is-photo': attachment.url }"
          >
            <img v-if="attachment.url" :src="attachment.url" alt="" class="chat-attachment-thumb">
            <v-icon v-else icon="mdi-file-pdf-box" size="16" color="error" />
            <span class="chat-attachment-name">{{ attachment.file.name }}</span>
            <span class="chat-attachment-size">{{ formatFileSize(attachment.file.size) }}</span>
            <button type="button" title="Remove" @click="removeAttachment(index)">
              <v-icon icon="mdi-close" size="14" />
            </button>
          </span>
        </div>
        <div class="chat-input-row">
          <button type="button" class="chat-icon-btn" title="Attach photos or PDFs" @click="fileInput?.click()">
            <v-icon icon="mdi-paperclip" size="20" />
          </button>
          <div class="chat-input-wrap" :style="{ '--c': myColor }">
            <textarea
              ref="input"
              v-model="draft"
              class="chat-input"
              rows="1"
              placeholder="Write a message… (paste a screenshot to attach it)"
              maxlength="4000"
              @input="autoGrow"
              @keydown.enter.exact="onEnter"
              @paste="onPaste"
            />
          </div>
          <button type="button" class="chat-send" :disabled="!canSend" title="Send" @click="send">
            <v-icon icon="mdi-send" size="19" />
          </button>
        </div>
        <div class="chat-hint">Enter to send · Shift+Enter for a new line · photos and PDFs are saved to Documents</div>
        <input ref="fileInput" type="file" :accept="FOLLOW_UP_FILE_ACCEPT" multiple class="d-none" @change="onPick">
      </footer>

      <Transition name="chat-fade">
        <div v-if="dragDepth > 0" class="chat-drop">
          <v-icon icon="mdi-tray-arrow-down" size="40" />
          <div>Drop photos or PDFs to attach</div>
        </div>
      </Transition>
    </div>
  </v-dialog>

  <DocPreviewModal
    :open="!!previewUrl"
    :blob-url="previewUrl"
    :file-name="previewName"
    :mime-type="previewMime"
    @close="closePreview"
  />
</template>

<script setup lang="ts">
import {
  FOLLOW_UP_FILE_ACCEPT,
  isAllowedFollowUpFile,
  isImageMime,
  senderColors,
  senderInitials,
  type FollowUpRow,
} from '~/utils/customerFollowUps'

interface ChatDocument {
  id: number
  originalFileName: string
  mimeType: string
  fileSizeBytes: number
}

interface ChatMessage {
  id: number
  text: string | null
  sentBy: string
  sentAt: string
  document: ChatDocument | null
  documentName: string | null
  /** Shown at once while the send request runs. */
  pending?: boolean
  /** A pending photo's local preview. */
  localUrl?: string | null
}

/** A file waiting in the composer; photos carry a local preview URL. */
interface Attachment {
  file: File
  url: string | null
}

type TimelineItem =
  | { type: 'day'; key: string; label: string }
  | { type: 'msg'; key: string; message: ChatMessage; mine: boolean; color: string; first: boolean; last: boolean }

/** The chat of one Customer Follow Ups row. Open while `row` is set. */
const props = defineProps<{ row: FollowUpRow | null }>()

const emit = defineEmits<{
  close: []
  'message-count': [rowId: number, count: number]
  /** Files sent (positive) or removed with a deleted message (negative). */
  'documents-added': [rowId: number, delta: number]
  /** The chat became read (opened) or unread ("Mark as unread") for the current user. */
  'unread-change': [rowId: number, unread: boolean]
  notify: [text: string, color: string]
}>()

const POLL_MS = 5000
/** Messages of one sender closer than this are grouped under one name / avatar. */
const GROUP_GAP_MS = 5 * 60 * 1000

const api = useApi()
const auth = useAuthStore()
const myName = computed(() => auth.user?.name ?? '')

const messages = ref<ChatMessage[]>([])
const loading = ref(false)
const sending = ref(false)
const deletingId = ref<number | null>(null)
const draft = ref('')
const attachments = ref<Attachment[]>([])
/** Object URLs of photos shown in the chat, by document id. */
const photos = ref<Record<number, string>>({})
const photosLoading = new Set<number>()
const dragDepth = ref(0)
const unseen = ref(0)
const scroller = ref<HTMLElement | null>(null)
const input = ref<HTMLTextAreaElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
let pollTimer: ReturnType<typeof setInterval> | null = null
let tempId = -1

function errorMessage(error: any, fallback: string) {
  return error?.data?.message || error?.response?._data?.message || fallback
}

function basePath(rowId: number) {
  return `/customer-follow-ups/${rowId}/messages`
}

function isMine(message: ChatMessage) {
  return message.sentBy.toLowerCase() === myName.value.toLowerCase()
}

// ── Colors & timeline ────────────────────────────────────────────────────────

const participants = computed(() => {
  const seen = new Map<string, string>()
  for (const message of messages.value) {
    if (!seen.has(message.sentBy.toLowerCase())) seen.set(message.sentBy.toLowerCase(), message.sentBy)
  }
  return [...seen.values()]
})

const colors = computed(() => senderColors([...participants.value, myName.value].filter(Boolean)))

function colorOf(name: string) {
  return colors.value.get(name.toLowerCase()) ?? '#6366f1'
}

const myColor = computed(() => colorOf(myName.value))

function dayKey(date: Date) {
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`
}

function dayLabel(date: Date) {
  const today = new Date()
  const yesterday = new Date(today)
  yesterday.setDate(today.getDate() - 1)
  if (dayKey(date) === dayKey(today)) return 'Today'
  if (dayKey(date) === dayKey(yesterday)) return 'Yesterday'
  return date.toLocaleDateString(undefined, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: date.getFullYear() === today.getFullYear() ? undefined : 'numeric',
  })
}

/** Messages with day separators; consecutive messages of one sender form a group. */
const timeline = computed<TimelineItem[]>(() => {
  const items: TimelineItem[] = []
  const list = messages.value
  list.forEach((message, index) => {
    const date = new Date(message.sentAt)
    const previous = list[index - 1]
    const next = list[index + 1]
    const newDay = !previous || dayKey(new Date(previous.sentAt)) !== dayKey(date)
    if (newDay) items.push({ type: 'day', key: `day-${dayKey(date)}`, label: dayLabel(date) })
    const joins = (other: ChatMessage | undefined) => !!other
      && other.sentBy.toLowerCase() === message.sentBy.toLowerCase()
      && Math.abs(new Date(other.sentAt).getTime() - date.getTime()) < GROUP_GAP_MS
      && dayKey(new Date(other.sentAt)) === dayKey(date)
    items.push({
      type: 'msg',
      key: `msg-${message.id}`,
      message,
      mine: isMine(message),
      color: colorOf(message.sentBy),
      first: newDay || !joins(previous),
      last: !joins(next),
    })
  })
  return items
})

function itemClass(item: TimelineItem) {
  if (item.type === 'day') return 'chat-day'
  return ['chat-msg', { 'is-mine': item.mine, 'is-first': item.first, 'is-last': item.last, 'is-pending': item.message.pending }]
}

// ── Loading & polling ────────────────────────────────────────────────────────

/** A cheap fingerprint, so a poll that brings nothing new does not re-render the list. */
function signature(list: ChatMessage[]) {
  return list.map(m => `${m.id}:${m.document?.id ?? ''}`).join(',')
}

async function fetchMessages(rowId: number, initial: boolean) {
  if (initial) loading.value = true
  try {
    const list = await api.get<ChatMessage[]>(basePath(rowId))
    // Ignore a reply for a row that is no longer open, or one that raced a send / delete.
    if (props.row?.id !== rowId || (!initial && (sending.value || deletingId.value !== null))) return
    const confirmed = messages.value.filter(m => !m.pending)
    if (!initial && signature(list) === signature(confirmed)) return

    const knownIds = new Set(confirmed.map(m => m.id))
    const arrived = list.filter(m => !knownIds.has(m.id)).length
    const nearBottom = isNearBottom()
    messages.value = [...list, ...messages.value.filter(m => m.pending)]
    emit('message-count', rowId, list.length)
    // Everything on screen counts as read: on opening, and whenever someone else's message arrives.
    if (initial || list.some(m => !knownIds.has(m.id) && !isMine(m))) markRead(rowId)

    await nextTick()
    if (initial || nearBottom) scrollToBottom(!initial)
    else if (arrived > 0) unseen.value += arrived
  } catch (error) {
    if (initial) emit('notify', errorMessage(error, 'Could not load messages.'), 'error')
  } finally {
    if (initial) loading.value = false
  }
}

// ── Read / unread (per user) ─────────────────────────────────────────────────

/** The last read request — "Mark unread" waits for it, so the server never applies them out of order. */
let readRequest: Promise<unknown> = Promise.resolve()

async function markRead(rowId: number) {
  try {
    readRequest = api.put(`${basePath(rowId)}/read`, {})
    await readRequest
    if (props.row?.id === rowId) emit('unread-change', rowId, false)
  } catch {
    // Not fatal — the row just stays highlighted until the next open.
  }
}

const markingUnread = ref(false)

/** Flags the chat unread for this user only, and closes it so the row shows the unread color. */
async function markUnread() {
  const rowId = props.row?.id
  if (!rowId) return
  markingUnread.value = true
  try {
    await readRequest.catch(() => {})
    await api.put(`${basePath(rowId)}/unread`, {})
    stopPolling()
    emit('unread-change', rowId, true)
    emit('close')
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not mark the chat as unread.'), 'error')
  } finally {
    markingUnread.value = false
  }
}

function stopPolling() {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = null
}

watch(() => props.row?.id, (rowId) => {
  stopPolling()
  releaseFiles()
  messages.value = []
  draft.value = ''
  attachments.value = []
  unseen.value = 0
  dragDepth.value = 0
  if (!rowId) return
  fetchMessages(rowId, true)
  pollTimer = setInterval(() => {
    if (document.visibilityState === 'visible') fetchMessages(rowId, false)
  }, POLL_MS)
  nextTick(() => input.value?.focus())
}, { immediate: true })

onBeforeUnmount(() => {
  stopPolling()
  releaseFiles()
  closePreview()
})

// ── Scrolling ────────────────────────────────────────────────────────────────

function isNearBottom() {
  const el = scroller.value
  return !el || el.scrollHeight - el.scrollTop - el.clientHeight < 120
}

function scrollToBottom(smooth = false) {
  unseen.value = 0
  const el = scroller.value
  if (el) el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' })
}

function onScroll() {
  if (unseen.value && isNearBottom()) unseen.value = 0
}

// ── Composer ─────────────────────────────────────────────────────────────────

const canSend = computed(() => !sending.value && (!!draft.value.trim() || attachments.value.length > 0))

function autoGrow() {
  const el = input.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = `${Math.min(el.scrollHeight, 140)}px`
}

function onEnter(event: KeyboardEvent) {
  if (event.isComposing) return
  event.preventDefault()
  send()
}

function addFiles(files: File[]) {
  const allowed = files.filter(isAllowedFollowUpFile)
  if (allowed.length < files.length) emit('notify', 'Only photos (JPG, PNG, GIF, WEBP, BMP) and PDF files can be sent.', 'warning')
  attachments.value = [
    ...attachments.value,
    ...allowed.map(file => ({ file, url: file.type.startsWith('image/') ? URL.createObjectURL(file) : null })),
  ]
  input.value?.focus()
}

function removeAttachment(index: number) {
  const [removed] = attachments.value.splice(index, 1)
  if (removed?.url) URL.revokeObjectURL(removed.url)
}

/** A screenshot pasted into the message box is attached; pasted text is left alone. */
function onPaste(event: ClipboardEvent) {
  const files = [...(event.clipboardData?.files ?? [])]
  if (!files.length) return
  event.preventDefault()
  addFiles(files)
}

function onPick(event: Event) {
  const target = event.target as HTMLInputElement
  addFiles([...(target.files ?? [])])
  target.value = ''
}

function onDragEnter(event: DragEvent) {
  if (event.dataTransfer?.types.includes('Files')) dragDepth.value++
}

function onDragLeave() {
  dragDepth.value = Math.max(0, dragDepth.value - 1)
}

function onDrop(event: DragEvent) {
  dragDepth.value = 0
  addFiles([...(event.dataTransfer?.files ?? [])])
}

async function send() {
  const rowId = props.row?.id
  if (!rowId || !canSend.value) return
  const text = draft.value.trim()
  const files = attachments.value

  // Show the message at once, the same way the server splits it (first file rides on the text).
  const now = new Date().toISOString()
  const pending: ChatMessage[] = files.length
    ? files.map((attachment, index) => ({
        id: tempId--,
        text: index === 0 ? text || null : null,
        sentBy: myName.value,
        sentAt: now,
        document: null,
        documentName: attachment.file.name,
        localUrl: attachment.url,
        pending: true,
      }))
    : [{ id: tempId--, text, sentBy: myName.value, sentAt: now, document: null, documentName: null, pending: true }]
  const pendingIds = new Set(pending.map(m => m.id))
  messages.value = [...messages.value, ...pending]
  draft.value = ''
  attachments.value = []
  nextTick(() => {
    autoGrow()
    scrollToBottom(true)
  })

  const data = new FormData()
  if (text) data.append('text', text)
  files.forEach(attachment => data.append('files', attachment.file))
  sending.value = true
  try {
    const sent = await api.post<ChatMessage[]>(basePath(rowId), data)
    if (props.row?.id !== rowId) return
    // The server answers in file order: each photo keeps its local preview, so nothing is downloaded again.
    if (files.length) {
      sent.forEach((message, index) => {
        const url = files[index]?.url
        if (url && message.document) photos.value = { ...photos.value, [message.document.id]: url }
      })
    }
    messages.value = [...messages.value.filter(m => !pendingIds.has(m.id)), ...sent]
    emit('message-count', rowId, messages.value.filter(m => !m.pending).length)
    if (files.length) emit('documents-added', rowId, files.length)
  } catch (error) {
    if (props.row?.id !== rowId) return
    messages.value = messages.value.filter(m => !pendingIds.has(m.id))
    // Give the user their message back to retry.
    if (!draft.value) draft.value = text
    attachments.value = [...files, ...attachments.value]
    nextTick(autoGrow)
    emit('notify', errorMessage(error, 'Could not send the message.'), 'error')
  } finally {
    sending.value = false
  }
}

async function deleteMessage(message: ChatMessage, done: () => void) {
  const rowId = props.row?.id
  if (!rowId) return
  deletingId.value = message.id
  try {
    await api.delete(`${basePath(rowId)}/${message.id}`)
    messages.value = messages.value.filter(m => m.id !== message.id)
    const photo = message.document ? photos.value[message.document.id] : undefined
    if (photo) URL.revokeObjectURL(photo)
    emit('message-count', rowId, messages.value.filter(m => !m.pending).length)
    if (message.document) emit('documents-added', rowId, -1)
    done()
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not delete the message.'), 'error')
  } finally {
    deletingId.value = null
  }
}

// ── Files ────────────────────────────────────────────────────────────────────

const previewUrl = ref<string | null>(null)
const previewName = ref('')
const previewMime = ref('application/pdf')

function photoSrc(message: ChatMessage): string | null {
  if (message.localUrl) return message.localUrl
  return message.document ? photos.value[message.document.id] ?? null : null
}

/** Photos are fetched with the user's token (an <img src> to the API could not send it). */
watch(messages, (list) => {
  const rowId = props.row?.id
  if (!rowId) return
  for (const message of list) {
    const document = message.document
    if (!document || !isImageMime(document.mimeType) || photos.value[document.id] || photosLoading.has(document.id)) continue
    photosLoading.add(document.id)
    fetchBlob(rowId, document, false).then((blob) => {
      photosLoading.delete(document.id)
      if (!blob || props.row?.id !== rowId) return
      photos.value = { ...photos.value, [document.id]: URL.createObjectURL(blob) }
    })
  }
})

/** Frees every local preview — on close and when another row's chat opens. */
function releaseFiles() {
  Object.values(photos.value).forEach(url => URL.revokeObjectURL(url))
  photos.value = {}
  photosLoading.clear()
  attachments.value.forEach(attachment => attachment.url && URL.revokeObjectURL(attachment.url))
  messages.value.forEach(message => message.localUrl && URL.revokeObjectURL(message.localUrl))
}

async function fetchBlob(rowId: number, document: ChatDocument, notify = true) {
  try {
    return await api.get<Blob>(`/customer-follow-ups/${rowId}/documents/${document.id}/download`, { responseType: 'blob' })
  } catch {
    if (notify) emit('notify', 'This file could not be opened — it may have been deleted.', 'error')
    return null
  }
}

async function preview(document: ChatDocument) {
  const rowId = props.row?.id
  if (!rowId) return
  const blob = await fetchBlob(rowId, document)
  if (!blob) return
  closePreview()
  previewName.value = document.originalFileName
  previewMime.value = document.mimeType
  previewUrl.value = URL.createObjectURL(new Blob([blob], { type: document.mimeType }))
}

function closePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = null
}

async function download(document: ChatDocument) {
  const rowId = props.row?.id
  if (!rowId) return
  const blob = await fetchBlob(rowId, document)
  if (!blob) return
  const url = URL.createObjectURL(blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = document.originalFileName
  link.click()
  URL.revokeObjectURL(url)
}

// ── Formatting ───────────────────────────────────────────────────────────────

function formatTime(value: string) {
  return new Date(value).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
}

function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} KB`
    : `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
</script>

<style scoped>
.chat-shell {
  --chat-accent: #6366f1;
  --chat-accent-2: #8b5cf6;
  position: relative;
  display: flex;
  flex-direction: column;
  height: min(82vh, 760px);
  border-radius: 22px;
  overflow: hidden;
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  box-shadow: 0 30px 80px -20px rgba(15, 23, 42, 0.45);
}

/* Header */
.chat-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px 14px 18px;
  color: #fff;
  background:
    radial-gradient(120% 140% at 0% 0%, rgba(255, 255, 255, 0.18), transparent 55%),
    linear-gradient(120deg, var(--chat-accent), var(--chat-accent-2) 60%, #d946ef);
}

.chat-header-icon {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  flex-shrink: 0;
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.18);
  backdrop-filter: blur(6px);
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.25);
}

.chat-header-text { min-width: 0; flex: 1; }
.chat-title { font-size: 16px; font-weight: 700; letter-spacing: 0.2px; }
.chat-subtitle {
  font-size: 12px;
  opacity: 0.85;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.chat-participants { display: flex; padding-left: 8px; }
.chat-participants .chat-avatar { margin-left: -8px; box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.9); }

.chat-unread-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 34px;
  flex-shrink: 0;
  padding: 0 12px;
  border: 0;
  border-radius: 999px;
  font: inherit;
  font-size: 12px;
  font-weight: 700;
  color: #fff;
  background: rgba(255, 255, 255, 0.16);
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.28);
  cursor: pointer;
  transition: background 0.15s, transform 0.15s;
}
.chat-unread-btn:hover:not(:disabled) { background: rgba(255, 255, 255, 0.3); transform: translateY(-1px); }
.chat-unread-btn:disabled { opacity: 0.6; cursor: default; }

@media (max-width: 600px) {
  .chat-unread-btn span { display: none; }
  .chat-unread-btn { padding: 0 9px; }
}

.chat-close {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  flex-shrink: 0;
  border: 0;
  border-radius: 50%;
  color: #fff;
  background: rgba(255, 255, 255, 0.14);
  cursor: pointer;
  transition: background 0.15s, transform 0.15s;
}
.chat-close:hover { background: rgba(255, 255, 255, 0.28); transform: rotate(90deg); }

/* Avatars */
.chat-avatar {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.3px;
  color: #fff;
  background: linear-gradient(140deg, var(--c), color-mix(in srgb, var(--c) 65%, #000));
  box-shadow: 0 4px 10px -3px color-mix(in srgb, var(--c) 70%, transparent);
  user-select: none;
}
.chat-avatar-sm { width: 28px; height: 28px; font-size: 10px; }
.chat-avatar-more { --c: #64748b; }
.chat-avatar-gap { width: 32px; flex-shrink: 0; }

/* Body */
.chat-body {
  flex: 1;
  overflow-y: auto;
  padding: 18px 18px 10px;
  background-color: rgb(var(--v-theme-background));
  background-image: radial-gradient(rgba(var(--v-theme-on-surface), 0.06) 1px, transparent 1px);
  background-size: 18px 18px;
  scrollbar-width: thin;
}

.chat-list { display: flex; flex-direction: column; }

.chat-day {
  display: flex;
  justify-content: center;
  margin: 14px 0 10px;
  position: sticky;
  top: -10px;
  z-index: 1;
}
.chat-day span {
  padding: 3px 12px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
  color: rgba(var(--v-theme-on-surface), 0.7);
  background: rgba(var(--v-theme-surface), 0.85);
  backdrop-filter: blur(8px);
  box-shadow: 0 1px 4px rgba(15, 23, 42, 0.08), inset 0 0 0 1px rgba(var(--v-border-color), 0.12);
}

/* Messages */
.chat-msg {
  display: flex;
  align-items: flex-end;
  gap: 8px;
  margin-top: 2px;
}
.chat-msg.is-first { margin-top: 10px; }
.chat-msg.is-mine { flex-direction: row-reverse; }
.chat-msg.is-pending { opacity: 0.6; }

.chat-bubble {
  position: relative;
  max-width: min(72%, 520px);
  padding: 8px 12px 6px;
  border-radius: 18px;
  font-size: 13.5px;
  line-height: 1.45;
  background: color-mix(in srgb, var(--c) 12%, rgb(var(--v-theme-surface)));
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.06), inset 0 0 0 1px color-mix(in srgb, var(--c) 22%, transparent);
}
.chat-msg:not(.is-mine) .chat-bubble { border-bottom-left-radius: 6px; border-top-left-radius: 6px; }
.chat-msg:not(.is-mine).is-first .chat-bubble { border-top-left-radius: 18px; }
.chat-msg:not(.is-mine).is-last .chat-bubble { border-bottom-left-radius: 4px; }

.chat-msg.is-mine .chat-bubble {
  color: #fff;
  background: linear-gradient(135deg, var(--c), color-mix(in srgb, var(--c) 72%, #000));
  box-shadow: 0 6px 16px -8px color-mix(in srgb, var(--c) 80%, transparent);
  border-bottom-right-radius: 6px;
  border-top-right-radius: 6px;
}
.chat-msg.is-mine.is-first .chat-bubble { border-top-right-radius: 18px; }
.chat-msg.is-mine.is-last .chat-bubble { border-bottom-right-radius: 4px; }

.chat-sender {
  margin-bottom: 2px;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.2px;
  color: var(--c);
}
.chat-msg.is-mine .chat-sender { color: rgba(255, 255, 255, 0.92); }

.chat-text { white-space: pre-wrap; overflow-wrap: anywhere; }

.chat-time {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 3px;
  margin-top: 2px;
  font-size: 10.5px;
  opacity: 0.6;
}

/* PDF card inside a bubble */
.chat-file {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-width: 220px;
  margin: 4px 0 2px;
  padding: 8px 10px;
  border: 0;
  border-radius: 12px;
  text-align: left;
  color: inherit;
  font: inherit;
  cursor: pointer;
  background: rgba(var(--v-theme-surface), 0.75);
  box-shadow: inset 0 0 0 1px rgba(var(--v-border-color), 0.12);
  transition: transform 0.15s, box-shadow 0.15s;
}
.chat-file:hover { transform: translateY(-1px); box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--c) 45%, transparent), 0 6px 14px -8px rgba(15, 23, 42, 0.4); }
.chat-msg.is-mine .chat-file { background: rgba(255, 255, 255, 0.16); box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.22); }

.chat-file-icon {
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  flex-shrink: 0;
  border-radius: 10px;
  color: #fff;
  background: linear-gradient(140deg, #f43f5e, #dc2626);
}
.chat-file-info { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.chat-file-name { font-size: 13px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.chat-file-meta { font-size: 11px; opacity: 0.65; }
.chat-file-action {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  flex-shrink: 0;
  border-radius: 50%;
  transition: background 0.15s;
}
.chat-file-action:hover { background: rgba(var(--v-theme-on-surface), 0.1); }
.chat-msg.is-mine .chat-file-action:hover { background: rgba(255, 255, 255, 0.2); }

.chat-file-removed { cursor: default; }
.chat-file-removed:hover { transform: none; }
.chat-file-removed .chat-file-icon { background: rgba(var(--v-theme-on-surface), 0.25); }
.chat-file-removed .chat-file-name { text-decoration: line-through; opacity: 0.75; }
.chat-msg.is-pending .chat-file-removed .chat-file-name { text-decoration: none; }

/* Photo inside a bubble — a fixed height, so a picture loading in never shifts the list */
.chat-photo {
  position: relative;
  margin: 4px -6px 2px;
  border-radius: 14px;
  overflow: hidden;
  line-height: 0;
}

.chat-photo-open {
  display: block;
  padding: 0;
  border: 0;
  background: transparent;
  cursor: zoom-in;
}
.chat-photo-open:disabled { cursor: default; }

.chat-photo img {
  display: block;
  height: 220px;
  width: auto;
  max-width: 100%;
  min-width: 120px;
  object-fit: cover;
  transition: transform 0.3s ease, filter 0.2s;
}
.chat-photo-open:not(:disabled):hover img { transform: scale(1.03); }
.chat-photo.is-uploading img { filter: blur(1px) brightness(0.7); }

.chat-photo-progress {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  pointer-events: none;
}

.chat-photo-download {
  position: absolute;
  top: 8px;
  right: 8px;
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border: 0;
  border-radius: 50%;
  color: #fff;
  background: rgba(15, 23, 42, 0.55);
  backdrop-filter: blur(6px);
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.15s, background 0.15s;
}
.chat-photo:hover .chat-photo-download { opacity: 1; }
.chat-photo-download:hover { background: rgba(15, 23, 42, 0.8); }

.chat-photo-placeholder {
  display: grid;
  place-items: center;
  width: 220px;
  height: 220px;
  max-width: 100%;
  background: rgba(var(--v-theme-on-surface), 0.08);
}

/* Delete (own messages, on hover) */
.chat-msg-delete {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  align-self: center;
  border: 0;
  border-radius: 50%;
  color: rgba(var(--v-theme-on-surface), 0.6);
  background: rgba(var(--v-theme-surface), 0.9);
  box-shadow: 0 2px 8px rgba(15, 23, 42, 0.12);
  cursor: pointer;
  opacity: 0;
  transform: scale(0.85);
  transition: opacity 0.15s, transform 0.15s, color 0.15s;
}
.chat-msg:hover .chat-msg-delete,
.chat-msg-delete[aria-expanded='true'] { opacity: 1; transform: scale(1); }
.chat-msg-delete:hover { color: rgb(var(--v-theme-error)); }

.chat-confirm {
  padding: 12px 14px 8px;
  border-radius: 14px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 12px 32px -8px rgba(15, 23, 42, 0.35);
  max-width: 260px;
}
.chat-confirm-title { font-size: 13px; font-weight: 700; }
.chat-confirm-text { margin-top: 2px; font-size: 12px; opacity: 0.7; }
.chat-confirm-actions { display: flex; justify-content: flex-end; gap: 4px; margin-top: 8px; }

/* Empty & loading */
.chat-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 24px;
  text-align: center;
}
.chat-empty-icon {
  display: grid;
  place-items: center;
  width: 76px;
  height: 76px;
  margin-bottom: 14px;
  border-radius: 24px;
  color: #fff;
  background: linear-gradient(135deg, var(--chat-accent), var(--chat-accent-2));
  box-shadow: 0 16px 32px -12px rgba(99, 102, 241, 0.6);
  animation: chat-float 3.2s ease-in-out infinite;
}
.chat-empty-title { font-size: 16px; font-weight: 700; }
.chat-empty-text { max-width: 340px; margin-top: 4px; font-size: 13px; opacity: 0.65; }

.chat-skeleton { display: flex; flex-direction: column; gap: 14px; padding-top: 8px; }
.chat-skeleton-bubble {
  width: 46%;
  height: 46px;
  border-radius: 18px;
  background: linear-gradient(90deg, rgba(var(--v-theme-on-surface), 0.06), rgba(var(--v-theme-on-surface), 0.12), rgba(var(--v-theme-on-surface), 0.06));
  background-size: 200% 100%;
  animation: chat-shimmer 1.2s linear infinite;
}
.chat-skeleton-bubble.is-mine { align-self: flex-end; width: 38%; }

/* New messages pill */
.chat-new-pill {
  position: absolute;
  left: 50%;
  bottom: 118px;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 14px;
  border: 0;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, var(--chat-accent), var(--chat-accent-2));
  box-shadow: 0 8px 20px -6px rgba(99, 102, 241, 0.6);
  cursor: pointer;
  z-index: 2;
}

/* Composer */
.chat-composer {
  padding: 10px 14px 8px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 -1px 0 rgba(var(--v-border-color), 0.1);
}

.chat-attachments { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
.chat-attachment {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 260px;
  padding: 4px 6px 4px 8px;
  border-radius: 10px;
  font-size: 12px;
  background: rgba(var(--v-theme-on-surface), 0.06);
}
.chat-attachment.is-photo { padding-left: 4px; }
.chat-attachment-thumb {
  width: 28px;
  height: 28px;
  flex-shrink: 0;
  border-radius: 7px;
  object-fit: cover;
}
.chat-attachment-name { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-weight: 600; }
.chat-attachment-size { opacity: 0.6; flex-shrink: 0; }
.chat-attachment button {
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 50%;
  background: transparent;
  color: inherit;
  cursor: pointer;
  opacity: 0.6;
}
.chat-attachment button:hover { opacity: 1; color: rgb(var(--v-theme-error)); }

.chat-input-row { display: flex; align-items: flex-end; gap: 8px; }

.chat-icon-btn {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  flex-shrink: 0;
  border: 0;
  border-radius: 50%;
  color: rgba(var(--v-theme-on-surface), 0.65);
  background: rgba(var(--v-theme-on-surface), 0.05);
  cursor: pointer;
  transition: background 0.15s, color 0.15s, transform 0.15s;
}
.chat-icon-btn:hover { color: var(--chat-accent); background: rgba(99, 102, 241, 0.12); transform: rotate(-12deg); }

.chat-input-wrap {
  flex: 1;
  display: flex;
  border-radius: 22px;
  background: rgba(var(--v-theme-on-surface), 0.05);
  box-shadow: inset 0 0 0 1px rgba(var(--v-border-color), 0.12);
  transition: box-shadow 0.15s, background 0.15s;
}
.chat-input-wrap:focus-within {
  background: rgb(var(--v-theme-surface));
  box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--c) 70%, transparent), 0 0 0 4px color-mix(in srgb, var(--c) 14%, transparent);
}

.chat-input {
  flex: 1;
  min-height: 42px;
  max-height: 140px;
  padding: 11px 16px;
  border: 0;
  outline: none;
  resize: none;
  font: inherit;
  font-size: 14px;
  line-height: 1.4;
  color: inherit;
  background: transparent;
}

.chat-send {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  flex-shrink: 0;
  border: 0;
  border-radius: 50%;
  color: #fff;
  background: linear-gradient(135deg, var(--chat-accent), var(--chat-accent-2));
  box-shadow: 0 8px 18px -8px rgba(99, 102, 241, 0.8);
  cursor: pointer;
  transition: transform 0.15s, opacity 0.15s, box-shadow 0.15s;
}
.chat-send:hover:not(:disabled) { transform: translateY(-1px) scale(1.05); box-shadow: 0 10px 22px -8px rgba(99, 102, 241, 0.9); }
.chat-send:active:not(:disabled) { transform: scale(0.95); }
.chat-send:disabled { opacity: 0.4; cursor: default; box-shadow: none; }

.chat-hint { margin-top: 6px; padding-left: 52px; font-size: 10.5px; opacity: 0.5; }

/* Drag & drop overlay */
.chat-drop {
  position: absolute;
  inset: 10px;
  z-index: 5;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border-radius: 18px;
  font-weight: 700;
  color: var(--chat-accent);
  background: rgba(var(--v-theme-surface), 0.88);
  backdrop-filter: blur(4px);
  box-shadow: inset 0 0 0 2px var(--chat-accent);
  border: 2px dashed transparent;
  pointer-events: none;
}

/* Motion */
.chat-msg-enter-active { transition: opacity 0.25s ease, transform 0.25s cubic-bezier(0.2, 0.9, 0.3, 1.2); }
.chat-msg-enter-from { opacity: 0; transform: translateY(10px) scale(0.97); }
.chat-msg-leave-active { transition: opacity 0.2s ease, transform 0.2s ease; }
.chat-msg-leave-to { opacity: 0; transform: scale(0.94); }
.chat-fade-enter-active,
.chat-fade-leave-active { transition: opacity 0.18s ease; }
.chat-fade-enter-from,
.chat-fade-leave-to { opacity: 0; }

@keyframes chat-shimmer {
  from { background-position: 200% 0; }
  to { background-position: -200% 0; }
}

@keyframes chat-float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-6px); }
}

@media (prefers-reduced-motion: reduce) {
  .chat-empty-icon,
  .chat-skeleton-bubble { animation: none; }
  .chat-msg-enter-active,
  .chat-msg-leave-active { transition: none; }
}

@media (max-width: 600px) {
  .chat-shell { height: 92vh; border-radius: 16px; }
  .chat-participants { display: none; }
  .chat-bubble { max-width: 82%; }
  .chat-hint { display: none; }
}
</style>

<style>
/* The dialog's own surface is transparent — the chat shell draws the card. */
.chat-dialog { box-shadow: none !important; }
</style>
