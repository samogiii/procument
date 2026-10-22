<template>
  <v-dialog
    :model-value="modelValue"
    :max-width="hasEdit ? 1720 : 1320"
    scrollable
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <v-card class="email-viewer">
      <!-- Title bar -->
      <div class="ev-titlebar">
        <v-icon icon="mdi-email-open-outline" color="primary" />
        <h2 class="ev-subject">{{ email?.subject || '(no subject)' }}</h2>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </div>

      <div v-if="loading" class="d-flex justify-center align-center" style="min-height: 320px;">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else-if="email" class="ev-layout" :class="{ 'with-preview': preview, 'with-edit': hasEdit }">
        <!-- ═══ Email column ═══ -->
        <div class="ev-email-col">
          <!-- Sender / recipients -->
          <div class="ev-meta">
            <v-avatar :color="avatarColor" size="44" class="ev-avatar">
              <span class="text-subtitle-1 font-weight-bold text-white">{{ initials }}</span>
            </v-avatar>
            <div class="ev-meta-main">
              <div class="ev-from-line">
                <span class="ev-from-name">{{ sender.name || sender.email }}</span>
                <span v-if="sender.name && sender.email" class="ev-from-addr">&lt;{{ sender.email }}&gt;</span>
                <v-btn
                  v-if="sender.email"
                  icon="mdi-content-copy"
                  size="x-small"
                  variant="text"
                  density="comfortable"
                  title="Copy email address"
                  @click="copy(sender.email)"
                />
              </div>
              <div v-if="email.toHeader" class="ev-recipients">
                <span class="ev-label">To</span>
                <span>{{ email.toHeader }}</span>
              </div>
              <div v-if="email.ccHeader" class="ev-recipients">
                <span class="ev-label">Cc</span>
                <span>{{ email.ccHeader }}</span>
              </div>
            </div>
            <div v-if="email.emailDate" class="ev-date" :title="new Date(email.emailDate).toString()">
              {{ formatDate(email.emailDate) }}
            </div>
          </div>

          <!-- Attachments -->
          <div v-if="email.attachments.length" class="ev-attachments">
            <div class="ev-section-label">
              <v-icon icon="mdi-paperclip" size="16" />
              {{ email.attachments.length }} attachment{{ email.attachments.length === 1 ? '' : 's' }}
            </div>
            <div class="ev-att-list">
              <div
                v-for="att in email.attachments"
                :key="att.id"
                class="ev-att"
                :class="{ active: preview?.id === att.id }"
                :title="canPreview(att) ? 'Click to preview' : 'Click to download'"
                @click="canPreview(att) ? openPreview(att) : download(att)"
              >
                <v-icon :icon="fileIcon(att).icon" :color="fileIcon(att).color" size="26" />
                <div class="ev-att-text">
                  <div class="ev-att-name">{{ att.fileName }}</div>
                  <div class="ev-att-size">{{ formatSize(att.sizeBytes) }}</div>
                </div>
                <v-btn
                  icon="mdi-download-outline"
                  size="x-small"
                  variant="text"
                  title="Download"
                  :loading="downloadingId === att.id"
                  @click.stop="download(att)"
                />
              </div>
            </div>
          </div>

          <!-- Body -->
          <div class="ev-body-toolbar">
            <v-btn-toggle v-if="htmlSource && plainSource" v-model="bodyMode" density="compact" variant="outlined" divided mandatory>
              <v-btn value="html" size="small">Original</v-btn>
              <v-btn value="text" size="small">Plain text</v-btn>
            </v-btn-toggle>
            <v-spacer />
            <template v-if="bodyMode === 'html' && hasRemoteImages">
              <span class="text-caption text-medium-emphasis mr-2">
                {{ showRemoteImages ? 'Remote images shown' : 'Remote images blocked for privacy' }}
              </span>
              <v-btn size="small" variant="text" @click="showRemoteImages = !showRemoteImages">
                {{ showRemoteImages ? 'Block' : 'Load images' }}
              </v-btn>
            </template>
          </div>

          <div class="ev-paper">
            <iframe
              v-if="bodyMode === 'html' && htmlSource"
              class="ev-html-frame"
              :srcdoc="htmlDoc"
              sandbox="allow-popups allow-popups-to-escape-sandbox"
              referrerpolicy="no-referrer"
              title="Email body"
            />
            <div v-else class="ev-text">
              <template v-for="(block, i) in textBlocks" :key="i">
                <div v-if="!block.quoted" class="ev-text-block" v-html="block.html" />
                <div v-else class="ev-quoted">
                  <button class="ev-quoted-toggle" type="button" @click="toggleQuoted(i)">
                    <v-icon :icon="openQuoted.has(i) ? 'mdi-chevron-up' : 'mdi-dots-horizontal'" size="16" />
                    {{ openQuoted.has(i) ? 'Hide earlier message' : 'Show earlier message' }}
                  </button>
                  <div v-if="openQuoted.has(i)" class="ev-text-block ev-quoted-text" v-html="block.html" />
                </div>
              </template>
              <p v-if="!textBlocks.length" class="text-medium-emphasis">This email has no text.</p>
            </div>
          </div>
        </div>

        <!-- ═══ Attachment preview column ═══ -->
        <div v-if="preview" class="ev-preview-col">
          <div class="ev-preview-head">
            <v-icon :icon="fileIcon(preview).icon" :color="fileIcon(preview).color" size="20" />
            <span class="ev-preview-name">{{ preview.fileName }}</span>
            <v-spacer />
            <v-btn icon="mdi-open-in-new" size="small" variant="text" title="Open in new tab" :disabled="!previewUrl" @click="openInTab" />
            <v-btn icon="mdi-download-outline" size="small" variant="text" title="Download" :disabled="!previewUrl" @click="downloadPreview" />
            <v-btn icon="mdi-close" size="small" variant="text" title="Close preview" @click="closePreview" />
          </div>
          <div class="ev-preview-body">
            <v-progress-circular v-if="!previewUrl" indeterminate color="primary" class="ma-auto" />
            <img v-else-if="isImage(preview)" :src="previewUrl" :alt="preview.fileName" class="ev-preview-img" />
            <iframe v-else :src="previewUrl" class="ev-preview-frame" :title="preview.fileName" />
          </div>
        </div>

        <!-- ═══ Quote details (editable, autosaved) — always the right-most column ═══ -->
        <aside v-if="hasEdit && modelValue" class="ev-edit-col">
          <div class="ev-edit-head">
            <v-icon icon="mdi-pencil-box-multiple-outline" color="primary" size="20" />
            <span class="ev-edit-title">{{ editTitle || 'Quote details' }}</span>
            <v-spacer />
            <v-chip size="x-small" variant="tonal">{{ rows!.length }} line{{ rows!.length === 1 ? '' : 's' }}</v-chip>
          </div>
          <!-- Emails can carry 100+ lines: pick one by typing its P/N, or step through with the arrows -->
          <div v-if="rows!.length > 1" class="ev-line-picker">
            <v-btn
              icon="mdi-chevron-left"
              size="small"
              variant="text"
              title="Previous line"
              :disabled="activeIndex <= 0"
              @click="stepLine(-1)"
            />
            <v-autocomplete
              :model-value="activeRow?.id"
              :items="lineItems"
              item-title="title"
              item-value="id"
              label="Line — type a part number"
              density="compact"
              variant="outlined"
              prepend-inner-icon="mdi-magnify"
              hide-details
              auto-select-first
              class="ev-line-select"
              @update:model-value="(id: number | null) => { if (id != null) activeRowId = id }"
            >
              <template #item="{ props: itemProps, item }">
                <v-list-item v-bind="itemProps" :title="item.raw.partNumber" density="compact">
                  <template #prepend><span class="ev-line-no">{{ item.raw.no }}</span></template>
                  <template v-if="item.raw.detail" #subtitle><span class="ev-line-alt">{{ item.raw.detail }}</span></template>
                </v-list-item>
              </template>
            </v-autocomplete>
            <span class="ev-line-count">{{ activeIndex + 1 }} / {{ rows!.length }}</span>
            <v-btn
              icon="mdi-chevron-right"
              size="small"
              variant="text"
              title="Next line"
              :disabled="activeIndex >= rows!.length - 1"
              @click="stepLine(1)"
            />
          </div>
          <div class="ev-edit-body">
            <!-- Email RFQs puts its own line form here; Email Quotes uses the quote form below. -->
            <slot v-if="$slots.edit && activeRow" name="edit" :row="activeRow" />
            <EmailQuoteEditForm
              v-else-if="activeRow"
              :row="activeRow"
              :accepted="accepted"
              compact
              @updated="r => emit('row-updated', r)"
              @open-certificate="c => openCertificatePreview(activeRow!, c)"
            />
          </div>
        </aside>
      </div>

      <div v-else class="pa-8 text-center text-medium-emphasis">The email could not be loaded.</div>
    </v-card>

    <v-snackbar v-model="snackbar" :timeout="2000" color="success">{{ snackText }}</v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
