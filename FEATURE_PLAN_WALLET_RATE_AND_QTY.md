# Feature Plan — Wallet rate corrections & QTY visibility in Total Project

Two independent features, planned together because both are small, both touch reporting, and both
can ship in one release.

- **Part A — Auto transactions in wallets:** let users correct the exchange rate of an automatic
  transaction (the wallet balance follows), and paint every *new* auto transaction yellow until
  someone has looked at it. The yellow mark can be cleared and set again.
- **Part B — Total Project (`total-pn`):** next to the ordered QTY, show how much has arrived at the
  warehouse, how much is still sitting there, and how much the supplier still owes.

Status: **not started.** Nothing in this document is implemented yet.

---

## Part A — Exchange-rate correction + "new" highlight on auto transactions

### A.1 How it works today

| Where | What it creates |
|---|---|
| `SupplierPaymentsController.Upload` (`POST /supplier-payments/po/{poId}/pop`) | The **auto withdraw**: `Type=Withdraw`, `ToType=Supplier`, `TxCurrency="USD"`, `ExchangeRate = PaymentRequest.PendingExchangeRate`, `IsAuto=true`, carries the POP file. |
| `SupplierPaymentsController.FinalizeAmount` | The bank-fee row: `ToType=BankFee`, `IsAuto=true`, amount = bank's final debit − (amount × rate). |
| `PaymentBoxService.TryAutoDepositAsync` | Customer POP deposit, `IsAuto=true`, rate only when the POP currency differs from the wallet. |
| `PaymentBoxService.TryAutoWithdrawAsync` | Present but currently unused — no caller in the solution. |

Balances are **already derived** from the rate: `PaymentBoxService.ToSummary`, `GetByIdAsync`,
`AddTransactionAsync` and `UpdateTransactionAsync` all compute `Amount * (ExchangeRate ?? 1m)`. So
correcting a rate automatically corrects the wallet balance, the running-balance column and the
per-currency breakdown — no recalculation job is needed.

Editing exists but is blunt: `PATCH /payment-boxes/{id}/transactions/{txId}` is
`[Authorize(Roles = "SuperAdmin")]`, rewrites every field on the row, and the pencil button is hidden
behind `authStore.isSuperAdmin`. There is no notion of an unreviewed transaction.

### A.2 Database — one additive migration

`PaymentTransactions` gains five nullable columns (naming follows `TrackNumberItem`, which already
uses `ReviewedAt` / `ReviewedByUserId`):

| Column | Type | Meaning |
|---|---|---|
| `ReviewedAt` | `datetime2 NULL` | `NULL` + `IsAuto` means the row is **new** (yellow). |
| `ReviewedByUserId` | `bigint NULL` → `Users` | Who cleared it. |
| `OriginalExchangeRate` | `decimal(18,6) NULL` | The rate as the system created it; written once, on the first correction. |
| `RateEditedAt` | `datetime2 NULL` | When the rate was last corrected. |
| `RateEditedByUserId` | `bigint NULL` → `Users` | Who corrected it. |

Index: `IX_PaymentTransactions_Unreviewed` on `(IsAuto, ReviewedAt)` filtered
`WHERE ReviewedAt IS NULL` — makes the "N new" badge a cheap count.

**Backfill (in the same migration):** `UPDATE PaymentTransactions SET ReviewedAt = CreatedAt WHERE
IsAuto = 1`. Without it every historical auto row turns yellow on the day we deploy and the feature
is useless noise. See decision **D1**.

### A.3 Backend

**DTOs** (`PaymentBoxDTOs.cs`) — `PaymentTransactionRow` and `AllTransactionRow` each gain: `IsNew`
(computed: `IsAuto && ReviewedAt == null`), `ReviewedAt`, `ReviewedByName`, `OriginalExchangeRate`,
`RateEditedAt`, `RateEditedByName`.

**New endpoints** on `PaymentBoxesController`:

| Verb & route | Role | Body | Returns |
|---|---|---|---|
| `PATCH /payment-boxes/{id}/transactions/{txId}/exchange-rate` | `SuperAdmin,Admin` | `{ exchangeRate, note? }` | the rebuilt row + the wallet's new balance |
| `PATCH /payment-boxes/{id}/transactions/{txId}/review` | `SuperAdmin,Admin` | `{ reviewed: bool }` | the rebuilt row |
| `GET /payment-boxes/unreviewed-count` | `SuperAdmin,Admin` | — | `{ total, byBoxId }` for the badge |

**`PaymentBoxService.UpdateExchangeRateAsync(txId, rate, userId)`:**

1. Reject `rate <= 0`. A `null` rate is only allowed when `TxCurrency` equals the wallet currency
   (1:1); a non-null rate is required when they differ — the same rule `TryAutoDepositAsync` enforces.
