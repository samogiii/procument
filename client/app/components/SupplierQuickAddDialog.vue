<template>
  <v-dialog :model-value="modelValue" max-width="500" @update:model-value="emit('update:modelValue', $event)">
    <v-card class="sq-card">
      <div class="sq-header">
        <div class="sq-icon">
          <v-icon :icon="mode === 'supplier' ? 'mdi-domain-plus' : 'mdi-email-plus-outline'" size="22" />
        </div>
        <div class="flex-grow-1" style="min-width: 0;">
          <div class="text-subtitle-1 font-weight-bold">
            {{ mode === 'supplier' ? 'Add New Supplier' : 'Supplier Email for this Item' }}
          </div>
          <div class="text-caption text-medium-emphasis text-truncate">
            {{ mode === 'supplier'
              ? 'Saved to the Supplier Catalog'
              : (lookup?.name || name || 'Supplier') }}
          </div>
        </div>
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </div>

      <v-card-text class="pt-4">
        <!-- ── Add-email mode: current emails of the supplier ── -->
        <template v-if="mode === 'email'">
          <v-progress-linear v-if="lookupLoading" indeterminate color="primary" class="mb-3" />
          <v-alert v-else-if="lookupError" type="error" variant="tonal" density="compact" class="mb-3">
            {{ lookupError }}
          </v-alert>
          <div v-else-if="lookup" class="mb-4">
            <div class="sq-label">Pick the email you talk to for this item</div>
            <div v-if="existingEmails.length" class="d-flex flex-column gap-1">
              <button
                v-for="e in existingEmails"
                :key="e.email"
                type="button"
                class="sq-email-option"
                :class="{ 'sq-email-option--current': isCurrent(e.email) }"
                @click="useExisting(e.email)"
              >
                <v-icon :icon="isCurrent(e.email) ? 'mdi-check-circle' : 'mdi-email-outline'" size="16" />
                <span class="sq-email-addr">{{ e.email }}</span>
                <span class="sq-email-owner">{{ e.owner }}</span>
              </button>
            </div>
            <div v-else class="text-caption text-medium-emphasis">No emails in the Catalog yet — add one below.</div>
            <div class="sq-divider"><span>or add a new email</span></div>
          </div>
        </template>

        <v-text-field
          v-if="mode === 'supplier'"
          v-model="form.name"
          label="Supplier name"
          variant="outlined"
          density="comfortable"
          prepend-inner-icon="mdi-domain"
          :disabled="!!domainMatch"
          :hint="domainMatch ? 'Ignored — the email domain belongs to an existing supplier' : ''"
          :persistent-hint="!!domainMatch"
          :class="{ 'sq-ignored': !!domainMatch }"
          class="mb-3"
          autofocus
        />

        <v-text-field
          v-model="form.email"
          label="Email"
          type="email"
          variant="outlined"
          density="comfortable"
          prepend-inner-icon="mdi-email-outline"
          :error-messages="emailError"
          :loading="domainLoading"
          :autofocus="mode === 'email'"
          class="mb-2"
          @update:model-value="onEmailChange"
          @keydown.enter.prevent="save"
        />

        <v-expand-transition>
          <v-alert
            v-if="domainMatch && (mode === 'supplier' || domainMatch.id !== targetId)"
            :type="mode === 'supplier' ? 'info' : 'warning'"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            <template v-if="mode === 'supplier'">
              <strong>@{{ domainInfo?.domain }}</strong> already belongs to
              <strong>{{ domainMatch.name }}</strong>. The email will be added to that supplier and it
              will be used instead of the name you typed.
            </template>
            <template v-else>
              <strong>@{{ domainInfo?.domain }}</strong> is used by another supplier:
              <strong>{{ domainMatch.name }}</strong>. Make sure this email really belongs here.
            </template>
          </v-alert>
        </v-expand-transition>
        <div v-if="domainInfo?.generic" class="text-caption text-medium-emphasis mb-3">
          <v-icon icon="mdi-information-outline" size="14" class="mr-1" />
          Free-mail domain — not matched against other suppliers.
        </div>

        <v-combobox
          v-model="form.contactName"
          :items="contactNames"
          label="Contact person (optional)"
          variant="outlined"
          density="comfortable"
          prepend-inner-icon="mdi-account-outline"
          :hint="contactHint"
          persistent-hint
          clearable
        />
      </v-card-text>

      <v-card-actions class="px-6 pb-5">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="saving"
          :disabled="!canSave"
          :prepend-icon="mode === 'supplier' ? (domainMatch ? 'mdi-link-variant' : 'mdi-plus') : 'mdi-email-plus-outline'"
          @click="save"
        >
          {{ saveLabel }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
/**
 * One dialog for both quick actions of the supplier picker:
 *  - mode 'supplier': name + email → POST /suppliers/quick-add (domain match wins over the typed name)
 *  - mode 'email':    add an email to an existing supplier → POST /suppliers/{id}/emails
 * Emits `saved` with the resulting supplier and a ready-to-show message.
 */
interface SupplierQuickAddResult {
  id: number
  name: string
  status?: string
  dependency?: string
  /** The email chosen/added — the row records it as the address used for this RFQ item. */
  email: string
  message: string
}

interface DomainInfo { valid: boolean; domain?: string; generic?: boolean; supplier?: { id: number; name: string; status?: string; dependency?: string } | null }
interface LookupResult { id: number; name: string; status?: string; dependency?: string; email?: string; contacts: { name: string; emails: string[] }[] }

const props = withDefaults(defineProps<{
  modelValue: boolean
  mode: 'supplier' | 'email'
  /** Typed supplier name (supplier mode) or the row's supplier name (email mode). */
  name?: string
  supplierId?: number | null
  /** Email currently recorded on the row (email mode) — highlighted in the list. */
  currentEmail?: string | null
}>(), { name: '', supplierId: null, currentEmail: null })

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: [result: SupplierQuickAddResult]
}>()

