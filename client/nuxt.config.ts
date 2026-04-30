// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-05-15',
  devtools: { enabled: true },
  ssr: false,

  modules: [
    'vuetify-nuxt-module',
    '@pinia/nuxt',
  ],

  vuetify: {
    vuetifyOptions: './vuetify.config.ts',
  },

  css: [
    '@mdi/font/css/materialdesignicons.css',
    '~/assets/styles/main.css',
  ],

  runtimeConfig: {
    public: {
      apiBase: 'https://localhost:7024/api',
      // apiBase: 'http://192.168.54.2:3333/api'
    },
  },
})
