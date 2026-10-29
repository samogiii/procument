/** Wallet transaction Base tags. A transaction carries one or more, stored comma-separated ("B1,B3"). */
export const WALLET_BASES = ['B1', 'B2', 'B3', 'B4', 'B5', 'B6', 'B7']

/** "B1,B3" → ['B1', 'B3']; empty/null → []. */
export function splitBases(value: string | null | undefined): string[] {
  return (value ?? '').split(',').map(b => b.trim()).filter(Boolean)
}

/** Column-filter values for a row: its tags, or '—' when it has none. */
export function baseFilterValues(value: string | null | undefined): string[] {
  const tags = splitBases(value)
  return tags.length ? tags : ['—']
}