type Attachment = { id: number; fileName: string; contentType?: string; sizeBytes: number }
type Email = {
  id: number
  subject?: string
  fromHeader?: string
  fromEmail?: string
  toHeader?: string
  ccHeader?: string
  emailDate?: string
  bodyHtml?: string
  bodyText?: string
  attachments: Attachment[]
}

const props = defineProps<{
  modelValue: boolean
  emailId: number | null
  /** Open this attachment (by file name) in the preview pane straight away. */
  initialAttachment?: string | null
  /** Quote lines extracted from this email — shown editable in the right-hand panel. */
  rows?: any[]
  /** The lines are already accepted (edits only reach new RFQs). */
  accepted?: boolean
  /** API the email and its attachments are read from — Email RFQs passes '/pending-rfqs'. */
  apiBase?: string
  /** Heading of the edit panel (default "Quote details"). */
  editTitle?: string
}>()
const emailApi = computed(() => props.apiBase || '/pending-supplier-quotes')
const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
  /** A line was autosaved / its cert PDFs changed — the refreshed line for the parent's list. */
  (e: 'row-updated', row: any): void
}>()

// ── Editable quote lines (right panel) ──
const hasEdit = computed(() => !!props.rows?.length)
const activeRowId = ref<number | null>(null)
const activeRow = computed(() => props.rows?.find(r => r.id === activeRowId.value) ?? props.rows?.[0] ?? null)
const activeIndex = computed(() => props.rows?.findIndex(r => r.id === activeRow.value?.id) ?? -1)
/** Line picker entries; the title holds alt / S/N too so typing any of them finds the line. */
const lineItems = computed(() => (props.rows ?? []).map((r, i) => {
  const detail = [
    r.alt ? `alt ${r.alt}` : '',
    r.alternatives?.length ? `alt ${r.alternatives.join(', ')}` : '',
    r.serialNumber ? `S/N ${r.serialNumber}` : '',
    r.condition || '',
  ].filter(Boolean).join(' · ')
  const partNumber = r.partNumber || '—'
  return { id: r.id, no: i + 1, partNumber, detail, title: detail ? `${partNumber} · ${detail}` : partNumber }
}))
function stepLine(delta: number) {
  const next = props.rows?.[activeIndex.value + delta]
  if (next) activeRowId.value = next.id
}

