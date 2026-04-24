**From:** Sam (backend)
**To:** Gemini (frontend senior)
**Subject:** Procurement ↔ PO loop — UI bits you need to add (no backend work needed)
**Prereq:** You've already shipped the first brief — `GEMINI_FRONTEND_BRIEF_PROCUREMENTS.md` (the new `/procurements` list + detail page). This is Round 2 — the "reject a PO and it flows back into Procurement" loop.

---

## ⚠️ Ground rules (please read first)

1. **Do NOT run, build, test, or inspect the backend.** No `dotnet`, no `ef`, no DB queries, no Swagger. The backend is done, compiled, migrated, and verified on my machine. Just code the frontend against the endpoints below — trust the contracts.
2. **Do NOT touch backend files** (`src/**/*`, `.cs`, migrations, `appsettings.*`). If you think a contract is wrong, stop and ping me in chat. I will fix it.
3. **You may only touch `client/**`** (Nuxt/Vue/Vuetify frontend).
4. Use the existing patterns from `purchase-orders/[id].vue`, `procurements/[id].vue` (from the first brief), and `BulkPermissionManager.vue`. Don't introduce new libraries.

---

## What this round is about

The happy path today: Invoice → Procurement (snapshot) → Finalize → POItems → PO → status progression → Completed.

The new bit: at any point before a PO reaches `Completed` / `Cancelled` / `Returned`, an **Admin or SuperAdmin** may decide "this supplier is no good, pull these items back into Procurement so a user can re-source them." That's a **PO Return**. Items pulled back become editable `ProcurementItem`s again (ItemStatus reopens to `Open`, parent Procurement flips `Finalized` → `Reopened`), a user can tweak them, admin re-finalizes → new POItems → new POs. Loop continues until the PO is `Completed`.

Hard cap: a `ProcurementItem` can go around the loop **5 times**. After that it's skipped by the backend with a warning — admin must Cancel it instead.

---

## New backend you can consume

### 1. `POST /api/purchase-orders/{id}/return` — **admin only**

Returns the whole PO or a subset of its items back into Procurement.

**Request:**
```ts
{
  reason: string;           // required, trimmed server-side
  itemIds?: number[] | null;  // null/empty = full return (entire PO)
}
```

**Response (200 OK):**
```ts
{
  poId: number;
  fullReturn: boolean;       // true if every live item was returned
  poStatus: string;          // "Returned" on full return, otherwise unchanged
  returnedPOItemIds: number[]; // POItems soft-deleted
  reopenedProcurementIds: number[]; // parent Procurements flipped to "Reopened"
  skippedPOItemIds: number[];   // e.g. ProcurementItem hit the 5-loop cap
  warnings: string[];           // human-readable skip reasons
}
```

**400 possibilities:**
- Missing `reason`
- PO already `Completed` / `Cancelled` / `Returned`
- No live items on PO
- PO is locked by a Final Invoice

### 2. Changed response fields you'll see

**`GET /api/procurements/{id}` → each item in `items[]` now includes:**
```ts
loopCount: number;            // 0–5
lastReturnReason?: string;
lastReturnedAt?: string | null;    // ISO datetime
fulfilledByPOItemId?: number | null;  // set when this line was consumed by a Completed PO
```

**`GET /api/purchase-orders/{id}` — status field may now be `"Returned"`.** The PO header also carries (through the same response shape; fields may not yet surface in the DTO — check `.poStatus` / status chip only for now):
- PO with `status === "Returned"` is effectively read-only in the UI; hide the status action menu.

**`GET /api/purchase-orders/unassigned-items`** — now filters out items that have been returned (backend excludes `ReturnedAt != null`). No frontend change required, just FYI.

---

## Frontend work — concrete changes

### A) `client/app/pages/purchase-orders/[id].vue`

1. **Add a "Return to Procurement" button** to the header action area, visible only to `Admin` / `SuperAdmin`, and **disabled** when `po.status` is one of:
   - `Completed`, `Cancelled`, `Returned`
   - or the PO is locked (same lock flag the existing "Update Status" button respects)

2. **Clicking opens a dialog:**
   - Title: `Return PO {{ po.poNumber }} to Procurement`
   - A required `v-textarea` for `reason` (label: "Reason for return", rules: required, min 3 chars).
   - A toggle: `"Return entire PO"` (default ON) vs `"Return selected items"`.
   - When toggle OFF: render the PO's item list (only live items — filter `po.items` where backend didn't already mark them returned; treat items with no `returnedAt` as live — if the response shape doesn't include `returnedAt` for items yet, just show all items) with a checkbox per row. Must select ≥1.
   - Submit button: `"Return to Procurement"` — danger color.
   - Cancel button on the left.

