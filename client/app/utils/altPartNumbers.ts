/**
 * Alt P/N filler: users type "-", "_" or "no" (and the like) to mean "no alternative" (Sam).
 * Same rule as the server's AltPartNumbers, which also drops them on save.
 */

// Compared upper case without whitespace, so "no alt" = "NOALT".
const PLACEHOLDER_ALTS = new Set(['NO', 'N/A', 'NA', 'NONE', 'NOALT', 'SAME'])

/** Blank, only dashes / underscores ("-", "__"), or a "no alternative" word. */
export function isPlaceholderAlt(v: unknown): boolean {
  const key = String(v ?? '').replace(/\s+/g, '').toUpperCase()
  return !key || /^[-_]+$/.test(key) || PLACEHOLDER_ALTS.has(key)
}

/** The trimmed Alt P/N, or null when it is blank or filler. */
export function cleanAlt(v: unknown): string | null {
  return isPlaceholderAlt(v) ? null : String(v).trim()
}

/** An Alt list, trimmed, without blank or filler entries. */
export function cleanAltList(list: readonly unknown[] | null | undefined): string[] {
  return (list ?? []).map(a => String(a ?? '').trim()).filter(a => !isPlaceholderAlt(a))
}
