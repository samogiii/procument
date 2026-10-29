<template>
  <ClientOnly>
    <apexchart type="bar" :height="height" :options="options" :series="series" />
    <template #fallback>
      <v-skeleton-loader type="image" :height="height" />
    </template>
  </ClientOnly>
</template>

<script setup lang="ts">
import { biCustomerLabel, biMoney, type BiCustomerRow } from '~/utils/bi'
import { BI_SERIES_COLORS } from '~/utils/biChart'

/** Top customers by bought value — a single series, so no legend; the card title names it. */
const props = withDefaults(defineProps<{ customers: BiCustomerRow[]; limit?: number }>(), { limit: 10 })

const { isDark } = useAppTheme()

const top = computed(() => props.customers
  .filter(c => c.boughtValue > 0)
  .slice()
  .sort((a, b) => b.boughtValue - a.boughtValue)
  .slice(0, props.limit))

const height = computed(() => Math.max(160, top.value.length * 34 + 40))

const series = computed(() => [{ name: 'Bought value', data: top.value.map(c => Number(c.boughtValue)) }])

const options = computed(() => {
  const dark = isDark.value
  const muted = dark ? '#8B949E' : '#64748b'
  return {
    chart: { background: 'transparent', foreColor: muted, toolbar: { show: false }, animations: { speed: 400 }, fontFamily: 'inherit' },
    theme: { mode: dark ? 'dark' : 'light' },
    colors: [dark ? BI_SERIES_COLORS.dark.pi : BI_SERIES_COLORS.light.pi],
    plotOptions: { bar: { horizontal: true, barHeight: '62%', borderRadius: 4, borderRadiusApplication: 'end', dataLabels: { position: 'top' } } },
    dataLabels: {
      enabled: true,
      formatter: (v: number) => biMoney(v, true),
      offsetX: 6,
      textAnchor: 'start',
      style: { fontSize: '11px', fontWeight: 500, colors: [dark ? '#E6EDF3' : '#1A2332'] },
    },
    grid: { borderColor: dark ? '#21262d' : '#e2e8f0', strokeDashArray: 4, yaxis: { lines: { show: false } } },
    legend: { show: false },
    xaxis: {
      categories: top.value.map(c => biCustomerLabel(c)),
      labels: { formatter: (v: string | number) => biMoney(Number(v), true), style: { fontSize: '11px' } },
      axisBorder: { show: false },
      axisTicks: { show: false },
    },
    yaxis: { labels: { maxWidth: 200, style: { fontSize: '12px' } } },
    tooltip: {
      theme: dark ? 'dark' : 'light',
      y: {
        formatter: (v: number, ctx: { dataPointIndex: number }) => {
          const c = top.value[ctx.dataPointIndex]
          return c ? `${biMoney(v)} · profit ${biMoney(c.profit)}` : biMoney(v)
        },
      },
    },
  }
})
</script>
