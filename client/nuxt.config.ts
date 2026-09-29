// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-05-15',
  
  // devtools: { enabled: true },
  
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
      apiMap: {
        // Frontend Host/Domain : Target Backend API URL
        'localhost:3000': 'http://localhost:5184/api',
        '192.168.3.3:3000': 'https://192.168.3.3:7024/api',
        '37.114.248.6:3000': 'http://37.114.248.6:3333/api',
        '192.168.3.55:3000': 'http://37.114.248.6:3333/api',
        '192.168.54.2:3000': 'http://192.168.54.2:3333/api',
        '10.253.0.3:3000': 'http://10.253.0.3:4444/api',
        '10.10.9.1:3000': 'http://10.10.9.1:8080/api',
        '192.168.31.8:3000': 'http://192.168.31.8:3333/api',
        'default': 'https://localhost:7024/api'
      },
    },
  },

  hooks: {
    // Internal / External Shipping (see app/composables/useShippingScope.ts): the shipping
    // pages are registered again under /internal-shipping and /external-shipping. These are
    // real routes, not aliases — Vue Router treats two aliases of one page as the same
    // location and silently drops the navigation between them.
    'pages:extend'(pages) {
      const scopedPages: Array<[original: string, sub: string]> = [
        ['/shipping', ''],
        ['/total-shipping', '/control'],
        ['/shipping/ready-for-sn', '/ready-for-sn'],
        ['/shipment-notes', '/shipment'],
        ['/shipping/track-numbers/:id()', '/track-numbers/:id()'],
      ]
      for (const [original, sub] of scopedPages) {
        const page = pages.find(p => p.path === original)
        if (!page) continue
        for (const scope of ['internal', 'external']) {
          pages.push({ name: `${scope}-${String(page.name)}`, path: `/${scope}-shipping${sub}`, file: page.file })
        }
      }
    },
  },
})