const api = useApi()
const email = ref<Email | null>(null)
const loading = ref(false)
const bodyMode = ref<'html' | 'text'>('html')
const showRemoteImages = ref(false)

const snackbar = ref(false)
const snackText = ref('')

// ── Sender ──
const sender = computed(() => {
  const header = email.value?.fromHeader || ''
  const m = header.match(/^\s*"?([^"<]*?)"?\s*<([^>]+)>\s*$/)
  if (m) return { name: m[1]!.trim(), email: m[2]!.trim() }
  return { name: '', email: email.value?.fromEmail || header.trim() }
})
const initials = computed(() => {
  const base = sender.value.name || sender.value.email || '?'
  const words = base.replace(/@.*/, '').split(/[\s._-]+/).filter(Boolean)
  return ((words[0]?.[0] || '?') + (words[1]?.[0] || '')).toUpperCase()
})
const AVATAR_COLORS = ['#2563eb', '#7c3aed', '#db2777', '#ea580c', '#059669', '#0891b2', '#4f46e5', '#b45309']
const avatarColor = computed(() => {
  const s = sender.value.email || sender.value.name
  let h = 0
  for (const c of s) h = (h * 31 + c.charCodeAt(0)) >>> 0
  return AVATAR_COLORS[h % AVATAR_COLORS.length]
})

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  })
}
function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
async function copy(text: string) {
  // Served over plain HTTP on the LAN — the async clipboard API isn't available there.
  const ta = document.createElement('textarea')
  ta.value = text
  ta.style.position = 'fixed'
  ta.style.opacity = '0'
  document.body.appendChild(ta)
  ta.select()
  document.execCommand('copy')
  document.body.removeChild(ta)
  snackText.value = 'Email address copied'
  snackbar.value = true
}

// ── HTML body: sandboxed (no scripts), remote content blocked unless asked for ──

