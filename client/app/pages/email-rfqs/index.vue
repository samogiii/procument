<template>
  <div>
    <PageHeader title="Email RFQs">
      <template #actions><MailFlowFetchButton kind="rfq" @finished="pendingKey++" /></template>
    </PageHeader>

    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <MailboxSelect v-model="mailbox" api-base="/pending-rfqs" />
      <span class="text-caption text-medium-emphasis">Which MailFlow mailbox the emails came in through.</span>
      <v-spacer />
      <EmailRfqExportButton :mailbox="mailbox" />
    </div>

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
        Customer RFQs MailFlow read from email. Check each email's lines against the email, make sure the customer
        is right (found by the sender's email or name — pick one when it isn't), then Create RFQ: a new RFQ with
        these items is made for that customer, with you as its user. Removed emails are never imported again —
        find them on the Removed tab.
      </p>
      <PendingRfqsPanel :key="pendingKey" :mailbox="mailbox" @loaded="(n: number) => pendingCount = n" />
    </div>

    <!-- Re-created on every visit so lines accepted a moment ago are there -->
    <div v-if="tab === 'accepted'">
      <p class="text-body-2 text-medium-emphasis mb-4">
        RFQ emails that were turned into RFQs, who did it, and a link to each RFQ.
      </p>
      <ReviewedRfqsPanel mode="accepted" :mailbox="mailbox" />
    </div>

    <div v-if="tab === 'removed'">
      <p class="text-body-2 text-medium-emphasis mb-4">
        RFQ email lines that were removed, and who removed them. Restore a line to move it back to Pending.
      </p>
      <ReviewedRfqsPanel mode="removed" :mailbox="mailbox" @restored="pendingKey++" />
    </div>
  </div>
</template>

<script setup lang="ts">
// Access: Menu Access "Email RFQs" (route gate in middleware/auth.global.ts, nav in layouts/default.vue).
const tab = ref<'pending' | 'accepted' | 'removed'>('pending')
const pendingCount = ref(0)
/** Bumped when a MailFlow run finishes or removed lines are restored, so Pending reloads. */
const pendingKey = ref(0)
/** Mailbox dropdown: null = every mailbox. */
const mailbox = ref<string | null>(null)
</script>