const api = useApi()
const EMAIL_RE = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/

const form = ref({ name: '', email: '', contactName: '' as string | null })
const saving = ref(false)
const serverError = ref('')
const domainInfo = ref<DomainInfo | null>(null)
const domainLoading = ref(false)
const lookup = ref<LookupResult | null>(null)
const lookupLoading = ref(false)
const lookupError = ref('')

const targetId = computed(() => lookup.value?.id ?? props.supplierId ?? null)
const domainMatch = computed(() => domainInfo.value?.supplier ?? null)
const emailValid = computed(() => EMAIL_RE.test(form.value.email.trim()))
const emailError = computed(() => {
  if (serverError.value) return serverError.value
  if (form.value.email.trim() && !emailValid.value) return 'Enter a valid email address'
  return ''
})

const existingEmails = computed(() => {
  if (!lookup.value) return []
  const list: { email: string; owner: string }[] = []
  for (const e of splitEmails(lookup.value.email)) list.push({ email: e, owner: 'Main email' })
  for (const c of lookup.value.contacts) for (const e of c.emails) list.push({ email: e, owner: c.name })
  const seen = new Set<string>()
  return list.filter((x) => {
    const key = x.email.toLowerCase()
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
})

const contactNames = computed(() => (lookup.value?.contacts ?? []).map(c => c.name).filter(Boolean))

const contactHint = computed(() => {
  if (form.value.contactName?.trim()) {
    return contactNames.value.some(n => n.toLowerCase() === form.value.contactName!.trim().toLowerCase())
      ? 'The email is added to this contact person'
      : 'A new contact person is created with this email'
  }
  return 'Empty: becomes the main email if the supplier has none, otherwise a new contact person'
})

const canSave = computed(() => {
  if (saving.value || !emailValid.value) return false
  if (props.mode === 'supplier') return !!domainMatch.value || !!form.value.name.trim()
  return !!targetId.value && !lookupError.value
})

const saveLabel = computed(() => {
  if (props.mode === 'email') return 'Add & Use Email'
  return domainMatch.value ? `Use ${domainMatch.value.name}` : 'Add Supplier'
})

watch(() => props.modelValue, (isOpen) => {
  if (!isOpen) return
  form.value = { name: props.mode === 'supplier' ? props.name : '', email: '', contactName: null }
  serverError.value = ''
  domainInfo.value = null
  lookup.value = null
  lookupError.value = ''
  if (props.mode === 'email') loadLookup()
})

async function loadLookup() {
  lookupLoading.value = true
  try {
    const params = props.supplierId ? `id=${props.supplierId}` : `name=${encodeURIComponent(props.name)}`
    lookup.value = await api.get<LookupResult>(`/suppliers/lookup?${params}`)
  } catch (e: any) {
    lookupError.value = e?.data?.message || `"${props.name}" is not in the Supplier Catalog yet — pick or add the supplier first.`
  } finally {
    lookupLoading.value = false
  }
}

let domainTimer: ReturnType<typeof setTimeout> | null = null
let domainSeq = 0
function onEmailChange() {
  serverError.value = ''
  if (domainTimer) clearTimeout(domainTimer)
  const email = form.value.email.trim()
  if (!EMAIL_RE.test(email)) { domainInfo.value = null; domainLoading.value = false; return }
  domainLoading.value = true
  domainTimer = setTimeout(async () => {
    const seq = ++domainSeq
    try {
      const info = await api.get<DomainInfo>(`/suppliers/domain-match?email=${encodeURIComponent(email)}`)
      if (seq === domainSeq) domainInfo.value = info?.valid ? info : null
    } catch {
      if (seq === domainSeq) domainInfo.value = null
    } finally {
      if (seq === domainSeq) domainLoading.value = false
    }
  }, 350)
}

function isCurrent(email: string) {
  return !!props.currentEmail && props.currentEmail.toLowerCase() === email.toLowerCase()
}

function useExisting(email: string) {
  if (!lookup.value) return
  emit('saved', {
    id: lookup.value.id,
    name: lookup.value.name,
    status: lookup.value.status,
    dependency: lookup.value.dependency,
    email,
    message: `Using ${email} for this item`,
  })
  emit('update:modelValue', false)
}

async function save() {
  if (!canSave.value) return
  saving.value = true
  serverError.value = ''
  const email = form.value.email.trim()
  const contactName = form.value.contactName?.trim() || null
  try {
    if (props.mode === 'supplier') {
      const res = await api.post<any>('/suppliers/quick-add', { name: form.value.name.trim(), email, contactName })
      const message = res.created
        ? `Supplier "${res.name}" added to the Catalog${res.status === 'Pending' ? ' (pending admin approval)' : ''}`
        : res.matchedBy === 'domain'
          ? `Email domain matches "${res.name}" — using that supplier${res.emailAdded ? ' and saved the email' : ''}`
          : `"${res.name}" already exists — using it${res.emailAdded ? ' and saved the email' : ''}`
      emit('saved', { id: res.id, name: res.name, status: res.status, dependency: res.dependency, email, message })
    } else {
      const res = await api.post<any>(`/suppliers/${targetId.value}/emails`, { email, contactName })
      const message = res.added
        ? `${email} added to ${res.name} (${res.target})`
        : `${email} is already saved for ${res.name}`
      emit('saved', {
        id: res.id, name: res.name, status: lookup.value?.status, dependency: lookup.value?.dependency, email, message,
      })
    }
    emit('update:modelValue', false)
  } catch (e: any) {
    serverError.value = e?.data?.message || 'Could not save — please try again'
  } finally {
    saving.value = false
  }
}

function splitEmails(raw?: string) {
  return (raw || '').split(/[,;]/).map(e => e.trim()).filter(Boolean)
}

onBeforeUnmount(() => { if (domainTimer) clearTimeout(domainTimer) })
</script>

<style scoped>
.sq-card {
  border-radius: 14px !important;
  overflow: hidden;
}
.sq-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px 16px 14px 20px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: linear-gradient(135deg, rgba(var(--v-theme-primary), 0.1), transparent 70%);
}
.sq-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(var(--v-theme-primary), 0.15);
  color: rgb(var(--v-theme-primary));
  flex-shrink: 0;
}
.sq-label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  opacity: 0.6;
  margin-bottom: 6px;
}
.sq-email-option {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 7px 10px;
  border-radius: 8px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s;
}
.sq-email-option:hover {
  border-color: rgba(var(--v-theme-primary), 0.6);
  background: rgba(var(--v-theme-primary), 0.06);
}
.sq-email-option--current {
  border-color: rgba(34, 197, 94, 0.6);
  background: rgba(34, 197, 94, 0.08);
  color: #16a34a;
}
.sq-email-addr {
  flex: 1;
  min-width: 0;
  font-size: 13px;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sq-email-owner {
  flex-shrink: 0;
  font-size: 11px;
  opacity: 0.6;
}
.sq-divider {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 14px;
  font-size: 11px;
  opacity: 0.55;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.sq-divider::before,
.sq-divider::after {
  content: '';
  flex: 1;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.sq-ignored :deep(input) {
  text-decoration: line-through;
}
</style>
