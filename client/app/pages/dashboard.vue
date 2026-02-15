<template>
  <div>
    <h1 class="text-h5 font-weight-bold mb-6">Dashboard</h1>

    <!-- Stats Row -->
    <v-row class="mb-6">
      <v-col v-for="stat in stats" :key="stat.title" cols="12" sm="6" md="3">
        <v-card class="glass-card pa-4">
          <div class="d-flex align-center">
            <v-avatar :color="stat.color" size="48" rounded="lg" class="mr-4">
              <v-icon :icon="stat.icon" size="24" />
            </v-avatar>
            <div>
              <p class="text-h5 font-weight-bold">{{ stat.value }}</p>
              <p class="text-caption text-medium-emphasis">{{ stat.title }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Quick Actions -->
    <v-row>
      <v-col cols="12" md="8">
        <v-card class="glass-card">
          <v-card-title class="d-flex align-center">
            <v-icon icon="mdi-clock-outline" class="mr-2" size="20" />
            Recent Activity
          </v-card-title>
          <v-card-text>
            <v-list bg-color="transparent">
              <v-list-item
                v-for="item in recentActivity"
                :key="item.id"
                :subtitle="item.time"
                :prepend-icon="item.icon"
              >
                <v-list-item-title>{{ item.text }}</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" md="4">
        <v-card class="glass-card">
          <v-card-title class="d-flex align-center">
            <v-icon icon="mdi-lightning-bolt" class="mr-2" size="20" />
            Quick Actions
          </v-card-title>
          <v-card-text>
            <v-btn color="primary" block class="mb-2" prepend-icon="mdi-plus" to="/rfqs">
              New RFQ
            </v-btn>
            <v-btn color="secondary" variant="outlined" block class="mb-2" prepend-icon="mdi-plus" to="/quotes">
              New Quote
            </v-btn>
            <v-btn color="accent" variant="outlined" block prepend-icon="mdi-plus" to="/purchase-orders">
              New PO
            </v-btn>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
const stats = ref([
  { title: 'Open RFQs', value: '—', icon: 'mdi-file-document-outline', color: 'primary' },
  { title: 'Pending Quotes', value: '—', icon: 'mdi-currency-usd', color: 'secondary' },
  { title: 'Active POs', value: '—', icon: 'mdi-package-variant-closed', color: 'accent' },
  { title: 'Unpaid Invoices', value: '—', icon: 'mdi-receipt-text-outline', color: 'error' },
])

const recentActivity = ref([
  { id: 1, text: 'Welcome to Procument!', time: 'Connect your API to see real data', icon: 'mdi-information-outline' },
])
</script>
