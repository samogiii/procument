<template>
  <NuxtLayout name="blank">
    <v-card class="glass-card pa-8" width="420" max-width="90vw">
      <div class="text-center mb-6">
        <v-icon icon="mdi-airplane" color="primary" size="48" />
        <h1 class="text-h5 font-weight-bold mt-3 text-gradient">Procument</h1>
        <p class="text-body-2 text-medium-emphasis mt-1">Aviation Procurement Management</p>
      </div>

      <v-form @submit.prevent="handleLogin">
        <v-text-field
          v-model="form.email"
          label="Email"
          prepend-inner-icon="mdi-email-outline"
          type="email"
          :error-messages="error ? ' ' : ''"
          class="mb-2"
        />
        <v-text-field
          v-model="form.password"
          label="Password"
          prepend-inner-icon="mdi-lock-outline"
          :type="showPassword ? 'text' : 'password'"
          :append-inner-icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
          :error-messages="error || ''"
          @click:append-inner="showPassword = !showPassword"
          class="mb-4"
        />
        <v-btn
          type="submit"
          color="primary"
          size="large"
          block
          :loading="loading"
        >
          Sign In
        </v-btn>
      </v-form>
    </v-card>
  </NuxtLayout>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const authStore = useAuthStore()
const api = useApi()

const form = ref({ email: '', password: '' })
const showPassword = ref(false)
const loading = ref(false)
const error = ref('')

async function handleLogin() {
  loading.value = true
  error.value = ''
  try {
    const res = await api.post<any>('/auth/login', form.value)
    authStore.setUser(res)
    await authStore.loadMenuPermissions()
    await navigateTo('/dashboard')
  } catch (e: any) {
    error.value = e?.data?.message || 'Invalid email or password'
  } finally {
    loading.value = false
  }
}
</script>
