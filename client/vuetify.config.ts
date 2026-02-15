import { defineVuetifyConfiguration } from 'vuetify-nuxt-module/custom-configuration'

export default defineVuetifyConfiguration({
    icons: {
        defaultSet: 'mdi',
    },
    theme: {
        defaultTheme: 'procumentDark',
        themes: {
            procumentDark: {
                dark: true,
                colors: {
                    background: '#0D1117',
                    surface: '#161B22',
                    'surface-variant': '#1C2333',
                    primary: '#1565C0',
                    'primary-darken-1': '#0D47A1',
                    secondary: '#00BCD4',
                    'secondary-darken-1': '#0097A7',
                    accent: '#FF6D00',
                    error: '#EF5350',
                    warning: '#FF6D00',
                    info: '#29B6F6',
                    success: '#66BB6A',
                    'on-background': '#E6EDF3',
                    'on-surface': '#E6EDF3',
                },
            },
        },
    },
    defaults: {
        VCard: {
            rounded: 'lg',
            elevation: 0,
        },
        VBtn: {
            rounded: 'lg',
        },
        VTextField: {
            variant: 'outlined',
            density: 'comfortable',
            color: 'primary',
        },
        VSelect: {
            variant: 'outlined',
            density: 'comfortable',
            color: 'primary',
        },
        VDataTableServer: {
            density: 'comfortable',
        },
    },
})