/** Single-part HTML emails used to arrive as "text" — raw <html><head>… — so detect that too. */
function looksLikeHtml(s?: string | null) {
  const head = (s || '').slice(0, 3000)
  return /^\s*<(!doctype|html|head|body|meta|div|table|p|span|style|font)\b/i.test(head)
    || /<(html|body)[\s>]/i.test(head)
}
const htmlSource = computed(() => {
  const e = email.value
  if (!e) return ''
  return e.bodyHtml || (looksLikeHtml(e.bodyText) ? e.bodyText! : '')
})
const plainSource = computed(() => (looksLikeHtml(email.value?.bodyText) ? '' : email.value?.bodyText || ''))

/**
 * The part of the email worth rendering: for a whole document, its <style> blocks plus the
 * <body> contents (a document nested inside ours renders badly). Scripts, <meta> (refresh!),
 * <base> and <title> are dropped — the iframe is sandboxed anyway.
 */
function emailMarkup(html: string) {
  const cleaned = html
    .replace(/<script[\s\S]*?<\/script>/gi, '')
    .replace(/<(meta|base|link)\b[^>]*>/gi, '')
    .replace(/<title[\s\S]*?<\/title>/gi, '')
  const body = cleaned.match(/<body[^>]*>([\s\S]*?)(<\/body>|$)/i)
  if (!body) return cleaned.replace(/<\/?(html|head)\b[^>]*>/gi, '')
  const inner = body[1] || ''
  const headStyles = (cleaned.slice(0, body.index).match(/<style[\s\S]*?<\/style>/gi) || []).join('\n')
  return headStyles + inner
}

