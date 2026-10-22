<template>
  <div>
    <PageHeader title="Email Quotes">
      <template #actions><MailFlowFetchButton @finished="pendingKey++" /></template>
    </PageHeader>

    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <MailboxSelect v-model="mailbox" api-base="/pending-supplier-quotes" />
      <span v-if="myMailboxes && !myMailboxes.all && myMailboxes.mailboxes.length" class="text-caption text-medium-emphasis">
        You see emails from your mailboxes only: {{ myMailboxes.mailboxes.join(', ') }}
      </span>
      <span v-else class="text-caption text-medium-emphasis">Which MailFlow mailbox the emails came in through.</span>
    </div>

    <v-alert
      v-if="myMailboxes && !myMailboxes.all && !myMailboxes.mailboxes.length"
      type="info"
      variant="tonal"
      density="compact"
      class="mb-3"
    >
      No mailboxes are set on your user, so no email quotes are shown. Ask a SuperAdmin to add your email
      addresses on the Users page.
    </v-alert>

    <v-tabs v-model="tab" color="primary" class="mb-3">
      <v-tab value="pending" prepend-icon="mdi-email-alert-outline">
        Pending
        <v-badge v-if="pendingCount" :content="pendingCount" color="warning" inline />
      </v-tab>
      <v-tab value="accepted" prepend-icon="mdi-email-check-outline">Accepted</v-tab>
      <v-tab value="removed" prepend-icon="mdi-email-remove-outline">Removed</v-tab>
    </v-tabs>

    <!-- Pending stays mounted so its count badge is current on the other tabs -->
    <div v-show="tab === 'pending'">
      <p class="text-body-2 text-medium-emphasis mb-4">
        Supplier quotes MailFlow read from email. Check each line against the email, then accept it onto the
        matching RFQs (deadline no more than a week ago), edit it, or remove it. Removed emails are never imported
        again — find them on the Removed tab.
      </p>
      <PendingSupplierQuotesPanel :key="pendingKey" :mailbox="mailbox" @loaded="(n: number) => pendingCount = n" />
    </div>

    <!-- Re-created on every visit so lines accepted a moment ago are there -->
    <div v-if="tab === 'accepted'">
      <p class="text-body-2 text-medium-emphasis mb-4">
        Email quote lines already accepted onto RFQs, and who accepted them.
      </p>
      <AcceptedSupplierQuotesPanel :mailbox="mailbox" />
    </div>

    <!-- Re-created on every visit so lines removed a moment ago are there -->
    <div v-if="tab === 'removed'">
      <p class="text-body-2 text-medium-emphasis mb-4">
        Email quote lines that were removed, and who removed them. Restore a line to move it back to Pending,
        where it can be edited and accepted again.
      </p>
      <RemovedSupplierQuotesPanel :mailbox="mailbox" @restored="pendingKey++" />
    </div>
  </div>
</template>

<script setup lang="ts">
// Access: Menu Access "Email Quotes" (route gate in middleware/auth.global.ts, nav in layouts/default.vue).
const tab = ref<'pending' | 'accepted' | 'removed'>('pending')
const pendingCount = ref(0)
/** Bumped when a MailFlow run finishes or removed lines are restored, so Pending reloads. */
const pendingKey = ref(0)
/** Mailbox dropdown: null = every mailbox. */
const mailbox = ref<string | null>(null)

/** The mailboxes this user may see (Users page); all = SuperAdmin. */
const api = useApi()
const myMailboxes = ref<{ all: boolean; mailboxes: string[] } | null>(null)
onMounted(async () => {
  try {
    myMailboxes.value = await api.get('/pending-supplier-quotes/my-mailboxes')
  } catch { /* the panels show their own errors */ }
})
</script>
