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
                    'on-surface-variant': '#C9D1D9',
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
                    tooltip: '#8B0000',
                    'on-tooltip': '#FFFFFF',
                },
            },
            procumentLight: {
                dark: false,
                colors: {
                    background: '#F5F7FA',
                    surface: '#FFFFFF',
                    'surface-variant': '#EEF2F6',
                    'on-surface-variant': '#1A2332',
                    primary: '#1565C0',
                    'primary-darken-1': '#0D47A1',
                    secondary: '#0097A7',
                    'secondary-darken-1': '#00838F',
                    accent: '#E65100',
                    error: '#D32F2F',
                    warning: '#E65100',
                    info: '#0288D1',
                    success: '#2E7D32',
                    'on-background': '#1A2332',
                    'on-surface': '#1A2332',
                    tooltip: '#FFCDD2',
                    'on-tooltip': '#000000',
                },
            },
            procumentFrost: {
                dark: false,
                colors: {
                    background: '#B8D4E3',   // medium ice-blue page bg — no white
                    surface: '#CADED9',      // blue-tinted surface for cards/dialogs
                    'surface-variant': '#A8C8DA', // darker blue-gray variant
                    'on-surface-variant': '#0A1E2E',
                    primary: '#155F87',      // strong dark teal-blue
                    'primary-darken-1': '#0E4669',
                    secondary: '#4AAEC8',
                    'secondary-darken-1': '#2D95B0',
                    accent: '#7CC5DC',
                    error: '#C0392B',
                    warning: '#D4700A',
                    info: '#4AAEC8',
                    success: '#1E6E3A',
                    'on-background': '#071525', // very dark navy for body text
                    'on-surface': '#071525',
                    tooltip: '#155F87',
                    'on-tooltip': '#E8F4FA',
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
        VTooltip: {
            color: 'tooltip',
        },
    },
})
