export function formatPrice(value: number | string | null | undefined): string {
  const num = Number(value)
  if (!num && num !== 0) return '0.00'
  return num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