const hasRemoteImages = computed(() => /<img[^>]+src\s*=\s*["']?https?:/i.test(htmlSource.value)
  || /url\(\s*["']?https?:/i.test(htmlSource.value))

const htmlDoc = computed(() => {
  const img = showRemoteImages.value ? 'data: https: http:' : 'data:'
  const csp = `default-src 'none'; img-src ${img}; style-src 'unsafe-inline'; font-src data:;`
  return `<!doctype html><html><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="${csp}">
<base target="_blank">
<style>
  html { background: #ffffff; }
  body { margin: 0; padding: 28px 32px; color: #1f2937; background: #ffffff;
         font: 15px/1.65 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
         word-wrap: break-word; overflow-wrap: anywhere; }
  img { max-width: 100%; height: auto; }
  table { max-width: 100%; }
  a { color: #2563eb; }
  blockquote { margin: 12px 0; padding: 4px 0 4px 14px; border-left: 3px solid #d1d5db; color: #4b5563; }
  pre { white-space: pre-wrap; }
</style></head><body>${emailMarkup(htmlSource.value)}</body></html>`
})

// ── Plain-text body: escaped, links clickable, earlier thread folded away ──
const openQuoted = ref<Set<number>>(new Set())
function toggleQuoted(i: number) {
  const s = new Set(openQuoted.value)
  s.has(i) ? s.delete(i) : s.add(i)
  openQuoted.value = s
}

function escapeHtml(s: string) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}
function linkify(escaped: string) {
  return escaped
    .replace(/\b(https?:\/\/[^\s<]+[^\s<.,;:!?)\]])/g, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>')
    .replace(/\b([\w.+-]+@[\w-]+(?:\.[\w-]+)+)\b/g, '<a href="mailto:$1">$1</a>')
}

/** Lines that start the quoted earlier message in a reply. */
const QUOTE_START = /^(-{2,}\s*(original message|forwarded message)\s*-{2,}|on .{3,200}wrote:\s*$|from:\s.+$|_{10,}\s*$)/i

const textBlocks = computed(() => {
  const text = plainSource.value.replace(/\r\n/g, '\n').replace(/\n{3,}/g, '\n\n').trim()
  if (!text) return []
  const lines = text.split('\n')
  const blocks: { quoted: boolean; lines: string[] }[] = []
  let quoted = false
  for (const line of lines) {
    const isQuoteLine = /^\s*>/.test(line)
    // Everything after the first "Original Message" / "On … wrote:" header is the older thread.
    if (!quoted && (QUOTE_START.test(line.trim()) && blocks.length > 0 || isQuoteLine)) quoted = true
    const current = blocks[blocks.length - 1]
    if (!current || current.quoted !== quoted) blocks.push({ quoted, lines: [line] })
    else current.lines.push(line)
  }
  return blocks.map(b => ({
    quoted: b.quoted,
    html: linkify(escapeHtml(b.lines.map(l => l.replace(/^\s*>\s?/, '')).join('\n').trim())),
  })).filter(b => b.html)
})

// ── Attachments ──
function isPdf(a: Attachment) {
  return (a.contentType || '').includes('pdf') || /\.pdf$/i.test(a.fileName)
}
function isImage(a: Attachment) {
  return (a.contentType || '').startsWith('image/') || /\.(png|jpe?g|gif|webp|bmp)$/i.test(a.fileName)
}
function canPreview(a: Attachment) {
  return isPdf(a) || isImage(a)
}
function fileIcon(a: Attachment) {
  if (isPdf(a)) return { icon: 'mdi-file-pdf-box', color: 'red' }
  if (isImage(a)) return { icon: 'mdi-file-image-outline', color: 'purple' }
  if (/\.(xlsx?|xlsm|csv)$/i.test(a.fileName)) return { icon: 'mdi-file-excel-outline', color: 'green' }
  if (/\.docx?$/i.test(a.fileName)) return { icon: 'mdi-file-word-outline', color: 'blue' }
  return { icon: 'mdi-file-outline', color: 'grey' }
}

async function fetchBlob(a: Attachment): Promise<Blob> {
  const raw = await api.get<Blob>(`${emailApi.value}/emails/${email.value!.id}/attachments/${a.id}`, { responseType: 'blob' })
  return new Blob([raw], { type: a.contentType || (isPdf(a) ? 'application/pdf' : 'application/octet-stream') })
}

const preview = ref<Attachment | null>(null)
const previewUrl = ref('')
async function openPreview(a: Attachment, load: () => Promise<Blob> = () => fetchBlob(a)) {
  closePreview()
  preview.value = a
  try {
    previewUrl.value = URL.createObjectURL(await load())
  } catch {
    preview.value = null
    snackText.value = 'Could not open the file'
    snackbar.value = true
  }
}
/** A line's cert PDF opens in the same middle pane as the email's attachments. */
function openCertificatePreview(row: { id: number }, c: { id: number; fileName: string; sizeBytes: number }) {
  // Negative id so it never highlights an email attachment chip with the same id.
  const entry: Attachment = { id: -c.id, fileName: c.fileName, contentType: 'application/pdf', sizeBytes: c.sizeBytes }
  openPreview(entry, async () => {
    const raw = await api.get<Blob>(`/pending-supplier-quotes/${row.id}/certificates/${c.id}`, { responseType: 'blob' })
    return new Blob([raw], { type: 'application/pdf' })
  })
}
function downloadPreview() {
  if (!previewUrl.value || !preview.value) return
  const link = document.createElement('a')
  link.href = previewUrl.value
  link.download = preview.value.fileName
  link.click()
}
function closePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  preview.value = null
}
function openInTab() {
  if (previewUrl.value) window.open(previewUrl.value, '_blank', 'noopener')
}

const downloadingId = ref<number | null>(null)
async function download(a: Attachment) {
  downloadingId.value = a.id
  try {
    const url = URL.createObjectURL(await fetchBlob(a))
    const link = document.createElement('a')
    link.href = url
    link.download = a.fileName
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } finally {
    downloadingId.value = null
  }
}

// Declared last: with `immediate` it runs during setup, so everything it calls must already exist.
watch(() => [props.modelValue, props.emailId] as const, async ([open, id]) => {
  if (!open) {
    closePreview()
    return
  }
  if (!id) return
  loading.value = true
  email.value = null
  activeRowId.value = null
  showRemoteImages.value = false
  openQuoted.value = new Set()
  try {
    email.value = await api.get<Email>(`${emailApi.value}/emails/${id}`)
    bodyMode.value = htmlSource.value ? 'html' : 'text'
    const first = props.initialAttachment
      ? email.value.attachments.find(a => a.fileName === props.initialAttachment)
      : null
    if (first) {
      if (canPreview(first)) openPreview(first)
      else download(first)
    }
  } catch {
    email.value = null
  } finally {
    loading.value = false
  }
}, { immediate: true })

onBeforeUnmount(closePreview)
</script>

<style scoped>
.email-viewer {
  height: 90vh;
  display: flex;
  flex-direction: column;
}
.ev-titlebar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 20px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.ev-subject {
  font-size: 1.15rem;
  font-weight: 600;
  line-height: 1.35;
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ev-layout {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr;
}
.ev-layout.with-preview {
  grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr);
}
/* Edit panel always sits at the right edge; an open PDF / image takes the middle. */
.ev-layout.with-edit {
  grid-template-columns: minmax(0, 1fr) 400px;
}
.ev-layout.with-edit.with-preview {
  grid-template-columns: minmax(0, 0.85fr) minmax(0, 1.15fr) 380px;
}
@media (max-width: 1100px) {
  .ev-layout.with-edit.with-preview { grid-template-columns: minmax(0, 1fr) 360px; }
  .ev-layout.with-edit.with-preview .ev-email-col { display: none; }
}
@media (max-width: 960px) {
  .ev-layout.with-preview,
  .ev-layout.with-edit,
  .ev-layout.with-edit.with-preview { grid-template-columns: 1fr; grid-template-rows: auto auto; overflow-y: auto; }
}

.ev-edit-col {
  display: flex;
  flex-direction: column;
  min-height: 0;
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-on-surface), 0.02);
}
.ev-edit-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.ev-edit-title { font-weight: 600; }
.ev-line-picker {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 10px 10px 0;
}
.ev-line-select { flex: 1; min-width: 0; }
.ev-line-count { font-size: 12px; opacity: 0.7; white-space: nowrap; padding: 0 2px; }
.ev-line-no { font-size: 11px; opacity: 0.6; min-width: 26px; }
.ev-line-alt { font-size: 11px; color: #f59e0b; }
.ev-edit-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 14px 16px 20px;
}

