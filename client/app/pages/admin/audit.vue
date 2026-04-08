<template>
  <v-container class="pa-4">
    <v-row>
      <v-col cols="12">
        <v-card class="glass-card">
          <v-card-title class="d-flex align-center justify-space-between py-3 px-4">
            <div class="d-flex align-center gap-2">
              <v-icon icon="mdi-timeline-clock-outline" color="primary" />
              <span class="text-h6">System Activity</span>
            </div>
          </v-card-title>
          <v-divider />
          <v-card-text class="pa-4">
            <BusinessAuditViewer :show-all-only="true" />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup lang="ts">
definePageMeta({
  layout: 'default'
})
</script>