2. `OriginalExchangeRate ??= tx.ExchangeRate` (so the first correction keeps the system's value),
   then set `ExchangeRate`, `RateEditedAt`, `RateEditedByUserId`.
3. A correction counts as a review: set `ReviewedAt` / `ReviewedByUserId` too.
4. Bank-fee reconciliation — see A.4.
5. Save, then rebuild the row.

**Refactor while we are here:** `AddTransactionAsync`, `UpdateTransactionAsync` and the new method
each end with ~35 identical lines that reload navigations, re-walk the wallet's transactions for the
running balance and construct a `PaymentTransactionRow`. Extract one private
`BuildRowAsync(PaymentTransaction tx)` and call it from all three.

**What a rate edit must *not* touch:** `Amount`, `TxCurrency`, supplier/customer,
`PaymentRequestId`, `InvoiceId`, `PopUploadId` / `PopFileName`. Because `Amount` is untouched and
`GetSupplierPaidAmountsAsync` sums `Amount` (USD), **PR and PO payment statuses cannot shift** as a
side effect of a rate correction. Changing the amount or the currency stays in the existing
SuperAdmin full-edit dialog.

### A.4 The bank-fee rule (the one real trap)

`FinalizeAmount` records the bank's final debit as two rows:
`initial = round(Amount × ExchangeRate, 2)` on the supplier withdraw, and
`bankFee = finalWalletAmount − initial` on a sibling `ToType=BankFee` row. Correcting the rate
changes `initial` but leaves the fee untouched, so the wallet would be debited a different total
than the bank actually took.

Rule: when the edited row is a POP withdraw (`PopUploadId != null`, `ToType == "Supplier"`) and a
sibling bank-fee row exists (same `PaymentRequestId`, `ToType == "BankFee"`, `IsAuto`):

- `finalTotal = oldInitial + oldFee` — what the bank really took, and the invariant.
- `newFee = finalTotal − round(Amount × newRate, 2)`.
- `newFee > 0` → update the fee row (and stamp it reviewed too).
- `newFee == 0` → delete the fee row.
- `newFee < 0` → **reject**: "A rate above {max} would make the bank fee negative. The bank debited
  {finalTotal} {currency} in total.", where `max = finalTotal / Amount`.

The dialog previews this before saving, so the user sees the fee move.

### A.5 Frontend

Both wallet screens are plain `v-data-table`s: `client/app/pages/payment-control/[id].vue` (one
wallet's ledger) and `client/app/pages/payment-control/index.vue` (all transactions).

- **Yellow rows:** `:row-props="({ item }) => item.isNew ? { class: 'tx-row-new' } : {}"` (Vuetify
  3.11 supports `row-props`), with `.tx-row-new td { background: rgba(255,193,7,.16) }` and a dimmer
  dark-theme variant. The `tx-total-row` footer must stay unaffected.
- **Review toggle:** a per-row icon button — `mdi-flag-outline` "Mark as reviewed" on a yellow row,
  `mdi-flag-off-outline` "Mark as new" on a normal one. Optimistic update, revert on error.
- **Header chip:** `N new` — clicking it filters the table to unreviewed rows.
- **Rate cell:** the existing `#item.exchangeRate` slot gains a pencil (visible to Admin and
  SuperAdmin) opening a small **Correct exchange rate** dialog showing the transaction amount and
  currency, the current rate, the new rate, a live "wallet debit becomes X" preview, and — when a
  bank fee is linked — "bank fee becomes Y". Save → `PATCH .../exchange-rate` → replace the row and
  refresh the wallet summary so the header balance matches.
- **Corrected marker:** a small `mdi-pencil` chip next to a rate that has `originalExchangeRate`,
  tooltip "Was 3.6725 — corrected by Sam on 2026-09-24".
- The `isAuto` column filter gains a **New** value; the Excel export gains *Reviewed*, *Reviewed by*
  and *Original rate* columns.

### A.6 Business rules (Part A)

1. Yellow means `IsAuto && ReviewedAt == null`. **Manual rows are never yellow.**
2. Correcting a rate marks the row reviewed; the user can push it back to new at any time.
3. Reviewing is a per-transaction flag and is never reset by later system writes.
4. Wallet balances, per-currency breakdowns and the running-balance column all follow the corrected
   rate automatically — nothing to recompute or re-run.
5. Same-currency transactions keep a `NULL` rate (1:1); the dialog refuses a rate there.

---

## Part B — Received / in-warehouse / remaining QTY in Total Project

### B.1 How it works today

`TotalPNRowResponse.Qty` is `POItem.Qty` (falling back to the procurement line, then the invoice
line). Nothing on the row says how much of it physically arrived. A user has to open the PO, then
each track number, to find out.

The receipt truth already exists:

- **Customer POs:** `TrackNumberItems` (`ExpectedQty`, `ActualQty`, `TransferredOutQty`, `Status` =
  `Pending` / `Accepted` / `Rejected`) hanging off `POItemTrackNumbers`; a track number is packed for
  the customer once it appears in `ShipmentNoteTrackNumbers`. This is exactly what
  `ShippingService.GetReadyForSnAsync` uses.
- **Stock POs (Our Inventory):** `OurStockMovements`, already exposed as
  `IStockReservationService.GetReceivedByPoItemAsync(poItemIds)`.
- **Lines served from Our Stock** (`ProcurementItem.FromStock`, no POItem):
  `IStockReservationService.GetLineStatusAsync(invoiceItemId)` → reserved / issued.

`TrackNumberItems` is already indexed on `POItemId`, so the new aggregate is cheap.

### B.2 The numbers we show

For one Total Project row:

| Field | Definition |
|---|---|
| **Ordered** (`qty`, unchanged) | `POItem.Qty` |
| **In transit** (`inTransitQty`) | Σ `ExpectedQty` of track items still `Pending` — shipped by the supplier, not yet checked in. |
| **Received** (`receivedQty`) | Σ `ActualQty` where `Status = "Accepted"`. Rejected items count as 0. |
| **In warehouse** (`inWarehouseQty`) | Σ `(ActualQty − TransferredOutQty)` for accepted items whose track number is **not** on a Shipment Note — still physically in a warehouse, not packed for the customer. Same definition as Ready-for-SN. |
| **Remaining** (`remainingQty`) | `max(0, Ordered − Received)` — what the supplier still owes. |

Row kinds that are not a plain customer PO line:

- **Stock PO rows** (`OUR STOCK`, from §10a of the Our Inventory plan): `receivedQty` from the stock
  ledger, `remainingQty = Qty − received`, and `inWarehouseQty = null` (—). The lot is shared across
  POs, so a per-line "still in stock" number would be a guess; Our Inventory's own screens own it.
  Decision **D3**.
- **Lines served from Our Stock** (`FromStock`, no POItem): `receivedQty = issued`,
  `inWarehouseQty = reserved`, `remainingQty = max(0, Qty − issued)`. Decision **D4**.
- **No PO yet:** all four are `null`, rendered as `—`.

### B.3 Backend

`TotalPNRowResponse` gains four `int?` properties: `InTransitQty`, `ReceivedQty`, `InWarehouseQty`,
`RemainingQty`.

In `TotalPNService.GetAsync`, after the page's rows are materialised, run **one** batched query for
the page's `POItem` ids (at most one page of them) — not an extra `Include` on the already-heavy
three-way join, and never a per-row query:

```
from item in TrackNumberItems
join track in POItemTrackNumbers on item.TrackNumberId equals track.Id
where poItemIds.Contains(item.POItemId)
select new { item.POItemId, item.Status, item.ExpectedQty, item.ActualQty,
             item.TransferredOutQty,
             OnSn = ShipmentNoteTrackNumbers.Any(s => s.TrackNumberId == track.Id) }
```

then aggregate in memory into a `Dictionary<long, QtyProgress>`. Same call in `GetTotalOrderAsync`,
which feeds the same grid. `BuildStockPoRowsAsync` already calls `GetReceivedByPoItemAsync` for its
"Received x/y into Our Stock" text — reuse that dictionary for the new fields instead of querying
twice.

Sorting: add `receivedQty` and `remainingQty` to the `sortBy` switch (customer rows). Filtering: the
new columns follow the `qty` pattern — client-side value filters, so `TotalPNFilterOptions` and the
filter-options endpoint do not change.

### B.4 Frontend (`client/app/pages/total-pn/index.vue`)

- **QTY cell** keeps the ordered number and gains a compact second line, `↓3 · 1 left`, colour-coded
  (green when nothing remains, amber when partially received, grey when nothing has arrived), with a
  tooltip spelling out all five numbers. Scalars only in the slot — no arrays, per the DataListPage
  performance rules.
- **Four new optional columns** in `ALL_COLUMNS` / `TPN_COLUMNS` / the CSV + Excel export map: `In
  Transit`, `Received QTY`, `In Warehouse`, `Remaining QTY`. *Received QTY* and *Remaining QTY* are
  visible by default; the other two are available but hidden (the grid is already very wide, and
  `visibleColumns` is persisted per user). Decision **D5**.
- **Name clash:** the grid already has `Received` and `Received Date`, which are **customer money**
  (`CustomerPayment`). Relabel those two to `Received (Payment)` and `Received Date (Payment)` —
  label only, keys unchanged, so saved column layouts keep working. Decision **D6**.

---

## Epics & tasks

### Epic A1 — Database & DTOs
- [ ] Add `ReviewedAt`, `ReviewedByUserId`, `OriginalExchangeRate`, `RateEditedAt`, `RateEditedByUserId` to `PaymentTransaction`.
- [ ] Migration + filtered index `IX_PaymentTransactions_Unreviewed` + backfill `ReviewedAt = CreatedAt` for existing auto rows.
- [ ] Extend `PaymentTransactionRow` / `AllTransactionRow` with `IsNew` and the audit fields.

### Epic A2 — Rate correction (backend)
- [ ] Extract `BuildRowAsync` and reuse it in Add / Update / the new rate endpoint.
- [ ] `UpdateExchangeRateAsync` with validation (positive rate; null only for same-currency).
- [ ] Bank-fee reconciliation, including the negative-fee rejection message.
- [ ] `PATCH .../exchange-rate`, roles `SuperAdmin,Admin`.

### Epic A3 — Review flag (backend)
- [ ] `SetReviewedAsync(txId, reviewed, userId)` + `PATCH .../review`.
- [ ] `GET /payment-boxes/unreviewed-count`.

### Epic A4 — Wallet screens
- [ ] Yellow rows via `row-props` on both tables, light + dark theme, totals row unaffected.
- [ ] Per-row review toggle with optimistic update.
- [ ] "N new" header chip that filters to unreviewed.
- [ ] Correct-exchange-rate dialog with wallet-amount and bank-fee preview.
- [ ] "corrected" chip + tooltip; `New` option in the `isAuto` filter; export columns.
- [ ] Refresh the wallet summary / balance after a correction on both pages.

### Epic A5 — Tests (Part A)
- [ ] Correcting a rate changes the wallet balance and the running balance, and leaves `Amount`, PR status and PO payment status alone.
- [ ] Bank fee follows the rate; the bank's total stays constant; a too-high rate is rejected; a zero fee deletes the row.
- [ ] Review toggle flips `IsNew` both ways; a manual row is never new; the backfill leaves historical rows not-new.

### Epic B1 — Quantities (backend)
- [ ] Four fields on `TotalPNRowResponse`.
- [ ] Batched track-item aggregate; wire into `GetAsync` and `GetTotalOrderAsync`.
- [ ] Stock PO rows and `FromStock` rows per D3 / D4 (reuse the existing dictionaries; no second query).
- [ ] Sorting by received / remaining.

### Epic B2 — Total Project grid
- [ ] QTY cell second line + tooltip.
- [ ] Four optional columns, default visibility per D5.
- [ ] Relabel the money `Received` columns per D6.
- [ ] CSV + Excel export.

### Epic B3 — Tests (Part B)
- [ ] Partially received line: received / in-warehouse / remaining are right; rejected items count 0.
- [ ] A track number packed onto a Shipment Note leaves `inWarehouse` but stays in `received`.
- [ ] A warehouse transfer moves `inWarehouse` to the destination and is not double-counted.
- [ ] Stock PO row and `FromStock` row behave per D3 / D4.
- [ ] One page load issues one extra query regardless of row count (no N+1).

### Epic C — Rollout
- [ ] Apply the migration to a copy of production first and check the backfill count.
- [ ] Confirm with the Payment team which roles may correct a rate (current default: Admin + SuperAdmin).
- [ ] Deploy, then watch the unreviewed count for a week to confirm the yellow flow is being used.

---

## Decisions & defaults taken

- **D1 — Backfill existing auto rows as reviewed.** Otherwise the whole history turns yellow on day
  one and nobody trusts the colour.
- **D2 — No separate audit table.** Three columns on the transaction (original rate, who, when) cover
  "who changed this and from what"; a full history table is not worth it for a field that is
  corrected once.
- **D3 — Stock PO rows show no *In warehouse* number.** The stock lot is shared between POs, so any
  per-line figure would be invented. Received and Remaining are exact and are shown.
- **D4 — Lines served from Our Stock** report issued as received and reserved as in-warehouse, which
  matches the wording already used in their shipping-status text.
- **D5 — Received QTY and Remaining QTY on by default**, In Transit and In Warehouse available but
  hidden — the grid is already very wide.
- **D6 — Relabel the existing money columns** to `Received (Payment)` / `Received Date (Payment)`.
  Two different "Received" columns side by side would be read wrong on the first day.
- **D7 — Rate edit is Admin + SuperAdmin**, and is rate-only. Everything else about an automatic
  transaction stays in the SuperAdmin full-edit dialog, so a correction can never silently move money
  between suppliers or detach a POP.

## Open question

- Should correcting a rate notify anyone (the expert on the PO, or the person who uploaded the POP)?
  The default taken here is **no notification** — the yellow flag already makes new rows visible, and
  `NotificationService` is easy to add later if the Payment team wants it.