.ev-email-col {
  overflow-y: auto;
  padding: 18px 22px 24px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

/* Sender block */
.ev-meta {
  display: flex;
  align-items: flex-start;
  gap: 14px;
}
.ev-meta-main { flex: 1; min-width: 0; }
.ev-from-line {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}
.ev-from-name { font-size: 1rem; font-weight: 600; }
.ev-from-addr { font-size: 0.9rem; opacity: 0.7; }
.ev-recipients {
  font-size: 0.85rem;
  opacity: 0.75;
  margin-top: 2px;
  display: flex;
  gap: 8px;
}
.ev-label { font-weight: 600; min-width: 22px; }
.ev-date { font-size: 0.85rem; opacity: 0.7; white-space: nowrap; padding-top: 2px; }

/* Attachments */
.ev-section-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  opacity: 0.7;
  margin-bottom: 8px;
}
.ev-att-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}
.ev-att {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 6px 8px 12px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 10px;
  cursor: pointer;
  max-width: 300px;
  transition: background 0.15s, border-color 0.15s;
}
.ev-att:hover { background: rgba(var(--v-theme-primary), 0.06); }
.ev-att.active { border-color: rgb(var(--v-theme-primary)); background: rgba(var(--v-theme-primary), 0.1); }
.ev-att-text { min-width: 0; }
.ev-att-name {
  font-size: 0.875rem;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ev-att-size { font-size: 0.75rem; opacity: 0.65; }

/* Body */
.ev-body-toolbar {
  display: flex;
  align-items: center;
  min-height: 32px;
}
.ev-paper {
  background: #ffffff;
  color: #1f2937;
  border-radius: 10px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.12), 0 1px 2px rgba(0, 0, 0, 0.08);
  overflow: hidden;
  flex: 1;
  min-height: 420px;
  display: flex;
}
.ev-html-frame {
  border: 0;
  width: 100%;
  min-height: 60vh;
  flex: 1;
  background: #ffffff;
}
.ev-text {
  padding: 28px 34px;
  max-width: 780px;
  font-size: 15px;
  line-height: 1.7;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
}
.ev-text-block {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  margin-bottom: 14px;
}
.ev-text-block :deep(a) { color: #2563eb; }
.ev-quoted { margin: 6px 0 14px; }
.ev-quoted-toggle {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #6b7280;
  background: #f3f4f6;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 2px 10px;
  cursor: pointer;
}
.ev-quoted-text {
  margin-top: 10px;
  padding-left: 14px;
  border-left: 3px solid #d1d5db;
  color: #4b5563;
}

/* Preview */
.ev-preview-col {
  display: flex;
  flex-direction: column;
  min-height: 0;
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.ev-preview-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.ev-preview-name {
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ev-preview-body {
  flex: 1;
  min-height: 60vh;
  display: flex;
  background: #525659;
}
.ev-preview-frame { border: 0; width: 100%; flex: 1; }
.ev-preview-img { max-width: 100%; max-height: 100%; margin: auto; object-fit: contain; }
</style>
