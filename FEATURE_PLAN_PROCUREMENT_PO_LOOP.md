# Plan — Procurement ↔ PO rejection loop

**Status:** Design — do NOT implement until frontend Procurement UI is landed and the open questions below are answered.

**Date:** 2026-04-24

---

## The loop (from user)

```
Invoice Accepted
     │
     ▼
Procurement created ──► Admin assigns item(s) to User
     │                         │
     │                   User sources suppliers / adjusts prices
     │                         │
     ▼                         ▼
Admin reviews & Finalizes ──► POItems dropped (unassigned)
     │
     ▼
Admin groups POItems → PO created & assigned to another User
     │
     ▼
User tries to buy ─── supplier unavailable / price changed / blocked
     │                         │
     │                         ▼
     │                  User requests REJECT on PO
     │                         │
     │                         ▼
     │                  Admin confirms reject
     │                         │
     │                         ▼
     │                  POItems flow BACK into Procurement
     │                         │
     └─────────────────────────┘     (loop — same Procurement re-opens)
     │
     ▼  (happy path)
PO Completed  ─── terminal, loop ends
```

---

## What already exists

- `PurchaseOrder.Status` supports `Cancelled` (and the full pipeline Waiting For Admin Approval → Waiting For Payment → Payment Done → Ship... → Completed → Cancelled).
- `PurchaseOrder.AdminApproval` (`Pending` / `Approved` / `Rejected`) + `PaymentApproval` (`Pending` / `Accepted` / `Rejected`).
- `POItem.SourceProcurementItemId` — the FK we just added. **This is the key to the loop** — every POItem knows which Procurement item it came from.
- `Procurement.Status`: `Open` / `InProgress` / `Finalized` / `Cancelled`.
- `ProcurementItem.ItemStatus`: `Open` / `Sourcing` / `Ready` / `Cancelled`.
- Per-entity user assignments via `EntityPermission(EntityName, EntityId)`.

---

## Proposed design

### 1. New PO rejection semantics

Today rejection is just a status change. For this loop, we need a **distinct rejection that recycles items** (different from `Cancelled`, which should remain terminal). Options:

- **A — new PO status `Returned`** (preferred): a PO moves to `Returned` when the assigned user says "I can't buy this." This triggers the recycling logic. `Cancelled` stays terminal (used when the admin kills the PO without re-sourcing).
- **B — reuse `Rejected` on `AdminApproval`**: overloads existing semantics. Skip this — it confuses the approval audit trail.
- **C — flag on the PO**: `RecycleRequested: bool`. Extra state; less clean than A.

→ **Go with A.** New status `Returned`. Reasons captured in a new field `ReturnReason`.

### 2. Procurement lifecycle — new `Reopened` state

Currently `Finalized` is terminal. That's wrong for the loop — finalizing drops POItems but the Procurement may need to come back to life.

New status transitions:
```
Open → InProgress → Finalized ─────────────┐
                       │                   │
                       │ (PO Returned)     ▼
                       └────► Reopened → InProgress → Finalized (again)
                                 │
                                 │ (admin gives up)
                                 ▼
                              Cancelled
```

`Reopened` behaves exactly like `InProgress` for editing purposes, but the distinction lets the UI show "this came back from a rejected PO — here's why" banners and we can count/filter loop iterations.

### 3. ProcurementItem loop state

Add a new ItemStatus: `Returned`. Set when its POItem is recycled. Also add:

- `ProcurementItem.LoopCount: int` — increments on each Returned cycle. Caps at say 5 (guard against infinite loops from misuse).
- `ProcurementItem.LastReturnReason: string?`
- `ProcurementItem.LastReturnedAt: DateTime?`

### 4. Recycling logic (the critical piece)

When admin confirms PO return (new endpoint `POST /purchase-orders/{id}/return` with `{ reason, itemIds?: long[] }`):

For each POItem being returned:
1. Look up `SourceProcurementItemId`. If null (legacy/no-trace), skip with a warning.
2. Load the `ProcurementItem`. Flip its `ItemStatus` to `Open` (fresh round), increment `LoopCount`, stamp `LastReturnReason` + `LastReturnedAt`.
3. Flip the parent `Procurement.Status` from `Finalized` to `Reopened`.
4. **POItem fate — proposed: soft-delete + audit row**
   - `POItem` gets a new `ReturnedAt: DateTime?` + `ReturnedFromPOId: long?` and is **unlinked** from its PO (`POId = null`) rather than deleted outright — preserves the audit trail.
   - Or simpler: just delete the POItem. The Procurement snapshot is the source of truth, so re-finalize creates a fresh POItem anyway.
   - **Preferred: soft delete** — queries on `/purchase-orders/unassigned-items` must filter out `ReturnedAt != null`.

5. The PO itself: mark `Status = "Returned"`, `ReturnReason`, `ReturnedAt`, `ReturnedByUserId`. If ALL items are returned → PO is fully returned. If partial → PO stays open with remaining items; returned items are gone from it.

6. Notify the Procurement's assigned users ("this item came back from PO-X, reason: ...").

### 5. Partial vs. full return

User question: can the user return a single line on a PO, or only the whole PO?

