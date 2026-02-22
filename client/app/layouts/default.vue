<template>
  <v-app>
    <v-navigation-drawer
      v-model="drawer"
      :rail="!mobile && rail"
      :temporary="mobile"
      :permanent="!mobile"
      color="surface"
      class="border-e-thin"
    >
      <!-- Logo -->
      <v-list-item
        :prepend-icon="(!mobile && rail) ? 'mdi-airplane' : undefined"
        class="pa-4"
        @click="mobile ? undefined : rail = !rail"
      >
        <template v-if="mobile || !rail" #default>
          <div class="d-flex align-center">
            <v-icon icon="mdi-airplane" color="primary" size="28" class="mr-3" />
            <span class="text-h6 font-weight-bold text-gradient">Procument</span>
          </div>
        </template>
      </v-list-item>

      <v-divider />

      <!-- Nav Items -->
      <v-list density="compact" nav>
        <v-list-item
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          :prepend-icon="item.icon"
          :title="item.title"
          rounded="lg"
          class="mb-1"
          active-color="primary"
          @click="mobile ? drawer = false : undefined"
        />

        <!-- Admin Section -->
        <template v-if="authStore.isAdmin">
          <v-divider class="my-2" />
          <v-list-subheader v-if="mobile || !rail">ADMIN</v-list-subheader>
          <v-list-item
            to="/users"
            prepend-icon="mdi-account-group"
            title="Users"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
        </template>
      </v-list>

      <template #append>
        <v-divider />
        <v-list-item class="pa-2">
          <template #prepend>
            <v-avatar color="primary" size="32">
              <span class="text-caption">{{ authStore.userInitials }}</span>
            </v-avatar>
          </template>
          <v-list-item-title class="text-body-2">{{ authStore.user?.name }}</v-list-item-title>
          <v-list-item-subtitle class="text-caption">{{ authStore.user?.role }}</v-list-item-subtitle>
          <template #append>
            <v-btn icon="mdi-logout" variant="text" size="small" @click="logout" />
          </template>
        </v-list-item>
      </template>
    </v-navigation-drawer>

    <!-- Top Bar -->
    <v-app-bar flat color="surface" class="border-b-thin" density="compact">
      <v-app-bar-nav-icon v-if="mobile" @click="drawer = !drawer" />
      <v-app-bar-title v-if="mobile">
        <span class="text-body-2 font-weight-bold text-gradient">Procument</span>
      </v-app-bar-title>
      <template #append>
        <v-btn icon="mdi-bell-outline" variant="text" size="small" />
        <v-btn icon="mdi-cog-outline" variant="text" size="small" class="ml-1" />
      </template>
    </v-app-bar>

    <v-main>
      <v-container fluid class="main-content">
        <slot />
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
const { mobile } = useDisplay()
const drawer = ref(!mobile.value)
const rail = ref(false)
const route = useRoute()
const authStore = useAuthStore()

watch(mobile, (isMobile) => {
  drawer.value = !isMobile
  if (isMobile) rail.value = false
})

const navItems = [
  { title: 'Dashboard', icon: 'mdi-view-dashboard', to: '/dashboard' },
  { title: 'RFQs', icon: 'mdi-file-document-outline', to: '/rfqs' },
  { title: 'Quotes', icon: 'mdi-currency-usd', to: '/quotes' },
  { title: 'Invoices', icon: 'mdi-receipt-text-outline', to: '/invoices' },
  { title: 'Purchase Orders', icon: 'mdi-package-variant-closed', to: '/purchase-orders' },
  { title: 'Catalog', icon: 'mdi-database-outline', to: '/catalog' },
]

const pageTitle = computed(() => {
  const name = route.name as string | undefined
  if (!name) return 'Procument'
  return name.charAt(0).toUpperCase() + name.slice(1).replace(/-/g, ' ')
})

async function logout() {
  authStore.logout()
  await navigateTo('/login')
}
</script>
