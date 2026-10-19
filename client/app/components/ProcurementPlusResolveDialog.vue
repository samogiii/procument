<template>
  <v-dialog :model-value="modelValue" max-width="980" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-account-question-outline" color="warning" class="mr-2" />
        Companies not found in the catalog
        <v-chip size="small" color="warning" variant="tonal" class="ml-2">{{ pending.length }}</v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-card-subtitle class="px-4 pb-3 text-wrap">
        These names are written in your files but no catalog supplier matched them by email, name or an earlier link.
        Pick the supplier once — the name is remembered for every future file.
      </v-card-subtitle>

      <v-card-text class="pt-0">
        <div v-if="!pending.length" class="text-center text-medium-emphasis pa-8">
          <v-icon icon="mdi-check-decagram" color="success" size="40" class="mb-2" />
          <div>Every company is linked to a catalog supplier.</div>
        </div>

        <div v-for="c in pending" :key="c.companyKey" class="resolve-card mb-3">
          <div class="d-flex flex-wrap align-start gap-3">
            <div class="resolve-card__info">
              <div class="text-caption text-medium-emphasis">Written in the file as</div>
              <div class="text-subtitle-1 font-weight-bold">{{ c.companyRaw }}</div>
              <div class="d-flex flex-wrap gap-1 mt-1">
                <v-chip
                  v-for="e in c.emails"
                  :key="e"
                  size="x-small"
                  :variant="searchOf(c) === e ? 'flat' : 'outlined'"
                  :color="searchOf(c) === e ? 'primary' : undefined"
                  prepend-icon="mdi-email-search-outline"
                  title="Find the supplier by this email's domain"
                  @click="searchBy(c, e)"
                >
                  {{ e }}
                </v-chip>
                <v-chip v-for="p in c.phones" :key="p" size="x-small" variant="outlined" prepend-icon="mdi-phone-outline">{{ p }}</v-chip>
              </div>
              <div v-if="c.comment" class="text-caption text-medium-emphasis mt-1 resolve-card__comment">“{{ c.comment }}”</div>
              <div class="text-caption mt-1">
                <strong>{{ c.rowCount }}</strong> row{{ c.rowCount === 1 ? '' : 's' }} ·
                parts: {{ c.partNumbers.join(', ') }}
                <span class="text-medium-emphasis"> · {{ c.files.join(', ') }}</span>
              </div>
            </div>
            <div class="resolve-card__pick">
              <ProcurementPlusSupplierPicker
                :key="`${c.companyKey}|${searchOf(c)}`"
                v-model="picks[c.companyKey]"
                :initial-search="searchOf(c)"
                auto-pick-domain
              />
              <div class="text-caption text-medium-emphasis mt-1">
                <template v-if="c.emails.length && searchOf(c) !== c.companyRaw">
                  Searched by the domain of {{ searchOf(c) }} —
                  <a href="#" @click.prevent="searchBy(c, c.companyRaw)">search the name instead</a>
                </template>
                <template v-else-if="c.emails.length">
                  <a href="#" @click.prevent="searchBy(c, c.emails[0]!)">search by email domain</a>
                </template>
                <template v-if="c.emails.length"> · its email{{ c.emails.length === 1 ? '' : 's' }} will be added to the supplier</template>
              </div>
              <div class="d-flex gap-2 mt-2">
                <v-btn
                  color="primary"
                  variant="flat"
                  size="small"
                  prepend-icon="mdi-link-variant"
                  :disabled="!picks[c.companyKey]"
                  :loading="busy === c.companyKey"
                  @click="link(c, picks[c.companyKey]!)"
                >
                  Link
                </v-btn>
                <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="openQuickAdd(c)">New supplier</v-btn>
              </div>
            </div>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <SupplierQuickAddDialog
      v-model="quickAdd.open"
      mode="supplier"
      :name="quickAdd.company?.companyRaw || ''"
      @saved="onQuickAdded"
    />
    <v-snackbar v-model="snack" color="success" :timeout="3000">{{ snackText }}</v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
export interface PpUnmatchedCompany {
  companyKey: string; companyRaw: string; emails: string[]; phones: string[]
  comment?: string; partNumbers: string[]; files: string[]; rowCount: number
}

const props = defineProps<{ modelValue: boolean; companies: PpUnmatchedCompany[] }>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; resolved: [] }>()

const api = useApi()
const picks = reactive<Record<string, number | null>>({})
/** What each company's picker searches: its first email (found by domain) unless the user switched to the name. */
const searches = reactive<Record<string, string>>({})
const searchOf = (c: PpUnmatchedCompany) => searches[c.companyKey] ?? c.emails[0] ?? c.companyRaw
function searchBy(c: PpUnmatchedCompany, text: string) {
  searches[c.companyKey] = text
  picks[c.companyKey] = null
}
const done = ref(new Set<string>())
const busy = ref<string | null>(null)
const snack = ref(false)
const snackText = ref('')

const pending = computed(() => props.companies.filter(c => !done.value.has(c.companyKey)))

async function link(c: PpUnmatchedCompany, supplierId: number) {
  busy.value = c.companyKey
  try {
    const res = await api.post<{ updated: number; emailsAdded: string[]; emailsOnOtherSupplier: string[] }>(
      '/procurement-plus/companies/resolve', { companyRaw: c.companyRaw, supplierId })
    done.value = new Set([...done.value, c.companyKey])
    const bits = [`${c.companyRaw} linked — ${res.updated} row${res.updated === 1 ? '' : 's'} updated`]
    if (res.emailsAdded.length) bits.push(`added ${res.emailsAdded.join(', ')} to the supplier`)
    if (res.emailsOnOtherSupplier.length) bits.push(`not added (another supplier has it): ${res.emailsOnOtherSupplier.join(', ')}`)
    snackText.value = bits.join(' · ')
    snack.value = true
    emit('resolved')
  } finally {
    busy.value = null
  }
}

const quickAdd = reactive<{ open: boolean; company: PpUnmatchedCompany | null }>({ open: false, company: null })
function openQuickAdd(c: PpUnmatchedCompany) {
  quickAdd.company = c
  quickAdd.open = true
}
async function onQuickAdded(result: { id: number }) {
  if (quickAdd.company) await link(quickAdd.company, result.id)
}

watch(() => props.modelValue, (open) => { if (open) done.value = new Set() })
</script>

<style scoped>
.resolve-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-left: 3px solid rgb(var(--v-theme-warning));
  border-radius: 10px;
  padding: 12px 14px;
}
.resolve-card__info { flex: 1 1 380px; min-width: 0; }
.resolve-card__pick { flex: 0 1 320px; min-width: 260px; }
.resolve-card__comment { font-style: italic; }
</style>
