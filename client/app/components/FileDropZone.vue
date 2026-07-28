<template>
  <div>
    <div
      class="drop-zone"
      :class="{
        'drop-zone--active': dragDepth > 0,
        'drop-zone--busy': busy,
        'drop-zone--compact': compact,
      }"
      role="button"
      tabindex="0"
      @click="pickFiles"
      @keydown.enter.prevent="pickFiles"
      @keydown.space.prevent="pickFiles"
      @dragenter.prevent="onDragEnter"
      @dragover.prevent="onDragOver"
      @dragleave.prevent="onDragLeave"
      @drop.prevent="onDrop"
    >
      <v-icon
        :icon="dragDepth > 0 ? 'mdi-tray-arrow-down' : 'mdi-cloud-upload-outline'"
        :size="compact ? 22 : 30"
        :color="dragDepth > 0 ? 'primary' : 'grey'"
      />
      <div :class="compact ? 'text-caption' : 'text-body-2'" class="mt-1">
        <span class="font-weight-medium">{{ dragDepth > 0 ? 'Drop to add' : label }}</span>
      </div>
      <div v-if="!compact" class="text-caption text-medium-emphasis">
        Images and PDFs · drop several at once
      </div>
    </div>

    <!-- Hidden picker so the box doubles as a click-to-browse target -->
    <input
      ref="inputEl"
      type="file"
      multiple
      :accept="accept"
      class="d-none"
      @change="onPick"
    >

    <!-- Staged files, not uploaded yet -->
    <div v-if="staged.length" class="mt-3">
      <div class="d-flex flex-wrap gap-2">
        <v-card
          v-for="f in staged"
          :key="f.id"
          variant="tonal"
          class="staged-file pa-1 d-flex align-center gap-2"
        >
          <v-img
            v-if="f.previewUrl"
            :src="f.previewUrl"
            width="36"
            height="36"
            cover
            class="rounded"
          />
          <v-icon v-else icon="mdi-file-document-outline" size="24" class="ml-1" />
          <div class="staged-file__meta">
            <div class="text-caption text-truncate">{{ f.file.name }}</div>
            <div class="text-caption text-medium-emphasis">{{ (f.file.size / 1024).toFixed(0) }} KB</div>
          </div>
          <v-btn
            icon="mdi-close"
            size="x-small"
            variant="text"
            :disabled="busy"
            @click.stop="remove(f.id)"
          />
        </v-card>
      </div>

      <div class="d-flex align-center gap-2 mt-2">
        <v-btn
          size="small"
          color="primary"
          variant="flat"
          prepend-icon="mdi-upload"
          :loading="busy"
          @click="submit"
        >
          Upload {{ staged.length }} file{{ staged.length === 1 ? '' : 's' }}
        </v-btn>
        <v-btn size="small" variant="text" :disabled="busy" @click="clearAll">Clear</v-btn>
        <span v-if="busy" class="text-caption text-medium-emphasis">
          {{ doneCount }} / {{ staged.length }} uploaded…
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface StagedFile {
  id: number
  file: File
  previewUrl: string | null
}

const props = withDefaults(defineProps<{
  /** Called with every staged file. Resolve to clear the box; reject to keep the files for a retry. */
  onUpload: (files: File[], onProgress: (done: number) => void) => Promise<void>
  label?: string
  accept?: string
  compact?: boolean
}>(), {
  label: 'Drag files here or click to browse',
  accept: 'image/*,application/pdf,.pdf,.png,.jpg,.jpeg',
  compact: false,
})

const inputEl = ref<HTMLInputElement | null>(null)
const staged = ref<StagedFile[]>([])
const busy = ref(false)
const doneCount = ref(0)

// dragenter/dragleave also fire when the cursor crosses child elements, so a plain
// boolean flickers. Counting enter/leave pairs keeps the highlight stable.
const dragDepth = ref(0)

let nextId = 0

function pickFiles() {
  if (busy.value) return
  inputEl.value?.click()
}

function onPick(e: Event) {
  const input = e.target as HTMLInputElement
  add(input.files)
  // Reset so picking the same file again still fires change.
  input.value = ''
}

function onDragEnter(e: DragEvent) {
  if (busy.value || !hasFiles(e)) return
  dragDepth.value++
}

function onDragOver(e: DragEvent) {
  if (busy.value || !hasFiles(e)) return
  if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy'
}

function onDragLeave() {
  if (dragDepth.value > 0) dragDepth.value--
}

function onDrop(e: DragEvent) {
  dragDepth.value = 0
  if (busy.value) return
  add(e.dataTransfer?.files ?? null)
}

/** Only light up for actual file drags, not text or an in-page row reorder. */
function hasFiles(e: DragEvent) {
  return Array.from(e.dataTransfer?.types ?? []).includes('Files')
}

function add(list: FileList | null) {
  if (!list?.length) return
  for (const file of Array.from(list)) {
    // Dropping a folder yields a zero-byte entry with no type — skip it rather
    // than posting something the server will reject.
    if (file.size === 0 && !file.type) continue
    staged.value.push({
      id: nextId++,
      file,
      previewUrl: file.type.startsWith('image/') ? URL.createObjectURL(file) : null,
    })
  }
}

function remove(id: number) {
  const idx = staged.value.findIndex(f => f.id === id)
  if (idx === -1) return
  const [gone] = staged.value.splice(idx, 1)
  if (gone?.previewUrl) URL.revokeObjectURL(gone.previewUrl)
}

function clearAll() {
  for (const f of staged.value) {
    if (f.previewUrl) URL.revokeObjectURL(f.previewUrl)
  }
  staged.value = []
}

async function submit() {
  if (!staged.value.length || busy.value) return
  busy.value = true
  doneCount.value = 0
  try {
    await props.onUpload(staged.value.map(f => f.file), n => { doneCount.value = n })
    clearAll()
  } catch {
    // Parent reports the failure; keep the files staged so they can retry.
  } finally {
    busy.value = false
    doneCount.value = 0
  }
}

// Object URLs are held by the browser until revoked — releasing them on unmount
// keeps repeated visits to this page from leaking the dropped images.
onBeforeUnmount(clearAll)
</script>

<style scoped>
.drop-zone {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 20px 12px;
  border: 2px dashed rgba(var(--v-border-color), 0.35);
  border-radius: 10px;
  cursor: pointer;
  transition: border-color 0.15s, background-color 0.15s;
}

.drop-zone--compact {
  padding: 12px 8px;
}

.drop-zone:hover {
  border-color: rgba(var(--v-theme-primary), 0.6);
  background-color: rgba(var(--v-theme-primary), 0.03);
}

.drop-zone--active {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.08);
}

.drop-zone--busy {
  cursor: default;
  opacity: 0.6;
}

.staged-file {
  width: 200px;
}

.staged-file__meta {
  min-width: 0;
  flex: 1 1 auto;
}
</style>
