<template>
  <ClientOnly>
    <apexchart :type="mode === 'count' ? 'line' : 'area'" height="280" :options="options" :series="series" />
    <template #fallback>
      <v-skeleton-loader type="image" height="280" />
    </template>
  </ClientOnly>
</template>

<script setup lang="ts">
import { biCount, biMoney, type BiBucket, type BiTrendPoint } from '~/utils/bi'
import { BI_SERIES_COLORS } from '~/utils/biChart'

const props = defineProps<{
  trend: BiTrendPoint[]
  bucket: BiBucket
  /** count = RFQs / sent quotes / proformas; value = quoted vs bought value. */
  mode: 'count' | 'value'
}>()

const { isDark } = useAppTheme()

const series = computed(() => props.mode === 'count'
  ? [
      { name: 'RFQs', data: props.trend.map(p => p.rfqs) },
      { name: 'Sent quotes', data: props.trend.map(p => p.sentQuotes) },
      { name: 'Proforma invoices', data: props.trend.map(p => p.pis) },
    ]
  : [
      { name: 'Quoted value', data: props.trend.map(p => Number(p.sentQuoteSum)) },
      { name: 'Bought value', data: props.trend.map(p => Number(p.boughtValue)) },
    ])

function label(iso: string) {
  const d = new Date(iso)
  if (props.bucket === 'month') return d.toLocaleDateString(undefined, { month: 'short', year: '2-digit', timeZone: 'UTC' })
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' })
}

const options = computed(() => {
  const dark = isDark.value
  const palette = dark ? BI_SERIES_COLORS.dark : BI_SERIES_COLORS.light
  const colors = props.mode === 'count' ? [palette.rfq, palette.quote, palette.pi] : [palette.quote, palette.pi]
  const muted = dark ? '#8B949E' : '#64748b'
  const fmt = props.mode === 'count' ? (v: number) => biCount(Math.round(v)) : (v: number) => biMoney(v, true)
  const fewPoints = props.trend.length <= 40
  return {
    chart: {
      background: 'transparent',
      foreColor: muted,
      toolbar: { show: false },
      zoom: { enabled: false },
      animations: { enabled: props.trend.length <= 120, speed: 400 },
      fontFamily: 'inherit',
    },
    theme: { mode: dark ? 'dark' : 'light' },
    colors,
    stroke: { width: 2, curve: 'smooth' },
    fill: props.mode === 'value'
      ? { type: 'gradient', gradient: { shadeIntensity: 0, opacityFrom: 0.22, opacityTo: 0.02, stops: [0, 100] } }
      : { opacity: 1 },
    markers: { size: fewPoints ? 4 : 0, strokeWidth: 2, strokeColors: dark ? '#161B22' : '#FFFFFF', hover: { size: 6 } },
    dataLabels: { enabled: false },
    grid: { borderColor: dark ? '#21262d' : '#e2e8f0', strokeDashArray: 4, xaxis: { lines: { show: false } } },
    legend: { position: 'top', horizontalAlign: 'left', fontSize: '12px', labels: { colors: muted }, markers: { size: 6 } },
    xaxis: {
      categories: props.trend.map(p => label(p.start)),
      tickAmount: Math.min(props.trend.length, 12),
      labels: { rotate: 0, hideOverlappingLabels: true, style: { fontSize: '11px' } },
      axisBorder: { show: false },
      axisTicks: { show: false },
      tooltip: { enabled: false },
    },
    yaxis: { min: 0, forceNiceScale: true, labels: { formatter: fmt, style: { fontSize: '11px' } } },
    tooltip: {
      theme: dark ? 'dark' : 'light',
      shared: true,
      intersect: false,
      x: {
        formatter: (_: unknown, ctx: { dataPointIndex: number }) => {
          const p = props.trend[ctx.dataPointIndex]
          if (!p) return ''
          const prefix = props.bucket === 'week' ? 'Week of ' : ''
          return prefix + new Date(p.start).toLocaleDateString(undefined, {
            day: props.bucket === 'month' ? undefined : 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC',
          })
        },
      },
      y: { formatter: props.mode === 'count' ? (v: number) => biCount(v) : (v: number) => biMoney(v) },
    },
  }
})
</script>
