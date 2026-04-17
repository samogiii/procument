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
