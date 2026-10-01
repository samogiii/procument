/**
 * Shipping — customer bases per user.
 *
 * Each user is granted the customer bases they work with in Menu Access (features
 * shippingBase1 … shippingBase5 — no role grants them and SuperAdmin does not bypass).
 * The API returns only lines in those bases; a line with no customer (e.g. a Stock PO)
 * counts as Base 1. Mirrors ShippingBaseAccess on the backend.
 *
 *   Base 1      — ships with a T-ID and SO#.
 *   Bases 2–5   — ship without T-ID / SO#, or are collected by the customer at the
 *                 warehouse (PickUp), which finishes the shipping right away.
 */
export const SHIPPING_MAX_BASE = 5
export const SHIPPING_BASE_FEATURES = Array.from({ length: SHIPPING_MAX_BASE }, (_, i) => `shippingBase${i + 1}`)

/** The base a line is shown under. No customer → Base 1. */
export const effectiveBase = (base: number | null | undefined): number => base ?? 1

/** Only Base 1 shipments carry T-ID / SO#. */
export const baseUsesTidSo = (base: number | null | undefined): boolean => effectiveBase(base) === 1

/** Bases 2–5 may be picked up by the customer at the warehouse. */
export const baseCanPickUp = (base: number | null | undefined): boolean => effectiveBase(base) >= 2

/** A shipment note shows T-ID / SO# when it holds Base 1 parts (or no parts yet). */
export function noteUsesTidSo(note: any): boolean {
  const tracks: any[] = note?.trackNumbers ?? []
  return !tracks.length || tracks.some(t => baseUsesTidSo(t.customerBase))
}

export function useShippingBases() {
  const authStore = useAuthStore()
  const bases = computed(() => authStore.shippingBases)
  return {
    bases,
    /** The user works Base 1, so T-ID / SO# columns and the By T-ID views apply. */
    seesBase1: computed(() => bases.value.includes(1)),
  }
}
