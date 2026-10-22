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
            procumentPistachio: {
                dark: false,
                colors: {
                    background: '#FBF5DC',   // soft butter cream page bg
                    surface: '#FFFBEA',      // warm cream for cards/dialogs
                    'surface-variant': '#F3EBC4', // pale yellow variant
                    'on-surface-variant': '#2E2A12',
                    primary: '#6E7E14',      // deep pistachio green (palette #A5B021, darkened for contrast)
                    'primary-darken-1': '#556310',
                    secondary: '#8A3049',    // pistachio-skin plum
                    'secondary-darken-1': '#6E2439',
                    accent: '#F4BD3A',       // golden
                    error: '#C62828',
                    warning: '#E0A21C',
                    info: '#4A7C8C',
                    success: '#4F7A1F',
                    'on-background': '#2B2810', // dark olive-brown for body text
                    'on-surface': '#2B2810',
                    tooltip: '#8A3049',
                    'on-tooltip': '#FFF8E1',
                },
            },
            procumentPistachioDark: {
                dark: true,
                colors: {
                    background: '#181A0E',   // near-black olive page bg
                    surface: '#222416',      // dark olive for cards/dialogs
                    'surface-variant': '#2D2F1D', // lifted olive variant
                    'on-surface-variant': '#EDE6C4',
                    primary: '#B5C12E',      // bright pistachio green (palette #A5B021, lifted for dark bg)
                    'primary-darken-1': '#8F9A1A',
                    secondary: '#C2566F',    // plum, lightened to read on dark
                    'secondary-darken-1': '#A3405A',
                    accent: '#F4BD3A',       // golden
                    error: '#EF5350',
                    warning: '#F4BD3A',
                    info: '#6FB3C4',
                    success: '#8BC34A',
                    'on-background': '#F3EDD3', // cream body text
                    'on-surface': '#F3EDD3',
                    tooltip: '#8A3049',
                    'on-tooltip': '#FFF8E1',
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
            fixedHeader: true,
            height: '65vh',
        },
        VDataTable: {
            fixedHeader: true,
            height: '65vh',
        },
        VTooltip: {
            color: 'tooltip',
        },
    },
})