- **Per-item return** is more useful (supplier might fulfil 2 of 3 lines).
- Endpoint shape: `POST /purchase-orders/{id}/return` body: `{ reason, itemIds?: long[] }`. Omit `itemIds` → full return. Provide → partial.
- When partial, the PO stays in its current status with the remaining items; it doesn't get `Status = Returned`. Only the removed POItems' Procurement parents re-open.

### 6. Who can trigger what

| Action | Role |
|---|---|
| User requests return (creates a "return request" flag on the PO) | Any assigned PO user |
| Admin confirms return (runs the recycling logic) | Admin / SuperAdmin |
| Direct admin return (skip request step) | Admin / SuperAdmin |

Simpler v1: **no request step** — assigned user calls the return endpoint directly, no admin confirmation needed. Audit log captures who did it. Add the request/confirm dance later if spam becomes a problem.

### 7. Terminal states (no more loop)

- PO `Completed` → loop ends. `SourceProcurementItem` can be left as-is; its `ItemStatus = Ready` from the last finalize. Optional: stamp `FulfilledByPOItemId` on the ProcurementItem for traceability.
- Procurement `Cancelled` → loop ends, items dead.
- `LoopCount >= 5` → backend rejects the return and tells admin to Cancel instead. Tunable.

---

## Data-model deltas

### `Procument.Module.Purchasing.Entities.PurchaseOrder`

```csharp
public string? ReturnReason { get; set; }
public DateTime? ReturnedAt { get; set; }
public long? ReturnedByUserId { get; set; }
// Status string gains a new literal: "Returned"
```

### `Procument.Module.Purchasing.Entities.POItem`

```csharp
public DateTime? ReturnedAt { get; set; }        // soft-delete marker for recycled POItems
public long? ReturnedFromPOId { get; set; }      // audit: the PO this was on when it was returned
public string? ReturnReason { get; set; }
```

Queries on `/purchase-orders/unassigned-items` and the "Warehouse/Vendor/Customer" tabs must add `where ReturnedAt == null`.

### `Procument.Module.Purchasing.Entities.ProcurementItem`

```csharp
public int LoopCount { get; set; }              // default 0
public string? LastReturnReason { get; set; }
public DateTime? LastReturnedAt { get; set; }
public long? FulfilledByPOItemId { get; set; }  // set when the PO that consumed this is Completed
```

ItemStatus literal gains `Returned`.

### `Procument.Module.Purchasing.Entities.Procurement`

Status literal gains `Reopened`. No schema change.

### Migration name: `AddProcurementPoReturnLoop`

Index recommendations:
- `POItems(ReturnedAt)` — speeds up the "exclude returned" filter
- `ProcurementItems(LoopCount)` — optional, for reporting

---

## Endpoints

### Purchasing

| Method | Route | Body | Description |
|---|---|---|---|
| `POST` | `/purchase-orders/{id}/return` | `{ reason: string, itemIds?: long[] }` | Mark PO (or subset) as returned. Triggers recycling. Admin+assigned PO users. |
| `PATCH` | `/purchase-orders/{id}/complete` | — | Existing status flow — when transitioning to Completed, stamp `FulfilledByPOItemId` on each source ProcurementItem. |

### Procurement service additions

- New internal method `RecyclePOItemsAsync(List<long> poItemIds, string reason, long userId)` — the core logic.
- `GetByIdAsync` response: include the new loop fields so the UI can render "this has been through 2 rejection cycles" badges and the reason history.

---

## UI (deferred to frontend phase)

- PO detail page: new `[Return PO]` action for assigned users + admin. Prompts for reason.
- PO list: new chip color for `Returned` status.
- Procurement list: badge items where `LoopCount > 0` so admin sees "this one has been bounced back".
- Procurement detail: show `LastReturnReason` + `LastReturnedAt` prominently on any item with `LoopCount > 0`.
- Invoice page: no change.

---

## Open questions — need your answers before I start coding

1. **Partial returns:** per-item return (my recommendation above) or whole-PO only?
2. **Approval step:** does user "request return" and admin approves, or user just returns directly?
3. **POItem on return:** soft-delete with `ReturnedAt`, or hard delete?
   - Soft-delete keeps history; queries get a bit uglier.
   - Hard delete is cleaner; history lives only in ProcurementItem's `LastReturnReason`.
4. **Loop cap:** Should a ProcurementItem be capped at N return cycles, or allowed infinite? If capped, N = ?
5. **`Returned` POs:** should they stay visible on `/purchase-orders` (filterable) or hide by default like Cancelled?
6. **Audit:** do we need a separate `ProcurementLoopHistory` table (one row per cycle with reason + POId + timestamps), or is single-latest data on the ProcurementItem enough?
7. **Notifications:** on return, notify Procurement-assigned users — email or in-app only?
8. **When PO is marked `Cancelled` (existing):** do those items ALSO recycle back into Procurement, or does `Cancelled` stay terminal and only `Returned` recycles? (My proposal: `Cancelled` stays terminal; admin explicitly chooses `Return` to re-open.)

Drop answers inline and I'll turn this into a migration + service changes next session.

— Sami