3. **On submit:**
   ```ts
   const body = { reason: reason.value.trim(), itemIds: fullReturn.value ? null : selectedIds.value }
   const res = await $api.post(`/purchase-orders/${id}/return`, body)
   ```
   - On success: show a `v-snackbar` summarizing:
     - `"Returned N item(s). Reopened Procurement(s): #12, #15."`
     - If `warnings.length > 0`, append them as separate warning-style snackbar lines (or inline them in the dialog before closing).
   - Refetch the PO, redirect to `/procurements` list if `fullReturn === true`, otherwise just close the dialog and let the user continue.

4. **Status chip** — add a new color mapping so `Returned` renders as an amber/warning chip (match the visual weight of `Cancelled` but a different hue).

### B) `client/app/pages/procurements/[id].vue` (the detail page you shipped last round)

Add three small UI affordances per item row:

1. **Loop badge** — next to the status chip, render a small chip/badge showing `🔁 {{ item.loopCount }}/5` when `loopCount > 0`.
   - Color: neutral if `loopCount <= 2`, amber at 3-4, red at 5.
   - Tooltip: `"This line has been returned {loopCount} time(s). Max 5."`

2. **Last-return tooltip** — when `lastReturnedAt` is not null, show a small ⚠️ icon next to the item status. Tooltip content:
   ```
   Last returned {{ relativeTime(item.lastReturnedAt) }}
   Reason: {{ item.lastReturnReason || '(none)' }}
   ```

3. **Fulfilled lock** — when `fulfilledByPOItemId` is set, render the whole row read-only (grey out, disable edits, disable supplier-quote mutations) with a small chip saying `"Fulfilled by PO#…"`. You don't need to resolve the PO number — the POItemId is enough for now.

### C) `client/app/pages/procurements/index.vue`

1. Add a new status-color case for `"Reopened"` — render as an amber chip (different from `Open`, `InProgress`, `Finalized`, `Cancelled`). Same style weight as `Finalized` but amber.

2. Optional nice-to-have: a filter chip at the top to quickly filter by Status (Open / InProgress / Reopened / Finalized / Cancelled). Frontend-side filter against the already-paged data.

### D) Notifications (existing component)

The backend fires a notification with `type: "POReturned"` to every user with `EntityPermission` on the PO. If your notification list component has a type→icon map, add a case for `POReturned` — use a 🔁 icon or similar. The `message` field is already human-readable (`"PO PO-42 was fully returned to Procurement."`).

---

## How I recommend you debug without touching backend

- Open devtools → Network tab while clicking the return button. You'll see the exact request/response.
- If the response is 400, the JSON body will contain `{ message: "..." }` — the backend message is already descriptive. Surface it in a snackbar verbatim.
- If the backend returns `skippedPOItemIds` with warnings, surface those warnings exactly as-is — I wrote them for end-users (e.g. `"ProcurementItem 42 (PartNumber #17) reached the 5-loop cap — cancel or skip."`).
- Use existing `useApi()` composable — don't hand-roll fetch.

---

## Acceptance checklist (you sign off)

- [ ] Admin can click "Return to Procurement" on a non-terminal PO, fill a reason, and submit.
- [ ] Full-return flips PO to `Returned` status + chip; the UI disables all status actions on that PO.
- [ ] Partial return keeps the PO in its current workflow state, removes the returned items from the table, and shows a success snackbar with the count.
- [ ] Procurement list shows `Reopened` Procurement(s) with an amber chip.
- [ ] Procurement detail shows loop badges on items that have been returned before.
- [ ] Items with `fulfilledByPOItemId` set are visibly locked / un-editable on the Procurement detail page.
- [ ] 5-loop cap warnings surface to the user (don't swallow them silently).
- [ ] No regression on any existing purchase-orders or procurements page.

---

## Files likely to touch (guess — verify in your own head)

- `client/app/pages/purchase-orders/[id].vue`
- `client/app/pages/procurements/[id].vue`
- `client/app/pages/procurements/index.vue`
- Maybe a small new `<ReturnToProcurementDialog.vue>` under `client/app/components/`
- Notification type-map component if you have one

That's it. Ping me in chat if any endpoint answers surprise you — do not try to "fix" it yourself by touching the backend.

Thanks,
Sam
