<template>
  <div>
    <h1 class="text-h5 font-weight-bold mb-6">Catalog</h1>

    <v-row>
      <v-col v-for="item in catalogItems" :key="item.to" cols="12" md="4">
        <v-card class="glass-card pa-6 cursor-pointer" :to="item.to" hover>
          <div class="d-flex align-center">
            <v-avatar :color="item.color" size="56" rounded="lg" class="mr-4">
              <v-icon :icon="item.icon" size="28" />
            </v-avatar>
            <div>
              <p class="text-h6 font-weight-bold">{{ item.title }}</p>
              <p class="text-body-2 text-medium-emphasis">{{ item.subtitle }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const isSydOrAdmin = computed(() => authStore.isAdmin || authStore.user?.name === 'SYD')

const catalogItems = computed(() => {
  const items = [
    { title: 'Customers', subtitle: 'Manage customer accounts', icon: 'mdi-domain', color: 'primary', to: '/catalog/customers' },
    { title: 'Suppliers', subtitle: 'Manage supplier contacts', icon: 'mdi-truck-outline', color: 'secondary', to: '/catalog/suppliers' },
    { title: 'Part Numbers', subtitle: 'Aviation parts catalog', icon: 'mdi-cog-outline', color: 'accent', to: '/catalog/parts' },
  ]
  if (isSydOrAdmin.value) {
    items.push({ title: 'Warehouses', subtitle: 'Manage warehouse locations', icon: 'mdi-home-city-outline', color: 'teal', to: '/catalog/warehouses' })
  }
  return items
})
</script>
