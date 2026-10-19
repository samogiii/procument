// Derives the lead time we quote to the customer (Sell LeadTime) from the supplier's lead time
// (Buy LeadTime). Stock counts as 0 weeks, days round up to whole weeks, months count as 4 weeks.
//   Default:          A-B weeks sells as (A+1)-(B+2) weeks — Stock → 1-2, 1 → 2-3, 1-2 → 2-4, 3-5 → 4-7.
//   Base 1 customers: A-B weeks sells as (A+4)-(B+4) weeks — Stock → 4, 1 → 5, 1-3 → 5-7.
// Mirrors LeadTimeRules.cs on the backend.
const STOCK_PATTERN = /\b(stock|stk)\b/i
const RANGE_PATTERN = /(\d+(?:\.\d+)?)\s*(?:(?:-|–|to|~)\s*(\d+(?:\.\d+)?))?\s*(days?|d|weeks?|wks?|w|months?|mos?)\b/i

function toWeeks(value: number, unit: string) {
  if (unit.startsWith('d')) return Math.ceil(value / 7)
  if (unit.startsWith('m')) return Math.ceil(value * 4)
  return Math.ceil(value)
}

function format(from: number, to: number, customerBase?: number | null) {
  if (Number(customerBase) === 1) {
    const a = from + 4
    const b = to + 4
    return a === b ? `${a} Weeks` : `${a}-${b} Weeks`
  }
  return `${from + 1}-${to + 2} Weeks`
}

/** Suggested Sell LeadTime for a buy lead time, or '' when it can't be parsed. */
export function toSellLeadTime(buyLeadTime?: string | null, customerBase?: number | null): string {
  if (!buyLeadTime?.trim()) return ''
  if (STOCK_PATTERN.test(buyLeadTime)) return format(0, 0, customerBase)
  const m = RANGE_PATTERN.exec(buyLeadTime)
  if (!m) return ''
  const unit = m[3]!.toLowerCase()
  const a = toWeeks(Number(m[1]), unit)
  const b = m[2] ? toWeeks(Number(m[2]), unit) : a
  return format(Math.min(a, b), Math.max(a, b), customerBase)
}

/** True when the sell value is what some rule suggests for the buy value (auto-filled, not typed). */
function isAutoSellLeadTime(sell: string, buy?: string | null) {
  return sell === toSellLeadTime(buy) || sell === toSellLeadTime(buy, 1)
}

/**
 * Sets a quote row's buy lead time and keeps its Sell LeadTime in step — unless the user has
 * typed their own sell value (i.e. it doesn't match what the old buy lead time suggested).
 */
export function setBuyLeadTime(
  quote: { leadTime?: string | null; sellLeadTime?: string | null },
  value: string,
  customerBase?: number | null,
) {
  const keepCustom = !!quote.sellLeadTime && !isAutoSellLeadTime(quote.sellLeadTime, quote.leadTime)
  quote.leadTime = value
  if (!keepCustom) quote.sellLeadTime = toSellLeadTime(value, customerBase)
}
