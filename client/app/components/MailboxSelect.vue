<template>
  <v-select
    :model-value="modelValue ?? ''"
    :items="items"
    item-title="title"
    item-value="value"
    label="Mailbox"
    density="compact"
    variant="outlined"
    hide-details
    prepend-inner-icon="mdi-email-multiple-outline"
    :loading="loading"
    style="max-width: 340px; min-width: 240px;"
    @update:model-value="(v: string | null) => emit('update:modelValue', v || null)"
  />
</template>

<script setup lang="ts">
/**
 * Email Quotes / Email RFQs: which MailFlow mailbox to show. MailFlow sends the mailbox each email
 * was read from; "-" = lines imported before it did. null = every mailbox.
 */
const props = defineProps<{ modelValue: string | null; apiBase: string }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: string | null): void }>()

type MailboxOption = { mailbox: string; label: string; lines: number }

const api = useApi()
const options = ref<MailboxOption[]>([])
const loading = ref(false)

const items = computed(() => [
  { value: '', title: 'All mailboxes' },
  ...options.value.map(m => ({
    value: m.mailbox,
    title: m.mailbox === '-' || m.label === m.mailbox ? m.label : `${m.label} — ${m.mailbox}`,
  })),
])

async function load() {
  loading.value = true
  try {
    options.value = await api.get<MailboxOption[]>(`${props.apiBase}/mailboxes`)
  } catch {
    options.value = []
  } finally {
    loading.value = false
  }
}
onMounted(load)
defineExpose({ load })
</script>
