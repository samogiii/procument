/**
 * BI chart colors — one fixed hue per entity so RFQs, quotes and proformas keep their color in
 * every chart (validated categorical slots 1–3, stepped separately for the dark surface).
 */
export const BI_SERIES_COLORS = {
    light: { rfq: '#2a78d6', quote: '#eb6834', pi: '#1baf7a' },
    dark: { rfq: '#3987e5', quote: '#d95926', pi: '#199e70' },
} as const
