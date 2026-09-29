# Feature Plan — Supplier Credits (credit we hold with a supplier, per buying company)

Status: **All build epics complete (E1–E7, E9, E10).** Backend: E1 Database, E2 single paid rule, E3
credit service & API, E4 change PO company. Frontend: E5 Supplier Credits page, E6 payment-page
suggestion, E7 PO picker & PO page. Change requests: **E9 credit proofs** (§9) and **E10 credit lines —
buy now, pay back later** (§10). **Remaining: E8 rollout (Sam)** — apply the three migrations, run the
DB tests, grant the menu permission, try it in the browser. Nothing has been run against a database
or opened in a browser yet.
Progress is tracked with checkboxes in §8.

This file is the brief for three senior agents: **Database**, **Backend** and **Frontend**. Each
agent reads §1–§3 (shared context and the contract), then its own section. Work in the order
**Database → Backend → Frontend**; the API contract in §3 is fixed so the frontend can start in
parallel with the backend once §3 is agreed.

---

## 1. What we are building

A **supplier credit** is money a supplier owes *us* that we can spend on future purchases instead
of paying cash: a credit note after a return, an overpayment, a prepayment/deposit, a goodwill
discount.

The credit belongs to **one supplier and one of our companies** (Company Preset). The same supplier
can have separate credit balances with Company A and Company B, and they never mix.

```
                   Supplier A
        ┌──────────────┴──────────────┐
  Company A: $0                 Company B: $1,200
```

**The rule:** a PO can only use the credit of *its own buying company* with *its own supplier*.

### The example this feature must make work

- Supplier A, our companies A and B. Company B holds **$1,200** credit with Supplier A.
- A PO to Supplier A under **Company A** → no usable credit. The payment screen says so and points
  out that Company B has $1,200 with this supplier.
- The same PO under **Company B** (chosen at creation, or changed before any payment) → at the
  payment withdraw step the user sees *"Company B has $1,200.00 credit with Supplier A — apply it?"*,
  applies e.g. $800, the balance counts down to **$400**, and only the rest is paid from a wallet.

### Scope

| In scope (v1) | Out of scope (later) |
|---|---|
| Credit balances per (Supplier, Company Preset), USD | Multi-currency credit (column exists, only USD allowed) |
| Add credit: credit note (manual) and wallet top-up (prepayment) | Auto-convert a cancelled/returned PO's payment into credit |
| Apply credit to a PO's payment request at the withdraw step | Credit expiry dates |
| Reverse a credit application / a credit entry | Supplier-side statements / reconciliation import |
| Credit suggestion on the payment page and in the PO company picker | Moving credit between companies (a transfer) |
| Change a PO's company before any payment is made | |
| Supplier Credits page (balances + ledger), feature-gated | |

---

## 2. How payments work today (read before designing)

| Piece | Where | Notes |
|---|---|---|
| Buying company of a PO | `PurchaseOrder.CompanyPresetId` | Set at creation (`PurchaseOrderService.CreateAsync`, line ~327). Legacy POs without it are resolved from `PreferredWalletId → PaymentBoxes.CompanyPresetId` by `PurchaseOrdersController` (~line 77, `FillCompanyPresetAsync`). **There is no endpoint to change it after creation.** |
| Payment request (PR) | `Purchasing/Entities/PaymentRequest.cs` | `Amount`, `Status`, `CompanyPresetId` ("company paying from"), pending draft fields `PendingWalletId/PendingPaymentAmount/PendingExchangeRate/PendingUploadId`. |
| PO payable total | `SupplierPaymentAmounts.Total(po)` | `TotalAmount + ProcessingFee + Shipping + Tax + ImportDetail.Wirefee`. |
| Supplier payment flow | `Sales/Controllers/SupplierPaymentsController.cs` | `POST po/{id}/draft` (PR + amount + wallet) → `POST po/{id}/pop` (creates `PaymentTransaction` Withdraw/`ToType="Supplier"`, PO → `WaitingForFinalAmount`) → `POST po/{id}/final-amount` (bank fee row, PR → `PAID`/`PARTIALLY PAID`, PO → `Submitted` + `PurchaseOrderStatusFlow.PaymentDone` when fully paid). |
| Wallets | `Sales/Entities/PaymentBox.cs`, `PaymentTransaction.cs` | Balance is **derived**: Σ Deposit − Σ Withdraw, each × `ExchangeRate ?? 1`. |
| "How much have we paid" | **Five places**, all `Type="Withdraw" AND ToType="Supplier"` | see table below |

**Every "paid" calculation that must learn about credit:**

| # | Location | Used for |
|---|---|---|
| P1 | `SupplierPaymentsController.SaveDraft` (`payments` query, ~line 128) | Validates the new wallet payment doesn't exceed PR / PO remaining. |
| P2 | `SupplierPaymentsController.FinalizeAmount` (~line 270) | Decides `PAID` vs `PARTIALLY PAID` and PO completion. |
| P3 | `SupplierPaymentsController.History` (`GET po/{poId}`) | Payment history list in the withdraw dialog. |
| P4 | `PaymentBoxService.GetSupplierPaidAmountsAsync` (via `IPaymentLedgerService`) | `PaymentRequestResponse.PaidAmount` / `RemainingAmount`, used by `PaymentRequestService` and `PurchaseOrdersController`. |
| P5 | `OurInventory/Services/StockPurchaseOrderService.cs` (~line 341, **raw SQL**) | `PaidAmount` on the Stock PO list. |

If any of these is missed, a PO paid partly by credit will look under-paid and the UI will let the
user pay the same money twice. **This is the main risk of the feature.**

### Key design decision: a separate credit ledger, not a fake wallet

Rejected: modelling each credit as a `PaymentBox` ("Supplier A – Company B credit wallet"). It would
reuse the paid calculations for free, but it pollutes every wallet list, the wallet dashboard totals
and the POP/final-amount flow (a credit has no POP and no bank fee).

Chosen: two new tables (an account per Supplier×Company with a cached balance, plus an append-only
ledger), and a single shared "paid" helper that adds **wallet payments + net credit applied**.
A wallet-funded top-up is still a real wallet withdrawal, with a new `ToType = "SupplierCredit"`,
which the existing `ToType = "Supplier"` filters already exclude — so a prepayment never counts as
paying a specific PO.

### Module placement

Credits are money and sit next to wallets: entities, service and controller go in
**`Procument.Module.Sales`** (Sales already references Purchasing and Catalog). Purchasing must
not reference Sales entities — it reaches credit totals only through `IPaymentLedgerService`
(`Procument.Shared`). OurInventory keeps its raw SQL and joins the new table by name.

---

## 3. Shared contract (all three agents)

### 3.1 Vocabulary

| Term | Meaning |
|---|---|
| **Account** | One row per (Supplier, Company Preset, Currency). Holds `Balance`. |
| **Entry** | One ledger line. `Direction = In` (credit grows) or `Out` (credit used). Append-only. |
| **Source** | Why the entry exists: `CreditNote`, `WalletTopUp`, `POApplication`, `Adjustment`, `Reversal`. |
| **Application** | An `Out` entry with `Source = POApplication`, linked to a PO **and** a PR. |
| **Credit applied to a PR** | Σ Amount of `Out` entries with that `PaymentRequestId` − Σ Amount of `In` entries with that `PaymentRequestId` (the reversals). Only applications and their reversals ever carry a `PaymentRequestId`. |
| **Paid on a PR** | wallet supplier withdrawals (today's rule) **+ credit applied to the PR**. |
| **Effective company of a PO** | `PO.CompanyPresetId`, else the legacy wallet resolution. No company → no credit can be used. |

### 3.2 Business rules

1. **Isolation.** A PO may only use the account matching `(PO.SupplierId, effective company, "USD")`.
2. **No negative balance.** An application or `Out` adjustment larger than the balance is rejected.
3. **Apply only when payable.** Same gate as `SaveDraft`: `PaymentApproval == "Accepted"`, PO not
   `Cancelled`/`Returned`, PR belongs to the PO.
4. **Cap.** `amount ≤ min(balance, PR remaining, PO remaining)`, where remaining subtracts paid
   **and** any pending wallet draft on the same PR/PO (`PendingPaymentAmount` not yet turned into a
   POP). Amount > 0, max 2 decimals.
5. **No POP, no bank fee.** An application settles immediately. It then runs the *same completion
   logic* as `FinalizeAmount` (PR `PAID`/`PARTIALLY PAID`; PO `Submitted` + `PaymentDone` when fully
   paid). Extract that logic into one shared helper so both paths are identical.
6. **Allowed while a wallet payment is in progress** (draft saved, or POP waiting for its final
   amount). Rule 4 already caps the amount (a saved draft is subtracted; an uploaded POP is already
   counted as paid). *As built:* in that case credit does **not** change the PR/PO status — the
   wallet flow owns `READY FOR POP` / `WAITING FOR FINAL AMOUNT` / `WaitingForFinalAmount`, and
   overwriting them would break the final-amount step. The final-amount step re-evaluates with the
   credit included (E2 made it credit-aware).
7. **Reversal, never delete/edit.** Undoing an entry writes a mirror entry (`Source = Reversal`,
   opposite direction, same PO/PR/amount, `ReversesEntryId` set). An entry can be reversed once.
   Reversing an application re-opens the PR/PO status (recompute with the shared helper; if the PO
   had moved to `PaymentDone` and nothing downstream has happened yet, move it back to
   `WaitingForPayment`; if it has shipped, block the reversal with a clear message). Reversing a
   `CreditNote`/`WalletTopUp` is blocked if it would drive the balance negative. Reversing a
   `WalletTopUp` also writes the matching wallet Deposit back (`FromType = "SupplierCredit"`).
8. **Changing a PO's company** is allowed only when the PO has **no** supplier wallet payment, **no**
   net credit applied, **no** pending draft, and is not `Cancelled`/`Returned`. Open PRs
   (`Status` not `PAID`) get their `CompanyPresetId` updated too. If `DestinationWarehouseId` is not
   linked to the new company (`CompanyPresetWarehouses`), the request must supply a valid new one.
   The PO PDF re-brands — the UI must warn that an already-sent PO PDF must be re-sent.
   *As built (E4):* also blocked once the PO status is past payment (anything other than Not
   Started / Waiting For Admin Approval / Sourcing / Waiting For Supplier Documents / Waiting For PR /
   Waiting For Payment / PR Rejected / Draft) and when a Final Invoice locks the PO. A PO whose credit
   was applied and then fully reversed (net paid 0) **may** change company. The rule lives once, in
   `IPurchaseOrderCompanyService.GetChangeBlockedReasonAsync`, used by both the PATCH and the credit
   suggestion.
9. **Idempotency.** Every write endpoint takes a client-generated `requestId` (GUID). A repeat with
   the same `requestId` returns the original result instead of posting twice (double-click safety).
10. **Audit.** Every write endpoint carries `[Auditable]` (the project's global audit filter, which
    records user, action and body) — the same mechanism as the other PO/payment actions.
11. **Currency.** v1 accepts `USD` only (supplier payments are already USD: "Amount paid (USD)").

### 3.3 Roles & feature flag

| Action | Roles |
|---|---|
| View balances / ledger / suggestion | `SuperAdmin, Admin, Payment, AHM` (same as `SupplierPaymentsController`) |
| Apply credit to a PO | `SuperAdmin, Admin, Payment, AHM` |
| Add credit note / wallet top-up | `SuperAdmin, Admin, Payment` |
| Manual `Adjustment` (In or Out), reverse any entry | `SuperAdmin, Admin` |
| Change a PO's company | `SuperAdmin, Admin, AHM` |
| See the **Supplier Credits** menu page | feature flag `supplierCredits` (SuperAdmin always) |

Base scoping: suppliers are not base-scoped today, so no `bases` filter is needed.

### 3.4 API (base `api/supplier-credits`, JSON camelCase)

```ts
// ── Types ─────────────────────────────────────────────────────────────
type CreditSource = 'CreditNote' | 'WalletTopUp' | 'POApplication' | 'Adjustment' | 'Reversal'

interface SupplierCreditAccountRow {
  id: number
  supplierId: number;        supplierName: string
  companyPresetId: number;   companyPresetName: string
  currency: 'USD'
  balance: number
  totalIn: number;           totalOut: number
  lastEntryAt: string | null
}

interface SupplierCreditEntryRow {
  id: number
  direction: 'In' | 'Out'
  source: CreditSource
  amount: number
  balanceAfter: number
  poId: number | null;       poNumber: string | null;  poLink: string | null
  paymentRequestId: number | null;  prNumber: number | null
  walletTransactionId: number | null;  walletName: string | null
  // E9 — proof of origin (credit notes). The PO the credit was USED on is poId above.
  sourcePoId: number | null;  sourcePoNumber: string | null;  sourcePoLink: string | null
  sourcePoReference: string | null   // free-text origin when it is not one of our POs
  creditMemo: string | null          // the supplier's credit memo (was `reference` before E9)
  notes: string | null
  documents: { id: number; entryId: number; category: 'SupplierPI' | 'CreditMemo' | 'Other';
               originalFileName: string; sizeBytes: number; uploadedAt: string; uploadedByName: string | null }[]
  reversesEntryId: number | null
  reversedByEntryId: number | null   // set when this entry has been reversed
  createdAt: string;         createdByName: string | null
}

interface CreditSuggestion {
  poId: number
  supplierId: number;        supplierName: string
  companyPresetId: number | null;  companyPresetName: string | null
  accountId: number | null
  usableBalance: number              // balance of the PO's own company
  poRemaining: number                // after wallet paid, credit applied, pending draft
  prOptions: { paymentRequestId: number; prNumber: number | null; remaining: number }[]  // remaining > 0 only
  suggestedAmount: number            // min(usableBalance, first PR option remaining, poRemaining); 0 when !canApply
  otherCompanies: { companyPresetId: number; companyPresetName: string; balance: number }[]  // balance > 0 only, largest first
  canApply: boolean                  // PR payment accepted and PO not cancelled/returned
  cannotApplyReason: string | null
  walletPaymentInProgress: boolean   // a wallet draft or POP is mid-flight — applying still works, statuses stay
  canChangeCompany: boolean          // data-state only (rule 8); the UI still checks the user's role
  changeCompanyBlockedReason: string | null
}

interface WriteResult { entry: SupplierCreditEntryRow; balance: number }   // credit-note, wallet-top-up, adjustment, reverse

// ── Endpoints ─────────────────────────────────────────────────────────
GET    /supplier-credits                       // PagedResult<SupplierCreditAccountRow> + totalAmountSum (Σ balance)
       ?page&pageSize&search&supplierIds=1&supplierIds=2&companyPresetIds=…&onlyWithBalance=true&sortBy=balance&sortDesc=true
       // sortBy: supplierName | companyPresetName | balance | totalIn | totalOut | lastEntryAt (+ sortDesc) — what
       // DataListPage/useServerSort sends. The older `sort=-balance` token still works. Default: balance desc.
       // search matches supplier or company name. Same multi-id filter shape as the Stock PO list.
GET    /supplier-credits/filter-options        // same query params → { suppliers: {id,name}[], companies: {id,name}[] } — exclude-self cascading
GET    /supplier-credits/{accountId}           // SupplierCreditAccountRow
GET    /supplier-credits/{accountId}/entries   // ?page&pageSize → PagedResult<SupplierCreditEntryRow>, newest first
GET    /supplier-credits/balances?supplierId=  // { companyPresetId, companyPresetName, balance }[] — for the PO company picker
GET    /supplier-credits/lookup/suppliers?q=   // { id, name }[] approved catalog suppliers, max 20 (built in E5: /suppliers is Admin/Expert only)
GET    /supplier-credits/lookup/companies      // { id, name }[] our active companies, never "(No Company)" (built in E5: /companypresets is Admin/Expert only)
GET    /supplier-credits/po/{poId}/suggestion  // CreditSuggestion

POST   /supplier-credits/credit-note           // multipart (E9): supplierId, companyPresetId, amount, sourcePurchaseOrderId | sourcePoReference (exactly one),
                                               //   creditMemo?, notes?, supplierPi? (1 file), documents? (0..n files, ≤ 10 files total), requestId → WriteResult
POST   /supplier-credits/wallet-top-up         // { supplierId, companyPresetId, walletId, amount, exchangeRate? (wallet currency per 1 USD; required when wallet ≠ USD), notes?, requestId } → WriteResult
POST   /supplier-credits/adjustment            // { supplierId, companyPresetId, direction: 'In'|'Out', amount, notes (required), requestId } → WriteResult
POST   /supplier-credits/po/{poId}/apply       // { paymentRequestId, amount, requestId } → { entry, balance, paidAmount, remainingAmount, prStatus, poPaymentStatus, complete }
POST   /supplier-credits/entries/{id}/reverse  // { notes (required), requestId } → { entry, balance }
GET    /supplier-credits/lookup/purchase-orders?supplierId=&q=  // (E9) the supplier's POs, newest first, max 30 → { id, poNumber, companyPresetName, status, totalAmount, createdAt }[]
POST   /supplier-credits/entries/{id}/documents                 // (E9) multipart: category (SupplierPI|CreditMemo|Other), files[] → DocumentRow[]  (SuperAdmin/Admin/Payment)
GET    /supplier-credits/documents/{documentId}/file            // (E9) one proof file (replaces /entries/{id}/file)
// Files: pdf, png, jpg, jpeg, webp, doc, docx, xls, xlsx, eml, msg — ≤ 10 per request, ≤ 100 MB per request.

PATCH  /purchase-orders/{id}/company-preset    // { companyPresetId, destinationWarehouseId? } → POResponse (refreshed)   (PurchaseOrdersController; SuperAdmin/Admin/AHM)
       // No requestId: setting the company is naturally idempotent (same company again = no-op).
```

Errors follow the existing convention: `400 { message }` with a human sentence the UI shows as-is;
`404` for unknown ids; `409 { message }` when the balance changed under the request, or a `requestId`
was reused for a *different* operation. **Generate a new `requestId` with `newRequestId()`
(`client/app/utils/supplierCredits.ts`) each time a dialog opens** and reuse it for retries of that
same submit — a repeat returns the first result. Do **not** use `crypto.randomUUID()`: it only exists
in secure contexts, and the app is served over plain HTTP on the LAN.

Existing responses that change (additive fields only):

| Response | New field(s) |
|---|---|
| `GET /supplier-payments/po/{poId}` (history) | ✅ built in E2. Every row now has `rowKey` (`tx-{id}` / `credit-{id}`, unique — **key on this**), `id` (wallet tx, `null` for credit rows), `creditEntryId`. Credit rows: `category = "Supplier Credit"` or `"Supplier Credit Reversal"` (negative `amount`), `walletName = "Credit · {Company}"`, `popFileName = null`. |
| `PaymentRequestResponse` | ✅ built in E2: `creditApplied: number` (already-existing `paidAmount` now **includes** it) |
| Wallet transaction rows (`PaymentTransactionRow`, `AllTransactionRow`) | ✅ built in E3: `toType = "SupplierCredit"` (prepayment top-up) → `toName = "Supplier Credit · {Supplier}"`; `fromType = "SupplierCredit"` (refund from a reversed top-up) → `fromName = "Supplier Credit"`. These rows cannot be edited/deleted on the wallet page (400 with a message) — reverse the credit entry instead. Rate correction still works. |
| `POResponse` | ✅ built in E4: `canChangeCompany: boolean`, `changeCompanyBlockedReason: string \| null` — filled on `GET /purchase-orders/{id}` and the PATCH response only (list rows leave them `false`/`null`) |

---

## 4. Database agent

**Goal:** two new tables, one migration, constraints that make bad data impossible even if a
backend bug slips through.

### 4.1 `SupplierCreditAccounts`

| Column | Type | Notes |
|---|---|---|
| `Id` | `bigint IDENTITY PK` | |
| `SupplierId` | `bigint NOT NULL` → `Suppliers.Id` | `ON DELETE NO ACTION` |
| `CompanyPresetId` | `bigint NOT NULL` → `CompanyPresets.Id` | `ON DELETE NO ACTION` |
| `Currency` | `nvarchar(3) NOT NULL DEFAULT 'USD'` | |
| `Balance` | `decimal(18,2) NOT NULL DEFAULT 0` | Cached; the ledger is the source of truth. |
| `CreatedAt` | `datetime2 NOT NULL` | |
| `ModifyAt` | `datetime2 NULL` | |
| `RowVersion` | `rowversion` | EF concurrency token. |

- `UX_SupplierCreditAccounts_Supplier_Company_Currency` **unique** on `(SupplierId, CompanyPresetId, Currency)`.
- `CK_SupplierCreditAccounts_Balance` : `Balance >= 0`.
- `IX_SupplierCreditAccounts_CompanyPresetId` on `(CompanyPresetId)` include `(Balance)`.

### 4.2 `SupplierCreditEntries` (append-only ledger)

| Column | Type | Notes |
|---|---|---|
| `Id` | `bigint IDENTITY PK` | |
| `AccountId` | `bigint NOT NULL` → `SupplierCreditAccounts.Id` | `NO ACTION` |
| `Direction` | `nvarchar(3) NOT NULL` | `In` \| `Out` |
| `Source` | `nvarchar(20) NOT NULL` | `CreditNote` \| `WalletTopUp` \| `POApplication` \| `Adjustment` \| `Reversal` |
| `Amount` | `decimal(18,2) NOT NULL` | always positive |
| `BalanceAfter` | `decimal(18,2) NOT NULL` | account balance right after this entry |
| `PurchaseOrderId` | `bigint NULL` → `PurchaseOrders.Id` | `NO ACTION` |
| `PaymentRequestId` | `bigint NULL` → `PaymentRequests.Id` | `NO ACTION` |
| `PaymentTransactionId` | `bigint NULL` → `PaymentTransactions.Id` | wallet row for `WalletTopUp` (and its reversal deposit) |
| `ReversesEntryId` | `bigint NULL` → `SupplierCreditEntries.Id` | self FK, `NO ACTION` |
| `Reference` | `nvarchar(100) NULL` | supplier credit-note number |
| `Notes` | `nvarchar(500) NULL` | |
| `DocumentFileName` | `nvarchar(260) NULL` | stored via `IDocumentStorageService` |
| `RequestId` | `uniqueidentifier NOT NULL` | idempotency key |
| `CreatedAt` | `datetime2 NOT NULL` | |
| `CreatedByUserId` | `bigint NULL` → `Users.Id` | `NO ACTION` |

Check constraints:

- `CK_SupplierCreditEntries_Amount` : `Amount > 0`
- `CK_SupplierCreditEntries_BalanceAfter` : `BalanceAfter >= 0`
- `CK_SupplierCreditEntries_Direction` : `Direction IN ('In','Out')`
- `CK_SupplierCreditEntries_Source` : `Source IN ('CreditNote','WalletTopUp','POApplication','Adjustment','Reversal')`
- `CK_SupplierCreditEntries_Application` : `Source <> 'POApplication' OR (Direction = 'Out' AND PurchaseOrderId IS NOT NULL AND PaymentRequestId IS NOT NULL)`
- `CK_SupplierCreditEntries_Reversal` : `(Source = 'Reversal') = (ReversesEntryId IS NOT NULL)` — write as
  `(Source = 'Reversal' AND ReversesEntryId IS NOT NULL) OR (Source <> 'Reversal' AND ReversesEntryId IS NULL)`
- `CK_SupplierCreditEntries_TopUp` : `Source <> 'WalletTopUp' OR PaymentTransactionId IS NOT NULL`

Indexes:

- `IX_SupplierCreditEntries_Account_CreatedAt` on `(AccountId, CreatedAt DESC, Id DESC)` — ledger paging.
- `IX_SupplierCreditEntries_PaymentRequestId` on `(PaymentRequestId)` include `(Direction, Amount)`
  **filtered** `WHERE PaymentRequestId IS NOT NULL` — makes the "credit applied per PR" sum cheap (P1–P5).
- `IX_SupplierCreditEntries_PurchaseOrderId` filtered `WHERE PurchaseOrderId IS NOT NULL`.
- `UX_SupplierCreditEntries_ReversesEntryId` **unique** filtered `WHERE ReversesEntryId IS NOT NULL`
  — an entry can be reversed at most once, enforced by the DB.
- `UX_SupplierCreditEntries_RequestId` **unique** on `(RequestId)` — idempotency.

### 4.3 EF configuration & migration

- Entities `SupplierCreditAccount`, `SupplierCreditEntry` in
  `src/Modules/Procument.Module.Sales/Entities/`; `DbSet`s and `modelBuilder.Entity<...>` blocks in
  `src/Procument.Data/AppDbContext.cs` next to `PaymentBox` / `PaymentTransaction` (~line 1258).
  Use `HasPrecision(18,2)`, `IsRowVersion()`, `HasCheckConstraint(...)` inside `ToTable(t => ...)`,
  `HasFilter(...)` for filtered indexes, `DeleteBehavior.NoAction` everywhere.
- No change to existing columns. `PaymentTransactions.ToType` / `FromType` are free text, so the
  new value `SupplierCredit` needs no schema change — only update the code comment on the entity.
- Migration name: **`AddSupplierCredits`**. No backfill (there is no historical credit).
- Provide a read-only reconciliation query in `db-summary-queries.sql`:

```sql
-- Accounts whose cached balance disagrees with the ledger (must return 0 rows)
SELECT a.Id, a.Balance,
       Ledger = SUM(CASE e.Direction WHEN 'In' THEN e.Amount ELSE -e.Amount END)
FROM SupplierCreditAccounts a
LEFT JOIN SupplierCreditEntries e ON e.AccountId = a.Id
GROUP BY a.Id, a.Balance
HAVING a.Balance <> ISNULL(SUM(CASE e.Direction WHEN 'In' THEN e.Amount ELSE -e.Amount END), 0);
```

- Update `db-schema-reference.md` with both tables.

### 4.4 Done when

- `dotnet ef migrations add AddSupplierCredits` generates only the two tables, their FKs, indexes and
  check constraints; `Down()` drops them cleanly.
- `dotnet build src/Procument.API -clp:ErrorsOnly` is clean. **Do not run the app**; do not apply the
  migration to Sam's database — Sam applies it.

### 4.5 As built (E1)

| Item | Where |
|---|---|
| Entities | `src/Modules/Procument.Module.Sales/Entities/SupplierCreditAccount.cs`, `SupplierCreditEntry.cs` (incl. constants `SupplierCreditDirections`, `SupplierCreditSources` — use these, not string literals) |
| EF config + DbSets | `src/Procument.Data/AppDbContext.cs` — block "Supplier Credits", right after `WalletTransferPending` |
| Migration | `src/Procument.Data/Migrations/20260927045737_AddSupplierCredits.cs` (+ Designer, snapshot updated) |
| `PaymentTransaction` | comment only: `FromType`/`ToType` list now names `SupplierCredit` (column is `nvarchar(20)`, fits) |
| Docs | `db-schema-reference.md` (both tables, status values, new "paid = wallet + credit" trap); `db-summary-queries.sql` H4 (credit balances), H5 (paid per PR = wallet + credit), K3a–c (integrity checks that must return 0 rows) |

Differences from §4.1–4.2:

- Two extra plain FK indexes: `IX_SupplierCreditEntries_PaymentTransactionId` (filtered) and
  `IX_SupplierCreditEntries_CreatedByUserId`, so deletes/joins on those FKs never scan the ledger.
- `Currency` and `Balance` carry DB defaults (`'USD'`, `0`) as specified; the C# defaults match.
- Account → entries relationship is `NoAction`, like every other FK in the feature.

Verified:

- `dotnet ef migrations has-pending-model-changes` was clean **before** the change and is clean after.
- The generated migration contains only the 2 tables, 6 + 2 FKs, 7 + 1 CHECK constraints and 9 indexes;
  `Down()` drops both tables. The T-SQL script (`dotnet ef migrations script
  20260923054530_AddWalletTransactionReviewAudit AddSupplierCredits`) was generated and reviewed.
- `Procument.API` and `Procument.OurInventory.Tests` build with 0 errors.
- **Not applied to any database. The app was not run.**

> **Tooling note for the next agents:** while Sam has the API open in Visual Studio,
> `bin/Debug` DLLs are locked and a Debug build fails with MSB3027 (copy errors, not compile
> errors). Build and run EF tooling with **`-c Release`** / `--configuration Release` instead of
> stopping Sam's process.

---

## 5. Backend agent

**Goal:** credit ledger service + endpoints, a single source of truth for "paid", and the credit
path wired into the existing supplier payment flow without changing how wallet payments behave.

### 5.1 Files

| File | Change |
|---|---|
| `Sales/Entities/SupplierCreditAccount.cs`, `SupplierCreditEntry.cs` | new (from §4) |
| `Sales/DTOs/SupplierCreditDTOs.cs` | new — records matching §3.4 |
| `Sales/Services/ISupplierCreditService.cs`, `SupplierCreditService.cs` | new — all ledger writes live here |
| `Sales/Services/SupplierPaymentLedger.cs` | new — static helpers `PaidByPrAsync`, `PaidByPoAsync`, `CreditAppliedByPrAsync` (wallet + credit). **The only place the "paid" rule is written.** |
| `Sales/Services/SupplierPaymentCompletion.cs` | new — extracted from `FinalizeAmount`: recompute PR status, PO `PaymentStatus`, `PaymentSubmittedAt/By`, `PurchaseOrderStatusFlow`. Used by final-amount, credit apply and credit reversal. |
| `Sales/Controllers/SupplierCreditsController.cs` | new — `api/supplier-credits` |
| `Sales/Controllers/SupplierPaymentsController.cs` | P1, P2, P3 use `SupplierPaymentLedger`; `FinalizeAmount` uses `SupplierPaymentCompletion` |
| `Sales/Services/PaymentBoxService.cs` | P4: `GetSupplierPaidAmountsAsync` adds credit applied; label `SupplierCredit` in the 3 `toName`/`fromName` ternaries (`GetByIdAsync`, `GetAllTransactionsAsync`, `BuildRowAsync`) |
| `OurInventory/Services/StockPurchaseOrderService.cs` | P5: raw SQL gains a `UNION ALL` over `SupplierCreditEntries` (net: `Out` − `In`) grouped by `POId` |
| `Purchasing/Services/IPaymentRequestService.cs` | `PaymentRequestResponse.CreditApplied` |
| `Shared/Services/IPaymentLedgerService.cs` | add `Task<Dictionary<long, decimal>> GetCreditAppliedAmountsAsync(IEnumerable<long> paymentRequestIds)` so Purchasing can fill `CreditApplied` without referencing Sales |
| `Purchasing/Controllers/PurchaseOrdersController.cs` | `PATCH {id}/company-preset`; `canChangeCompany` on the PO response |
| `Sales/SalesModule.cs` | register `ISupplierCreditService` (scoped) |
| `Sales/Entities/PaymentTransaction.cs` | comment: `ToType` / `FromType` may be `"SupplierCredit"` |

### 5.2 Ledger write algorithm (every write uses this)

```
ExecutionStrategy.ExecuteAsync(async () => {
  ChangeTracker.Clear()
  BEGIN TRANSACTION (Serializable)            // same pattern as SupplierPaymentsController
  if entry with RequestId exists → return its result (idempotent)
  account = SELECT … WITH (UPDLOCK, ROWLOCK) WHERE Supplier/Company/Currency
            (create it with Balance 0 if missing — only for In entries)
  validate (rules §3.2) against account.Balance and fresh paid totals
  account.Balance ±= amount; entry.BalanceAfter = account.Balance
  insert entry (+ wallet PaymentTransaction for top-up / top-up reversal)
  SaveChanges → catch DbUpdateConcurrencyException → 409 "The credit balance changed. Refresh and try again."
  run SupplierPaymentCompletion when the entry touches a PR
  audit; COMMIT
})
```

Use `FromSqlInterpolated` for the `UPDLOCK` read or rely on `RowVersion` + Serializable — but never
read the balance outside the transaction and write it inside.

### 5.3 Endpoint behaviour notes

- **`GET /po/{poId}/suggestion`** — one round trip for the payment dialog. Resolves the effective
  company (reuse `PurchaseOrdersController`'s legacy resolver; move it to a small shared helper if
  needed), loads all accounts for `po.SupplierId` with `Balance > 0`, splits into the PO's own
  company vs `otherCompanies`, computes `prOptions` from open PRs (`remaining = Amount − paid − pending
  draft on that PR`), and `canChangeCompany` with the reason from rule 8.
- **`POST /po/{poId}/apply`** — rules 1–6. Writes an `Out`/`POApplication` entry with
  `PurchaseOrderId` + `PaymentRequestId`, `Notes = "Credit applied to PR-{PRId} / {PONumber}"`.
  Returns updated totals so the UI doesn't need a second call.
- **`POST /wallet-top-up`** — validates the wallet like `SaveDraft` does (exchange rate required for
  non-USD, 6 dp). Writes a wallet `PaymentTransaction`: `Type="Withdraw"`, `FromType="MotherWallet"`,
  `ToType="SupplierCredit"`, `ToSupplierId`, `TxCurrency="USD"`, `ExchangeRate`, `IsAuto=true`,
  `Notes="Prepayment credit — {Supplier} / {Company}"`, then the `In` entry pointing at it.
  The wallet balance drops through the existing derived-balance logic; no other wallet code changes.
  *Decision D3 below:* a top-up does not need a POP in v1.
- **`POST /credit-note`** — multipart; the file (optional, image/PDF, ≤ 100 MB like POP upload) is saved
  with `IDocumentStorageService.SaveFileInSupplierCategory(…, supplierName, "Supplier Credit Notes", …)`.
- **`POST /entries/{id}/reverse`** — rule 7. Reversing a top-up also writes a wallet `Deposit`
  (`FromType="SupplierCredit"`, same amount and rate) and links it via `PaymentTransactionId`.
- **`PATCH /purchase-orders/{id}/company-preset`** — rule 8. Also clears `PreferredWalletId` if it
  points at a wallet of the old company (it is "legacy, never used to select a wallet", per the DTO
  comment). Notify via `INotificationService` like other PO changes, if that is the local pattern.
- **List endpoints** — server-side paging/filtering/sorting in SQL (backend-first: no filtering in the
  browser). `filter-options` follows the exclude-self cascading contract used by the other 6 list
  pages. `totalAmountSum` on `PagedResult` = Σ balance of the filtered accounts.

### 5.4 Tests (`src/Procument.OurInventory.Tests`, reuse `StockDatabaseFixture`, see `PaymentBoxRateCorrectionTests.cs`)

1. **The example:** credit $1,200 on (Supplier A, Company B). PO $2,000 under Company A →
   suggestion `usableBalance = 0`, `otherCompanies = [B: 1200]`, `canChangeCompany = true`. Change
   company to B → `usableBalance = 1200`. Apply $800 → balance 400, PR `PARTIALLY PAID`, PR remaining 1,200.
   Wallet draft of $1,300 → rejected; $1,200 → accepted; POP + final amount → PR `PAID`, PO `PaymentDone`.
2. Apply more than the balance → 400; more than PR remaining → 400; with pending draft counted → 400.
3. Credit covers the whole PO → PR `PAID`, PO `Submitted` + `PaymentDone`, no wallet rows created.
4. Same `requestId` twice → one entry, same response.
5. Two concurrent applies that together exceed the balance → exactly one succeeds.
6. Reverse an application → balance restored, PR back to `PARTIALLY PAID`/pending, PO back to
   `WaitingForPayment`; reversing twice → 400 (and the DB unique index would also refuse).
7. Wallet top-up → wallet balance −X, credit +X, and **PO paid totals unchanged** (P1–P5).
8. Change company blocked after any wallet payment or credit application.
9. P5 raw SQL returns wallet + credit for a Stock PO.

### 5.5 Done when

`dotnet build -clp:ErrorsOnly` is clean and the tests above pass. **Do not start the API or the Nuxt
dev server** — Sam runs the app.


### 5.6 As built (E2 — single paid rule)

| Item | Where |
|---|---|
| The paid rule | `Sales/Services/SupplierPaymentLedger.cs` — `WalletPayments(db)`, `CreditApplications(db)`, `PaidByPoAsync`, `PaidByPrAsync` (single + batch), `CreditAppliedByPrAsync` |
| Completion | `Sales/Services/SupplierPaymentCompletion.cs` — `ApplyAsync(db, po, pr, userId)` → `SupplierPaymentProgress(PoTotal, PoPaid, PrPaid, Complete)` + `PoRemaining`. Logic moved verbatim from `FinalizeAmount`. |
| P1 `SaveDraft` | uses `PaidByPoAsync` / `PaidByPrAsync` |
| P2 `FinalizeAmount` | clears pending fields, then `SupplierPaymentCompletion.ApplyAsync`; response shape unchanged |
| P3 `History` | typed `SupplierPaymentHistoryRow` record; wallet rows + credit rows merged, newest first |
| P4 | `PaymentBoxService.GetSupplierPaidAmountsAsync` → `SupplierPaymentLedger.PaidByPrAsync`; new `GetCreditAppliedAmountsAsync` on `IPaymentLedgerService` |
| `PaymentRequestResponse.CreditApplied` | filled in `PaymentRequestService.MapToResponse` (batched in `GetAllAsync`) |
| P5 | `StockPurchaseOrderService.EnrichAsync` raw SQL: `UNION ALL` of wallet payments and net credit per PO |
| Tests | `src/Procument.OurInventory.Tests/SupplierPaymentLedgerTests.cs` (5 tests) |

Callers that get credit-awareness for free through P4: `PaymentRequestService` (`UpdateAmountAsync`
minimum = paid, `DeleteAsync` blocks a PR with credit applied, list/detail `PaidAmount`),
`PurchaseOrdersController.SubmitPayment`. A full `grep` confirmed there are no other readers of
supplier payments.

Behaviour with no credit rows is identical to before (credit sums are 0). Bank fees and
`ToType = "SupplierCredit"` top-ups are excluded by the `ToType = "Supplier"` filter — a test tags a
top-up with a PR on purpose to prove it.

Verified: `Procument.API` and the test project build with 0 errors (Release); no new warnings in the
touched files. The 5 new tests + 3 existing wallet tests compile and are discovered, but **were
skipped — `PROCUMENT_TEST_SQL` is not set on this machine**, so they have not run against SQL Server
yet. The app was not run.

**Carry-over for E3:** `SupplierPaymentCompletion` only moves *forward* (PAID / PARTIALLY PAID,
PaymentDone). Reversing a credit application (rule 7) needs a backward path: PR back to its
pre-payment status when `PrPaid == 0`, PO back to `WaitingForPayment` if it is still in `PaymentDone`.
Extend the helper there rather than writing a second one.

### 5.7 As built (E3 — credit service & API)

| Item | Where |
|---|---|
| DTOs | `Sales/DTOs/SupplierCreditDTOs.cs` |
| Service | `Sales/Services/SupplierCreditService.cs` — `ISupplierCreditService`, `SupplierCreditException` (→ 400), `SupplierCreditConflictException` (→ 409); registered in `SalesModule.cs` |
| Controller | `Sales/Controllers/SupplierCreditsController.cs` — all §3.4 endpoints, roles per §3.3, `[Auditable]` on writes |
| Completion | `SupplierPaymentCompletion` gained `ProgressAsync` (read-only), `ReopenAsync` (backward path), `CanReopen`, `IsClosed`, `UnpaidPrStatus = "PENDING APPROVAL"` |
| Wallet integration | `PaymentBoxService`: `SupplierCredit` names in the 3 row builders; `UpdateTransactionAsync` / `DeleteTransactionAsync` refuse credit-linked rows. `PaymentBoxesController` now turns those `ArgumentException`s into 400s (this also fixes the existing POP/bank-fee delete guard, which surfaced as a 500) |
| PR deletion | `IPaymentLedgerService.HasSupplierCreditHistoryAsync`; `PaymentRequestService.DeleteAsync` refuses a PR with credit history (net 0 after a reversal would pass the "paid" check and then fail on the ledger FK) |
| Tests | `src/Procument.OurInventory.Tests/SupplierCreditServiceTests.cs` — 8 tests: §5.4 (1)–(7) + Out-adjustment / used-credit reversal guards. Test (1) drives the real `SupplierPaymentsController.SaveDraft` and `FinalizeAmount` (the POP upload is simulated without writing a file) |

How the write path works (§5.2, as built):

1. `requestId` lookup **before** the transaction (read-committed). Found → replay the stored result;
   found but for a different operation → 409. (Inside a Serializable transaction this lookup would
   take a key-range lock that deadlocks with a parallel insert into the same index gap.)
2. Execution strategy → `ChangeTracker.Clear()` → Serializable transaction.
3. The account row is read `WITH (UPDLOCK, HOLDLOCK)` — this also locks the key range when the
   account does not exist yet, so two first-ever credits cannot both create it.
4. `Post()` moves `Balance` and writes the entry with `BalanceAfter`; a negative result is refused
   with a readable message before SQL's CHECK constraint would be hit.
5. Commit. Failure hooks undo side effects (the saved credit-note file).
6. Outside: `DbUpdateConcurrencyException` → 409; duplicate `RequestId` → replay; duplicate
   reversal → 400; duplicate account → 409 "try again".

Rules as built, beyond §3.2:

- **Effective company** = `PO.CompanyPresetId`, else the company of the legacy `PreferredWalletId`
  (never the "(No Company)" preset). No company → credit cannot be applied; the suggestion shows 0.
- **Parties:** the supplier must exist; the company must be active and not "(No Company)".
- **Reversing an application** is allowed while the PO is `Waiting For PR` / `Waiting For Payment` /
  `PR Rejected` / `Payment Done`, and on `Cancelled` / `Returned` POs (the credit comes back, the PO
  status is untouched). Past payment (shipping, received, completed) → refused. The PR returns to
  `PARTIALLY PAID` or `PENDING APPROVAL`; a PO in `Payment Done` returns to `Waiting For Payment`
  with `PaymentStatus` `PartiallyPaid` / `NotStarted` and submitted-at/by cleared.
- **Reversing a wallet top-up** writes a wallet `Deposit` (`FromType = "SupplierCredit"`, same USD
  amount at the top-up's *current* rate, so a later rate correction is undone exactly too).
- **Credit-note files** are saved with `SaveFileInSupplierCategory("Supplier Credits", supplierName,
  "Credit Notes", "{requestId}-{file}")`; pdf/png/jpg/jpeg/webp; ≤ 100 MB.

Verified: `Procument.API` and the test project build with 0 errors (Release); no new warnings in the
touched files. All 16 supplier-credit / ledger / wallet tests compile and are discovered, **but were
skipped — `PROCUMENT_TEST_SQL` is not set here**, so nothing has run against SQL Server yet. EF query
shapes were re-reviewed by hand for runtime-only translation problems. The app was not run.

~~Carry-over for E4~~ — done in E4: the suggestion now calls the same Purchasing rule (§5.8).

### 5.8 As built (E4 — change PO company)

| Item | Where |
|---|---|
| The rule + the change | `Purchasing/Services/PurchaseOrderCompanyService.cs` — `IPurchaseOrderCompanyService.GetChangeBlockedReasonAsync(po)` and `ChangeCompanyAsync(poId, companyPresetId, destinationWarehouseId?)`; registered in `PurchasingModule.cs` |
| Endpoint | `PATCH /purchase-orders/{id}/company-preset` in `PurchaseOrdersController` — roles SuperAdmin/Admin/AHM, the usual `CanAccessAsync`, Final-Invoice lock check, `[Auditable("PurchaseOrder", "ChangeCompany")]`; returns the refreshed `POResponse` |
| DTOs | `ChangePOCompanyRequest`; `POResponse.CanChangeCompany` / `ChangeCompanyBlockedReason` (filled by `GET /purchase-orders/{id}`) |
| Suggestion | `SupplierCreditService.GetSuggestionAsync` now asks `IPurchaseOrderCompanyService` — one rule, two callers |
| Tests | 3 more in `SupplierCreditServiceTests` (11 there in total): the A→B example end to end; blocked by wallet payment / draft / credit / past-payment status and re-allowed after a full reversal; warehouse links + inactive company |

What the change does (one Serializable transaction, so a draft or credit cannot slip in between the
check and the switch):

1. New company must exist and be active (this also excludes the inactive "(No Company)" preset).
   Same company and no warehouse given → no-op.
2. Rule 8 via `GetChangeBlockedReasonAsync` ("paid" = `IPaymentLedgerService`, i.e. wallet + credit).
3. Destination warehouse = the one given, else the current one. It must be active and — when the
   new company has warehouse links — linked to it (the Stock PO rule). A Stock PO must keep one.
4. `PurchaseOrder.CompanyPresetId` set; `PreferredWalletId` cleared when its wallet belongs to
   another company (raw SQL — Purchasing cannot reference `PaymentBox`).
5. Every PR of the PO gets the new `CompanyPresetId` (nothing is paid, so all are open).

The PO / PR PDFs read `CompanyPresetId`, so they re-brand automatically — the UI must warn (E7).

Verified: API and tests build with 0 errors, no new warnings in touched files. 19 supplier-credit /
ledger / wallet tests are discovered but **skipped — `PROCUMENT_TEST_SQL` is not set here**. The app
was not run.

---

## 6. Frontend agent

**Goal:** make credit visible where the decision is made (PO company picker, payment withdraw step)
and give finance a page to manage balances. Nuxt 3 + Vuetify, `<script setup>`, existing `api`
composable.

### 6.1 Feature flag & navigation (the usual 3 steps + route guard)

1. `client/app/stores/auth.ts` — `supplierCredits: []` in `DEFAULT_FEATURE_PERMISSIONS`, getter
   `supplierCredits(): boolean { return (this as any).isSuperAdmin || this.can('supplierCredits') }`.
2. `client/app/layouts/default.vue` — nav item
   `{ title: 'Supplier Credits', icon: 'mdi-cash-refund', to: '/supplier-credits', supplierCredits: true }`
   near Payment / Wallets, and `if (item.supplierCredits && !authStore.supplierCredits) return false` in `filterItem()`.
3. `client/app/pages/menu-access.vue` — `FEATURE_LABELS` / `FEATURE_ICONS` entries.
4. `client/app/middleware/auth.global.ts` — `{ prefix: '/supplier-credits', feature: 'supplierCredits' }`
   in `FEATURE_GATED_PREFIXES`.

### 6.2 New page — `client/app/pages/supplier-credits/index.vue`

- `DataListPage` with `serverSide`, `:show-total-sum="true"` (Σ balance chip), `extraParams` for
  `onlyWithBalance` (toggle, default on).
- Columns: Supplier · Company · Balance (right-aligned, green when > 0) · Total In · Total Out · Last movement.
  Supplier and Company use `ColFilterMenu` fed by `/supplier-credits/filter-options`.
- Header actions: **Add Credit Note**, **Prepay from Wallet**, **Adjustment** (Admin/SuperAdmin only).
- Row click → ledger drawer (`v-navigation-drawer`, right) for that account: entries table, `In` green
  / `Out` red, source chip, PO/PR links (`poLink`), document download, **Reverse** button
  (Admin/SuperAdmin; hidden when `reversedByEntryId` or `source === 'Reversal'`), reversal requires a note.
- **Performance rules (hard):** pass only scalars through `DataListPage` slots — never the row
  array; no template refs to read first-render data. The drawer fetches its own entries.

### 6.3 New components

| Component | Purpose |
|---|---|
| `components/SupplierCreditDialog.vue` | One dialog, three modes (`creditNote` / `walletTopUp` / `adjustment`). Supplier autocomplete (catalog suppliers only), Company select (`/companypresets`), amount (USD, 2 dp), reference, notes, file input (credit note), wallet select + rate when wallet ≠ USD (reuse the wallet list/label logic from `pages/payment/index.vue`: `/supplier-payments/wallets`, `boxLabel`). Generates a `requestId` when opened; disables submit while saving. *(Built — see §6.3a; uses `lookup/*` endpoints and `newRequestId()`.)* |
| `components/SupplierCreditSuggestion.vue` | Props: `poId`. Loads `/supplier-credits/po/{poId}/suggestion`, emits `applied(result)` and `company-changed`. Used in the payment dialog and on the PO page. |
| `components/ChangePoCompanyDialog.vue` | Company select listing every preset with its credit with this supplier (from `/supplier-credits/balances?supplierId=`), warehouse select when required, warning *"The PO PDF will be re-branded; re-send it to the supplier if it was already sent."* Calls `PATCH /purchase-orders/{id}/company-preset`. |

### 6.3a As built (E5 — Supplier Credits page)

| Item | Where |
|---|---|
| Page | `client/app/pages/supplier-credits/index.vue` — `DataListPage` (server-side, Σ-balance chip, `page-key="supplier-credits"`), Supplier/Company autocompletes fed by `filter-options` (exclude-self, debounced 250 ms — same pattern as the Stock PO list), "Only with balance" switch, one combined Clear. Sortable: supplier, company, balance, total in, used/out, last movement. Header buttons by role: Add Credit Note + Prepay from Wallet (SuperAdmin/Admin/Payment), Adjustment (SuperAdmin/Admin). Supplier name or the book icon opens the ledger. Deep link `/supplier-credits?account={id}` opens a ledger directly (for E7). |
| Dialog | `client/app/components/SupplierCreditDialog.vue` — one `<v-form>` for the three modes; supplier autocomplete via `lookup/suppliers` (server search), company radios (≤ 5 companies) or select, In/Out radios for adjustments, `inputmode="decimal"` money fields with 2-dp / 6-dp rules, wallet + rate + "wallet debit" preview for prepayments, credit-note number + file (pdf/png/jpg/jpeg/webp), errors in an `aria-live` region. A new `requestId` on every open; a failed submit keeps it, so retrying cannot post twice. Supplier/company can be pre-filled and locked. |
| Ledger drawer | `client/app/components/SupplierCreditLedgerDrawer.vue` — right drawer (never wider than the screen): balance / total in / total out, add buttons pre-filled with this supplier + company, paged entries (type chip, signed amount, balance after, PO/PR link, wallet, reference, notes), credit-note preview through `useDocPreview`, Reverse (SuperAdmin/Admin; reason required; hidden on reversals and reversed entries). |
| Shared | `client/app/utils/supplierCredits.ts` — API types, `newRequestId()`, source labels/colours, `apiErrorMessage()` |
| Refresh | After any write the page bumps an `_r` param in `extraParams` (the Our Inventory page pattern), which reloads the list, the filter options and an open ledger |

Flag wiring as built (§6.1 plus two things the plan missed):

- **Backend `KnownFeatures`** in `MenuPermissionsController` (`Identity/Controllers/AuthController.cs`)
  now lists `supplierCredits` — without it Menu Access cannot grant the flag.
- The `supplierCredits` getter is true for SuperAdmin, or for users granted the flag **whose role
  the API serves** (Admin, Payment, AHM) — an Expert granted by mistake gets no page that would 403.
- `default.vue`: `if (item.supplierCredits) return authStore.supplierCredits` early (like Our
  Inventory). For the **Payment role**, a nav group is now kept when one of its children is allowed.
  Before, the rule only let `walletMenu` items through, so the "Payment" group itself was dropped and
  its Wallets child could never show for that role. Only the Payment group is affected (checked).

Backend touched in E5: the list accepts `sortBy`/`sortDesc`; two lookup endpoints (see §3.4).

Verified without running the app: every new/edited `.vue` file compiles with `@vue/compiler-sfc`
(template + `<script setup lang="ts">`); the edited `.ts` files parse with esbuild; every URL the UI
calls exists on the controller and every query/form field matches the DTOs; API and tests build with
0 errors. There is no type-check script in the client (`vue-tsc` is not installed), and **the page
was not opened in a browser**.

### 6.4 Payment page — `client/app/pages/payment/index.vue` (the withdraw step)

Inside the **Supplier Payment** expansion panel (`dialogMode === 'withdraw'`, ~line 334), above the
Payment Request select, render `<SupplierCreditSuggestion :po-id="selectedPo.id" />`:

| Suggestion state | UI |
|---|---|
| `usableBalance > 0` | `v-alert type="success"`: **"{Company} has $1,200.00 credit with {Supplier}."** PR select (from `prOptions`), amount field prefilled with `suggestedAmount`, max = min(balance, PR remaining). Button **Apply Credit** → confirm dialog *"Use $800.00 of credit for PR-1523? Remaining credit: $400.00."* |
| `usableBalance == 0` and `otherCompanies.length` | `v-alert type="info"`: **"This PO is under {Company A}. {Company B} has $1,200.00 credit with {Supplier}."** + **Change PO company** button when `canChangeCompany`, else the `changeCompanyBlockedReason` text. |
| no credit anywhere | render nothing |

After a successful apply: show a snackbar, reload `paymentHistory`, `paymentRequests`, the draft and
the suggestion; set `pickerAmount` to the new PR remaining so the wallet form shows only what is
left. If the result is `complete`, close the wallet form and show "Paid in full with credit".

Payment history rows: chip colour for `category === 'Supplier Credit'` (e.g. `success`), text
`"PR-{n} · $800.00 from credit ({Company})"`, no eye/download button.

### 6.4a As built (E6 — payment-page suggestion)

| Item | Where |
|---|---|
| Component | `client/app/components/SupplierCreditSuggestion.vue` — props `poId`, `refreshKey`; emits `applied(ApplySupplierCreditResult)`. Loads the suggestion; a 403/error renders nothing, so the payment flow works as before for anyone without access. |
| State 1 — own company has credit | Green alert "*{Company} has $X credit with {Supplier}.*" with a PR select (only PRs with something left), amount prefilled with the most that fits (`min(balance, PR left, PO left)`, re-suggested when the PR changes), `inputmode="decimal"` + 2-dp/max rules, **Apply credit** → confirm dialog "*Use $X of {Company}'s credit for PR-n? Remaining credit: $Y.*" (new `requestId` per confirmation, reused on retry). If `!canApply` it shows `cannotApplyReason`; if nothing is left, "Nothing is left to pay". `walletPaymentInProgress` adds a note that PR status updates when that payment's final amount is saved. |
| State 2 — only other companies | Blue alert "*This PO is under {A}. {B} has $X credit with {Supplier}.*" + **Change PO company** (link to `/purchase-orders/{id}?changeCompany=1`, only when `canChangeCompany` and the role is SuperAdmin/Admin/AHM), else the blocked reason or "ask an Admin or AHM". |
| State 3 — no credit | Nothing rendered |
| Payment page | `pages/payment/index.vue` — the component sits at the top of the Supplier Payment panel (visible in both the wallet form and the waiting-for-final-amount state). `creditRefreshKey` is bumped on open and after saving wallet payment details. `onCreditApplied`: closes the dialog and jumps to **Finished** only when `complete && poPaymentStatus === 'Submitted'` (a PO covered while a POP waits for its final amount stays open, with a hint); otherwise reloads queue, PRs, history and draft, and — when no wallet draft is saved — moves the wallet form to the next open PR and pre-fills what is left. |
| History | Rows keyed on `rowKey`; chips: Supplier Payment (primary), Bank Fee (orange), Supplier Credit (green), Supplier Credit Reversal (grey); credit rows read "*PR-n · $800.00 paid from credit (Credit · Company B)*", reversals "*−$800.00 credit returned*". |
| Wallet pages | `payment-control/[id].vue` and `payment-control/index.vue`: `mdi-cash-refund` icon for `fromType`/`toType = SupplierCredit`. In `[id].vue` the SuperAdmin edit/delete buttons are hidden on those rows (the API refuses them since E3). |

Verified without running the app: the five touched `.vue` files compile with `@vue/compiler-sfc`,
the util parses with esbuild, and the page ↔ component ↔ API names were grepped
(`ApplySupplierCreditRequest(paymentRequestId, amount, requestId)`, `SupplierPaymentHistoryRow.rowKey`
/ `creditEntryId`). **Not opened in a browser.**

### 6.5 PO creation — `client/app/pages/purchase-orders/index.vue`

When the Company Preset picker opens (`createPOFromGroup` / `confirmCreate`, `pendingCreate`), call
`/supplier-credits/balances?supplierId=` for the group's supplier. In the `v-select`:

- `item-props` subtitle `"Credit with {Supplier}: $1,200.00"` for presets with a balance;
- a `v-alert type="info" density="compact"` under the picker when any preset has credit:
  *"Company B has $1,200.00 credit with this supplier. Choose Company B to use it at payment."*

Presets without credit look exactly as today. The picker is Admin/SuperAdmin/Expert-only today
(`/companypresets`); if the balances call returns 403 for a role, silently skip the hint.

### 6.6 PO detail — `client/app/pages/purchase-orders/[id].vue`

Next to the company chip (~line 8): a small credit chip *"Credit: $400.00"* when the PO's company has
credit with its supplier, and a **Change company** action (opens `ChangePoCompanyDialog`) when
`po.canChangeCompany` and the user's role allows it (§3.3). After a change, reload the PO and the
warehouse list (`/companypresets/{id}/warehouses`, ~line 1594).

### 6.6a As built (E7 — PO picker & PO page)

| Item | Where |
|---|---|
| PO creation picker | `pages/purchase-orders/index.vue` — when the Company Preset picker opens, `/supplier-credits/balances?supplierId=` loads (race-safe). Each company option with credit gets the subtitle "*Credit with this supplier: $X*". Below the select: a green note when the chosen company has credit, else a blue note naming the selectable company with the most credit plus a one-click **Use {company}** button. A 403 (e.g. Experts) just means no hints — the picker is unchanged. |
| Dialog | `components/ChangePoCompanyDialog.vue` — company radios from `/supplier-credits/lookup/companies`, each marked "(current)" and/or with a "$X credit" chip (`balances`, optional); pre-selects the company with the most credit. Loads the chosen company's warehouses and asks for one **only when** that company has linked warehouses and the current destination is not among them (the API's rule, applied up front). Always shows the PDF re-branding warning. `PATCH /purchase-orders/{id}/company-preset`; API errors shown in an `aria-live` alert. |
| PO page | `pages/purchase-orders/[id].vue` header: green **Credit: $X** chip when the PO's company has credit (links to `/supplier-credits?account={id}` for users with the menu permission); blue "*{Other} credit: $X*" chip when only another company has some; **Change company** button when `po.canChangeCompany`, the role is SuperAdmin/Admin/AHM and no Final Invoice locks the PO — otherwise a small icon whose tooltip gives `changeCompanyBlockedReason`. After a change: reload the PO and the credit chip, snackbar reminds to re-send the PDF. `?changeCompany=1` (from E6) opens the dialog once and is removed from the URL. |
| Backend | `CompanyPresetWarehousesController` read access widened to **AHM** (AHM may change a PO's company and must be able to pick the new company's warehouse). Link/unlink stay Admin/SuperAdmin via their own attributes. |

Verified without running the app: the three touched `.vue` files compile with `@vue/compiler-sfc`;
API builds with 0 errors; request/response field names grepped against `ChangePOCompanyRequest`,
`SupplierCreditCompanyBalance`, `SupplierCreditOption`, `WarehouseResponse` and `POResponse`.
**Not opened in a browser.**

Known limit (unchanged behaviour): the PO page and the PATCH both use the existing
`CanAccessAsync`, so a non-admin (e.g. AHM) only reaches POs they are assigned to.

### 6.7 Wallet pages

`client/app/pages/payment-control/[id].vue` (~line 324 and ~339) and `payment-control/index.vue`:
icon `mdi-cash-refund` and the server-provided name for `fromType/toType === 'SupplierCredit'`.
No other wallet UI changes.

### 6.8 Done when

Every wiring point is re-read and grepped (flag → nav → guard → page; suggestion → apply → reloads).
There is no typecheck script, so state in the summary that the frontend was **not run**. Do not
start the dev server.

---

## 7. Decisions (defaults chosen — change before implementation if wrong)

| # | Question | Default |
|---|---|---|
| D1 | Currency of credit | USD only in v1; `Currency` column ready for later. |
| D2 | Where credit comes from | Manual credit note + wallet prepayment + admin adjustment. Auto-credit from cancelled/returned paid POs is v2. |
| D3 | Does a wallet top-up need a POP / final bank amount? | No in v1 — it is recorded immediately. Bank fees on a prepayment are entered as a normal wallet withdrawal. |
| D4 | Can a PO change company after creation? | Yes, until the first payment (wallet or credit). Admin/SuperAdmin/AHM. |
| D5 | Is credit applied automatically? | **Never.** The system suggests; a person applies. |
| D6 | Which company decides credit: `PO.CompanyPresetId` or `PR.CompanyPresetId`? | The PO's. If an open PR has a different company, the suggestion shows a warning and the PR is updated when the PO company changes. |

---

## 8. Epics & order

Order: **E1 → E2 → E3 → E4**, frontend **E5–E7** can start once E3's DTOs exist.
E2 must land before E3 — it is the change that stops double payment.

| Epic | Owner | Status |
|---|---|---|
| E1 — Schema | Database | ✅ Done (2026-09-27) — see §4.5 |
| E2 — Single paid rule | Backend | ✅ Done (2026-09-27) — see §5.6; DB tests not yet run |
| E3 — Credit service & API | Backend | ✅ Done (2026-09-27) — see §5.7; DB tests not yet run |
| E4 — Change PO company | Backend | ✅ Done (2026-09-27) — see §5.8; DB tests not yet run |
| E5 — Supplier Credits page | Frontend | ✅ Done (2026-09-27) — see §6.3a; not opened in a browser yet |
| E6 — Payment suggestion | Frontend | ✅ Done (2026-09-27) — see §6.4a; not opened in a browser yet |
| E7 — PO picker & PO page | Frontend | ✅ Done (2026-09-27) — see §6.6a; not opened in a browser yet |
| E8 — Rollout | Sam | ⬜ Not started |
| E9 — Credit proofs (change request) | DB + Backend + Frontend | ✅ Done (2026-09-27) — see §9; DB tests not yet run, not opened in a browser |
| E10 — Credit lines (buy now, pay back) | DB + Backend + Frontend | ✅ Done (2026-09-27) — E10a §10.8, E10b §10.9, E10c §10.10; DB tests not yet run, not opened in a browser |

### E1 — Schema (Database) ✅

- [x] `SupplierCreditAccount` + `SupplierCreditEntry` entities in the Sales module, with direction/source constants
- [x] DbSets + EF configuration (precision, rowversion, NoAction FKs, 8 CHECK constraints, filtered/unique indexes)
- [x] Migration `20260927045737_AddSupplierCredits` — only the two new tables; `Down()` verified
- [x] Model snapshot in sync (`has-pending-model-changes` clean)
- [x] `PaymentTransaction` comment documents `ToType/FromType = "SupplierCredit"`
- [x] `db-schema-reference.md` updated
- [x] `db-summary-queries.sql`: H4 balances, H5 paid per PR, K3a–c integrity checks
- [x] API + test project build (Release)
- [ ] Migration applied to the database → **Sam, in E8**

### E2 — Single paid rule (Backend) ✅

- [x] `Sales/Services/SupplierPaymentLedger.cs`: `PaidByPrAsync`, `PaidByPoAsync`, `CreditAppliedByPrAsync` (wallet `Withdraw`/`Supplier` + net credit `Out − In` per PR)
- [x] `Sales/Services/SupplierPaymentCompletion.cs`: extract PR/PO status completion from `FinalizeAmount`
- [x] P1 `SupplierPaymentsController.SaveDraft` uses the ledger helper
- [x] P2 `SupplierPaymentsController.FinalizeAmount` uses the ledger helper + completion helper
- [x] P3 `SupplierPaymentsController.History` also returns credit applications (`category = "Supplier Credit"` / `"Supplier Credit Reversal"`, `rowKey`)
- [x] P4 `PaymentBoxService.GetSupplierPaidAmountsAsync` adds credit applied
- [x] P5 `StockPurchaseOrderService` raw SQL adds credit (`UNION ALL` over `SupplierCreditEntries`)
- [x] `IPaymentLedgerService.GetCreditAppliedAmountsAsync` + `PaymentRequestResponse.CreditApplied`
- [x] Regression tests written (`SupplierPaymentLedgerTests`, 5 tests); wallet-only behaviour unchanged by construction
- [ ] Run the DB tests with `PROCUMENT_TEST_SQL` set → **Sam** (skipped here, no test SQL Server configured)

### E3 — Credit service & API (Backend) ✅

- [x] `Sales/DTOs/SupplierCreditDTOs.cs` matching §3.4 (§3.4 updated to the built shape)
- [x] `ISupplierCreditService` / `SupplierCreditService` with the §5.2 write algorithm (Serializable, UPDLOCK+HOLDLOCK / RowVersion, idempotent `RequestId`, 409 on conflict)
- [x] `SupplierCreditsController` read endpoints: list (paged, `totalAmountSum`), filter-options, account, entries, balances, PO suggestion
- [x] Write endpoints: credit-note (multipart + file), wallet-top-up, adjustment, apply to PO, reverse, file download
- [x] Role gates per §3.3; `[Auditable]` on every write
- [x] `SupplierPaymentCompletion` backward path (`ReopenAsync`) used by reversals
- [x] `PaymentBoxService`: `SupplierCredit` labels in `GetByIdAsync`, `GetAllTransactionsAsync`, `BuildRowAsync`
- [x] Credit-linked wallet rows protected from edit/delete; 400 instead of 500 in `PaymentBoxesController`
- [x] A PR with credit history cannot be deleted
- [x] Register service in `SalesModule.cs`
- [x] Tests written (`SupplierCreditServiceTests`, 8 tests: §5.4 1–7 + guards); (9) was done in E2
- [ ] Run the DB tests with `PROCUMENT_TEST_SQL` set → **Sam**

### E4 — Change PO company (Backend) ✅

- [x] `PATCH /purchase-orders/{id}/company-preset` with rule 8 checks (no payments, no credit, no draft, warehouse validity, past-payment status, Final-Invoice lock) — "paid" via `IPaymentLedgerService`
- [x] Rule in one place (`IPurchaseOrderCompanyService`), shared with the credit suggestion
- [x] Updates the PO's PRs' `CompanyPresetId`; clears a stale `PreferredWalletId`
- [x] `POResponse.canChangeCompany` + `changeCompanyBlockedReason`
- [x] Tests §5.4 (8) — 3 tests
- [ ] Run the DB tests with `PROCUMENT_TEST_SQL` set → **Sam**

### E5 — Supplier Credits page (Frontend) ✅

- [x] Feature flag `supplierCredits`: `auth.ts`, `default.vue` nav, `menu-access.vue`, `auth.global.ts` `FEATURE_GATED_PREFIXES` **+ backend `KnownFeatures`**
- [x] `pages/supplier-credits/index.vue` on `DataListPage` (server-side, total-sum chip, cascading filters, sortable columns)
- [x] Ledger drawer with reverse action (`SupplierCreditLedgerDrawer.vue`)
- [x] `components/SupplierCreditDialog.vue` (credit note / wallet top-up / adjustment) — new `requestId` per dialog open (`newRequestId()`, HTTP-safe)
- [x] List query uses `supplierIds` / `companyPresetIds` / `sortBy`+`sortDesc`
- [x] Lookup endpoints so Payment users can pick suppliers and companies
- [ ] Open the page in a browser and try each dialog → **Sam** (after E8's migration)

### E6 — Payment suggestion (Frontend) ✅

- [x] `components/SupplierCreditSuggestion.vue` (three states, confirm dialog, `requestId`); shows `cannotApplyReason` when `!canApply`, and a hint when `walletPaymentInProgress`
- [x] Mounted in the withdraw panel of `pages/payment/index.vue`; reloads queue/PRs/history/draft after apply; "done" only when `poPaymentStatus === 'Submitted'`
- [x] "Supplier Credit" / "Supplier Credit Reversal" history chips; history `v-for` keyed on `payment.rowKey`
- [x] `SupplierCredit` icon on `payment-control/[id].vue` and `payment-control/index.vue`; edit/delete hidden on credit rows
- [ ] Try the three states in a browser → **Sam** (after E8's migration)

### E7 — PO picker & PO page (Frontend) ✅

- [x] Credit hints in the Company Preset picker of `pages/purchase-orders/index.vue` (+ one-click "Use {company}")
- [x] Credit chip + "Change company" action on `pages/purchase-orders/[id].vue`; blocked reason as a tooltip
- [x] `?changeCompany=1` opens the dialog once, then the query is dropped
- [x] Credit chip links to the ledger (`/supplier-credits?account={accountId}`) for users with the menu permission
- [x] `components/ChangePoCompanyDialog.vue` (balances per company, warehouse select only when required — decided up front instead of after a 400, PDF re-send warning); the page reloads the PO after the PATCH
- [x] AHM can read `/companypresets/{id}/warehouses`
- [ ] Try the picker hint, the chip and the dialog in a browser → **Sam** (after E8's migration)

### E8 — Rollout (Sam)

- [ ] Run the DB tests against a throwaway SQL Server (19 tests; they create and drop their own database):
      set `PROCUMENT_TEST_SQL`, then
      `dotnet test src/Procument.OurInventory.Tests -c Release --filter "FullyQualifiedName~SupplierCredit|FullyQualifiedName~SupplierPaymentLedger|FullyQualifiedName~PaymentBoxRateCorrection"`
- [ ] Apply migrations `20260927045737_AddSupplierCredits` (two new tables; no changes to existing data) and
      `20260927063916_AddSupplierCreditProofs` (E9) and `20260927093537_AddSupplierCreditLines` (E10;
      `dotnet ef database update` applies all three in order)
- [ ] Restart the API and the client (new endpoints, new feature flag)
- [ ] Grant `supplierCredits` in Menu Access (Admin / Payment / AHM users; SuperAdmin always sees it)
- [ ] Payment-role users: check the "Payment" nav group now shows Wallets / Supplier Credits as granted (see §6.3a)
- [ ] Enter opening balances as credit notes (Supplier Credits → Add Credit Note)
- [ ] Smoke test the example from §1: PO for Supplier A under Company A → payment page shows Company B's credit → Change PO company → apply $800 → ledger shows the balance counting down to $400
- [ ] Run `db-summary-queries.sql` K3a–c — all return 0 rows

---

## 9. E9 — Credit proofs (change request, 2026-09-27)

**Request (Sam):** for each credit, keep proof of where it came from — *from which PO* (one of our POs
**or** custom text), the **supplier PI document**, the **credit memo** (a string) and **other uploaded
documents**.

### 9.1 Decisions

| # | Question | Decision |
|---|---|---|
| P1 | Which entries need an origin? | **Credit notes** — required, exactly one of *our PO* / *text*. Opening balances use text (e.g. "Opening balance 2026"). Top-ups (origin = the wallet), adjustments and applications don't ask for one. |
| P2 | Which POs can be the origin? | Only POs **with the same supplier** (any status, any company). |
| P3 | Credit memo | The existing "credit-note number" field, **renamed `CreditMemo`** and widened to 200 characters. |
| P4 | Documents | Any number per entry, in three types: **Supplier PI** (at most one per upload), **Credit memo**, **Other**. Allowed: pdf, png, jpg, jpeg, webp, doc, docx, xls, xlsx, eml, msg — ≤ 10 files per request. |
| P5 | Adding proof later | Allowed on any entry from the ledger (**SuperAdmin/Admin/Payment**). Documents are evidence only, so this doesn't break the append-only rule for money. Deleting documents: not offered (append-only too). |
| P6 | Migration | A **new** migration (`AddSupplierCreditProofs`), not an edit of `AddSupplierCredits`, in case that one is already applied. |

### 9.2 Database — `20260927063916_AddSupplierCreditProofs` (hand-edited, no data loss)

- `SupplierCreditEntries`: `Reference` **renamed** to `CreditMemo` (`nvarchar(200)`); new
  `SourcePurchaseOrderId` (FK → `PurchaseOrders`, NoAction, filtered index) and `SourcePoReference`
  (`nvarchar(100)`); CHECK `CK_SupplierCreditEntries_SourcePo` = never both. Existing credit notes get
  `SourcePoReference = '(not recorded)'`.
- New `SupplierCreditDocuments` (`EntryId`, `Category` CHECK SupplierPI/CreditMemo/Other, `FileName`,
  `OriginalFileName`, `SizeBytes`, `UploadedAt`, `UploadedByUserId`).
- The old single `DocumentFileName` is **copied into `SupplierCreditDocuments`** (category CreditMemo,
  original name recovered from the `{requestId}-` prefix) **before** the column is dropped. `Down()`
  reverses all of it.
- EF's scaffold had renamed `Reference` → `SourcePoReference` (wrong meaning) and dropped the file
  column before copying; both corrected by hand. Model snapshot in sync; SQL script reviewed.
- Docs: `db-schema-reference.md` (new table, new columns, "two different PO links" note).

### 9.3 Backend

| Item | Where |
|---|---|
| Entities | `SupplierCreditEntry` (+ `SourcePurchaseOrderId`, `SourcePoReference`, `CreditMemo`, `Documents`), new `SupplierCreditDocument` + `SupplierCreditDocumentCategories` |
| DTOs | `AddSupplierCreditNoteRequest` (+ origin, `CreditMemo`, `SupplierPi`, `Documents`), `AddSupplierCreditDocumentsRequest`, `SupplierCreditDocumentRow`, `SupplierCreditPoOption`; `SupplierCreditEntryRow` gains origin fields + `Documents`, loses `Reference`/`HasDocument`; service input record renamed `SupplierCreditUpload` |
| Service | Credit note validates the origin (exactly one; PO must belong to the supplier), memo ≤ 200, files (type, count, one PI). Files are saved inside the same ledger transaction with failure clean-up. `AddDocumentsAsync`, `SearchPurchaseOrdersAsync`, `OpenDocumentAsync(documentId)`. Ledger pages load all their documents in one extra query. Reversals copy the credit memo. |
| Controller | `lookup/purchase-orders`, `entries/{id}/documents` (POST), `documents/{id}/file` (GET) — see §3.4 |
| Tests | 2 new in `SupplierCreditServiceTests` (origin PO + memo + 3 documents + attach later without balance change; validation of origin/supplier/file type/one PI). Existing tests updated to give an origin. |

### 9.4 Frontend

- **Add Credit Note dialog** (`SupplierCreditDialog.vue`): a "Proof" section — radios *One of our
  POs* / *Other (type it)*; PO autocomplete (the supplier's POs, searchable, shows company · status ·
  total; disabled until a supplier is chosen); origin text; **Credit memo**; **Supplier PI document**
  (single file); **Other documents** (multiple, ≤ 10 files in total).
- **Ledger drawer** (`SupplierCreditLedgerDrawer.vue`): each entry shows *From PO-123* (link) or the
  origin text, the credit memo, and one chip per document (click = preview). New **Attach documents**
  action per entry (type + files).
- `utils/supplierCredits.ts`: new types, labels, accepted file types, max files.

Verified without running the app: API + tests build with 0 errors; `has-pending-model-changes`
clean; migration SQL reviewed; the three `.vue` files compile with `@vue/compiler-sfc` and the util
parses; every multipart field name matches the DTOs; no leftover use of the removed fields.
18 supplier-credit/ledger tests are discovered but **skipped here (no `PROCUMENT_TEST_SQL`)**.

### 9.5 Checklist

- [x] P1–P6 decided (change any before rollout if wrong)
- [x] Migration `AddSupplierCreditProofs` (rename, new columns + table, data copy, `Down()`)
- [x] Backend: origin validation, multi-file upload, attach-later, PO lookup, per-document download
- [x] Tests written (2 new, existing updated)
- [x] Frontend: dialog proof section, ledger origin/memo/document chips, attach dialog
- [x] Docs: schema reference, §3.4 contract
- [ ] Run the DB tests and try it in the browser → **Sam** (E8)

---

## 10. E10 — Supplier credit lines (buy now, pay back later) — DESIGN, not built

**Request (Sam):** besides credit the supplier holds *for us* (§1–§9, "prepaid credit"), a supplier
can give one of our companies a **credit line**: e.g. Company A may buy up to **$50,000** from
Supplier A without paying first. Using it pays the PO, but creates a **debt**. A second tab in
Supplier Credits must remind us what we owe; paying it back goes through the **normal wallet
payment flow** (wallet transaction, POP upload, final bank amount / bank fee), and brings the line
back to its full limit so it can be used again.

### 10.1 The idea: a credit line is the same ledger, with a different kind of account

Treat the credit line as an account whose **balance = credit still available**:

| Event | Prepaid credit (today) | Credit line (new) |
|---|---|---|
| Money comes in | credit note / wallet top-up → **In** | supplier grants/raises the limit → **In** (`CreditLimit`) |
| Used on a PO | **Out** `POApplication` (unchanged) | **Out** `POApplication` — *exactly the same code path* |
| Undo a use | **In** `Reversal` | **In** `Reversal` (same) |
| Pay the supplier back | — | **In** `Repayment`, linked to the POP wallet withdrawal |
| What we owe | — | **Owed = CreditLimit − Balance** |

Because using a credit line is an ordinary `Out` + `POApplication` entry, **the single "paid" rule
(E2), the apply endpoint, the completion helper, the payment-page suggestion and every report keep
working without changes.** `Balance >= 0` (existing CHECK) already means "you cannot spend more than
the limit".

### 10.2 Database — the minimum

| Table | Change | Why |
|---|---|---|
| `SupplierCreditAccounts` | **+ `Kind` `nvarchar(20)` NOT NULL DEFAULT `'Prepaid'`** (`Prepaid` \| `CreditLine`) | one supplier × company can have both |
| | **+ `CreditLimit` `decimal(18,2)` NULL** | only for `CreditLine`; cached, moved by `CreditLimit` entries |
| | unique index → `(SupplierId, CompanyPresetId, Currency, Kind)`; CHECK `Kind = 'Prepaid' OR (CreditLimit IS NOT NULL AND Balance <= CreditLimit)` | |
| `SupplierCreditEntries` | **no new columns.** CHECK `CK_SupplierCreditEntries_Source` gains `'CreditLimit'` and `'Repayment'`; `CK_SupplierCreditEntries_TopUp` also requires `PaymentTransactionId` for `Repayment` | constraint-only change |
| `PaymentRequests` | **+ `SupplierCreditAccountId` `bigint` NULL** (FK → `SupplierCreditAccounts`, filtered index) | a repayment reuses a **payment request with no PO** (`POId` is already nullable), so the existing pending-draft fields (`PendingWalletId`, `PendingPaymentAmount`, `PendingExchangeRate`, `PendingUploadId`), PR numbering and statuses are reused as-is |

That is **3 columns on 2 existing tables, no new table**, one migration. Existing rows become
`Prepaid` by the default, so nothing already built changes meaning.

`PaymentRequest` lives in Purchasing, which cannot reference Sales entities: the property is a plain
`long?` there; the FK is configured in `AppDbContext`.

### 10.3 Pay-back flow — same three steps as a PO payment

1. **Save repayment details** (Credit Lines tab → *Pay back*): amount (≤ owed), wallet, rate for
   non-USD wallets → creates a payment request `{ POId = null, SupplierCreditAccountId, CompanyPresetId,
   Amount, Status = 'READY FOR POP', Pending* }`. It gets a normal PR number.
2. **Upload POP** → wallet `PaymentTransaction` `Withdraw`, `ToType = 'SupplierCredit'` (already
   labelled "Supplier Credit · {Supplier}", already excluded from every "paid on a PO" figure, already
   protected from edit/delete), `ToSupplierId`, `PaymentRequestId`, `PopUploadId`, POP file stored
   with the supplier's credit documents. **In the same transaction:** ledger `In` / `Repayment`
   entry with `PaymentTransactionId` → owed goes down, available goes up. PR → `WAITING FOR FINAL
   AMOUNT`.
3. **Save final bank amount** → the difference is a `BankFee` wallet row on the same PR (as today);
   PR → `PAID` (or `PARTIALLY PAID` when less than requested was sent).

Rules: one open repayment per credit line at a time (like one open POP per PO); repayment amount ≤
owed at every step; the ledger entry of a repayment **never** carries `PaymentRequestId` (only PO
applications do — that keeps the paid rule untouched); rate correction on a repayment POP must
re-balance its bank fee exactly like a PO POP (extend the `ToType == "Supplier"` condition in
`PaymentBoxService.UpdateExchangeRateAsync` to `SupplierCredit`).

### 10.4 Screens

- **Supplier Credits → tab 1 "Prepaid credit"** — today's page (`kind=Prepaid`).
- **Tab 2 "Credit lines"** — per supplier × company: **Limit · Used (owed) · Available · Oldest unpaid
  use · Open repayment**. Rows with owed > 0 are highlighted "*Pay back $X to {Supplier}*"; the tab
  shows a badge with the number of lines that have a debt. Actions: *Set / change limit*
  (Admin), *Pay back* (three-step dialog above, reusing the POP upload and final-amount parts of the
  payment page), ledger drawer (same component; labels: "Limit granted/changed", "Used on PO",
  "Repayment").
- **Payment page suggestion** — lists both kinds for the PO's company: "*Company A: $12,000 prepaid
  credit*" and "*Company A: credit line $38,000 available of $50,000*"; the user picks which to use.
  Using the line shows "*This amount must be paid back to {Supplier}*".
- **Wallet pages** — the repayment appears as a normal withdrawal with its POP and bank fee.

### 10.5 API delta — ✅ as built in E10b (the contract for E10c)

```ts
// List + filter-options: new query params
//   kind=Prepaid (default) | CreditLine;  onlyOwing=true (credit lines only; onlyWithBalance applies to Prepaid only)
//   sortBy: + owed | creditLimit  (default for CreditLine: owed desc)
//   totalAmountSum = Σ balance (Prepaid)  or  Σ owed (CreditLine)
interface SupplierCreditAccountRow {            // + these fields
  kind: 'Prepaid' | 'CreditLine'
  creditLimit: number | null                    // CreditLine only
  owed: number                                  // CreditLine: creditLimit − balance; Prepaid: 0
  oldestUnpaidAt: string | null                 // first use since the line was last fully available
  openRepaymentId: number | null                // repayment PR waiting for POP / final amount
}                                               // balance = what is still AVAILABLE on a credit line

GET  /supplier-credits/summary                  // { prepaidTotal, creditLinesOwingCount, creditLinesOwedTotal } — tab badge
GET  /supplier-credits/balances?supplierId=&kind=   // kind defaults to Prepaid (unchanged for existing callers); rows gain `kind`

// Suggestion (GET /supplier-credits/po/{poId}/suggestion) gains:
//   creditLine: { accountId, creditLimit, available, owed, suggestedAmount } | null   — the PO company's line
//   otherCompanies[].kind — both kinds now (prepaid first, then lines), balance = usable amount
// Apply: POST /supplier-credits/po/{poId}/apply  { paymentRequestId, amount, requestId, kind?: 'Prepaid'|'CreditLine' }

PUT  /supplier-credits/credit-lines             // { supplierId, companyPresetId, creditLimit, notes?, requestId } → WriteResult
                                                // SuperAdmin/Admin. Creates the line or writes a CreditLimit In/Out entry
                                                // for the difference; refused below what is owed or when unchanged.
GET  /supplier-credits/{accountId}/repayments   // SupplierCreditRepaymentRow[] newest first
POST /supplier-credits/{accountId}/repayments/draft                  // { walletId, amount, exchangeRate? } → RepaymentRow
POST /supplier-credits/{accountId}/repayments/{prId}/pop             // multipart field `file` → WriteResult (Repayment entry)
POST /supplier-credits/{accountId}/repayments/{prId}/final-amount    // { finalWalletAmount } → RepaymentRow

interface SupplierCreditRepaymentRow {
  id: number; prNumber: number | null; status: 'READY FOR POP' | 'WAITING FOR FINAL AMOUNT' | 'PAID'
  amount: number                                // USD paid back
  walletId: number | null; walletName: string | null; walletCurrency: string | null; exchangeRate: number | null
  popTransactionId: number | null; popFileName: string | null   // POP download: GET /supplier-payments/{popTransactionId}/file
  walletAmount: number | null                   // POP debit in wallet currency
  bankFee: number | null                        // "Bank Fee and Others", after the final amount
  createdAt: string
}
```

Repayment roles: the controller's (SuperAdmin/Admin/Payment/AHM), like PO supplier payments.

### 10.6 Decisions — ✅ Sam: "use your defaults" (2026-09-27)

| # | Question | Decision (the default) |
|---|---|---|
| L1 | Revolving, or must the debt be **fully** repaid before the line can be used again? | **Revolving:** available = limit − owed at any time (pay back $10K → $10K usable again). Option: "block new use while owed > 0". |
| L2 | When does paying back restore the credit — at **POP upload** or at **final bank amount**? | **POP upload** (the money has left; the final amount only adds the bank fee). |
| L3 | Payment terms: should a line have "pay back within N days" and show overdue in red / in the Action Center? | Not in v1; would add one column (`PaymentTermDays`) later. |
| L4 | Should repayments also appear in **Payment Withdraw** (the payment team's queue), or only in Supplier Credits → Credit lines? | **Only in Credit lines** (+ Wallets). |

### 10.7 Epics

- **E10a DB** — migration `AddSupplierCreditLines` (§10.2). ✅ Done — see §10.8.
- **E10b Backend** — kind-aware accounts, credit-limit writes, apply by kind, repayment draft/POP/final
  amount, rate-correction fix, tests (limit, use, over-limit refused, repay restores, repayment
  POP/fee in wallet, PO paid totals unchanged by repayment). ✅ Done — see §10.9.
- **E10c Frontend** — tabs, credit-lines list + reminder badge, limit dialog, pay-back dialog, suggestion
  shows both kinds, ledger labels. ✅ Done — see §10.10.

### 10.8 As built (E10a — database)

| Item | Where |
|---|---|
| Entities | `SupplierCreditAccount` + `Kind` (constants `SupplierCreditAccountKinds.Prepaid/CreditLine`), `CreditLimit`, computed `Owed` (not mapped); `SupplierCreditSources` + `CreditLimit`, `Repayment`; `PaymentRequest.SupplierCreditAccountId` (plain `long?`, no navigation — Purchasing must not reference Sales) |
| EF config | `AppDbContext`: account kind/limit + CHECK; unique index now includes `Kind`; entry CHECKs; `PaymentRequest` FK declared with `HasOne<SupplierCreditAccount>()` + filtered index + CHECK |
| Migration | `src/Procument.Data/Migrations/20260927093537_AddSupplierCreditLines.cs` — scaffolded, reviewed; `Down()` hand-guarded (throws a clear error instead of failing half-way when credit lines exist) |
| Docs | `db-schema-reference.md` (Kind, CreditLimit, PaymentRequests repayment link, "how much do we owe" trap); `db-summary-queries.sql` H4 by kind, **H6 owed on credit lines** (the reminder query), **K3d** limit == ledger |

Schema delta (the whole of it): **3 columns** (`SupplierCreditAccounts.Kind`, `.CreditLimit`,
`PaymentRequests.SupplierCreditAccountId`), 1 FK, 2 indexes (one replaced), and CHECK constraints:

| Constraint | Rule |
|---|---|
| `CK_SupplierCreditAccounts_Kind` | Prepaid ⇒ no limit; CreditLine ⇒ limit set and `Balance <= CreditLimit` |
| `CK_SupplierCreditEntries_Source` | + `CreditLimit`, `Repayment` |
| `CK_SupplierCreditEntries_TopUp` | `WalletTopUp` **and `Repayment`** need `PaymentTransactionId` |
| `CK_SupplierCreditEntries_PaymentRequestOnlyOnApplications` | **new** — only `POApplication`/`Reversal` may carry `PaymentRequestId`: the E2 paid rule is now protected by the database, not just by convention |
| `CK_PaymentRequests_PoOrCreditLine` | a PR pays a PO **or** a credit line, never both |

All existing rows satisfy every new constraint (existing accounts → `Prepaid` by default; no PR has
the new column set). Verified: model clean before and after (`has-pending-model-changes`), SQL
script reviewed, API + tests build with 0 errors. **Not applied to any database.**

**Carry-over for E10b (must do first):** until now "the account" of a supplier × company was unique.
Every place that looks one up must now say **which kind** — otherwise, once a credit line exists,
prepaid code could pick the credit line:

- `SupplierCreditService.LockAccountAsync(supplierId, companyPresetId)` → add `Kind` to the raw SQL.
- `GetOrCreateAccountAsync` (credit note, top-up, In adjustment) → `Prepaid`.
- `GetSuggestionAsync` (`accounts.FirstOrDefault(a => a.CompanyPresetId == companyId)`), `GetBalancesAsync`,
  list / filter-options → filter or group by `Kind`.
- `ApplyToPoAsync` → account chosen by the request's `kind`.
- `PaymentRequestService` / `/paymentrequests` consumers → repayment PRs have `POId = null`; confirm
  the PR PDF generator and wallet page ignore them (they filter by PO today).

### 10.9 As built (E10b — backend)

| Item | Where |
|---|---|
| Kind-aware lookups (the §10.8 carry-over) | `SupplierCreditService`: `LockAccountAsync(supplier, company, kind)`; `GetOrCreatePrepaidAccountAsync` (credit notes, top-ups, In adjustments); Out adjustments lock the **prepaid** account; list/filter-options filter by `kind`; balances default to Prepaid; suggestion separates own prepaid / own credit line / other companies |
| Guard | `Post()` refuses to take a credit line **above its limit** (message says what is owed) — so a repayment or reversal can never "over-repay"; the DB CHECK is the backstop |
| Credit limits | `SetCreditLineAsync` — creates the line (`Kind = CreditLine`, limit 0) then posts a `CreditLimit` In/Out entry for the difference; refused when unchanged or below what is owed. `ReverseAsync` refuses to reverse a `CreditLimit` entry ("change the limit instead") |
| Apply by kind | `ApplyToPoAsync` uses `request.Kind` (default Prepaid; unknown kind → 400). Same Out/`POApplication` entry, same paid rule and completion — nothing else changed |
| Pay back | `SaveRepaymentDraftAsync` (one open repayment per line; a new draft updates it; refused while one waits for its final amount; ≤ owed), `UploadRepaymentPopAsync` (inside one ledger transaction: POP file → wallet `Withdraw`/`SupplierCredit` with `PopUploadId` → `Repayment` In entry; the upload id is the idempotency key so a retried upload never pays twice), `FinalizeRepaymentAsync` (bank fee row, PR `PAID`, pending fields cleared) — the same rules as `SupplierPaymentsController` |
| POP files | Saved under `Supplier Credits/{Supplier}/Our POP to Supplier/` with `PopInvoiceNumber = "Supplier Credits"`, so the existing `GET /supplier-payments/{txId}/file` (wallet pages) serves repayment POPs unchanged |
| Wallet | `PaymentBoxService.UpdateExchangeRateAsync` now re-balances the bank fee for `SupplierCredit` POPs too (was `Supplier` only) |
| PR endpoints | `PaymentRequestService.UpdateAmountAsync / UpdateStatusAsync / DeleteAsync` refuse repayment PRs ("manage it on Supplier Credits → Credit lines"); `PaymentRequestsController` status/delete now return 400 instead of 500 for these (and for the existing "recorded payments" delete guard) |
| Controller | `summary`, `balances?kind`, `PUT credit-lines`, `{accountId}/repayments` (+ `draft`, `{prId}/pop`, `{prId}/final-amount`), all `[Auditable]` writes |
| Concurrency | A double POP upload that collides on `IX_PaymentTransactions_PopUploadId` replays the first upload's entry instead of a 500 |
| Tests | `SupplierCreditServiceTests.CreditLines.cs` (the class is now `partial`), 4 tests: limit raise/lower/"not below owed"/no limit reversal; prepaid + line side by side, suggestion, default kind, unknown kind, list `onlyOwing` + owed total; the full pay-back (draft cap, one open draft, PR endpoint guards, POP → wallet + owed down, retried upload, PO paid unchanged, final amount → bank fee, wallet balance, revolving reuse); rate correction re-balancing a repayment's fee. `AssertLedgerConsistentAsync` gained `kind` and the K3d limit check. |

Verified: API + tests build with 0 errors, no new warnings in touched files; 25 supplier-credit /
ledger / wallet tests discovered, **skipped here (no `PROCUMENT_TEST_SQL`)**. The app was not run.

**For E10c:** `balances` without `kind` still returns prepaid only, so the PO company picker and the
Change-company dialog keep working until they are taught about lines. `otherCompanies` in the
suggestion now contains both kinds — label them by `kind` ("credit line: $X available").

### 10.10 As built (E10c — frontend)

| Item | Where |
|---|---|
| Tabs | `pages/supplier-credits/index.vue` — **Prepaid credit** (the existing list) and **Credit lines**, with a red badge = number of lines that owe money (`/summary`). `?tab=lines` deep link; `?account={id}` still opens a ledger. Dialogs and the ledger drawer now live outside the lists so both tabs share them. |
| Credit lines list | `components/SupplierCreditLinesList.vue` — `DataListPage` (`page-key="supplier-credit-lines"`, `kind=CreditLine`), Supplier/Company cascading filters, "Only with a debt" switch, header chip **"To pay back: $X"** (Σ owed). Columns: Limit · **Owed** (red with an alert icon, or "Nothing owed") · Available (with a used-bar) · Unpaid since (date + "N days ago") · Repayment ("In progress" when one is open). Actions: **Pay back** (SuperAdmin/Admin/Payment/AHM, when something is owed or a repayment is open), change limit (SuperAdmin/Admin), ledger. Header button **Set up credit line** (SuperAdmin/Admin). |
| Limit dialog | `components/SupplierCreditLineDialog.vue` — supplier/company (locked when editing), limit with "not below what is owed" and "unchanged" rules, notes; `PUT /supplier-credits/credit-lines` with a fresh `requestId`. |
| Pay-back dialog | `components/SupplierCreditRepaymentDialog.vue` — owed / limit / available; **1. Repayment details** (amount ≤ owed, wallet, rate for non-USD, wallet-debit preview) → **2. Upload the POP** (`FileDropZone`, same as PO payments) → **3. Final bank amount** with the bank-fee preview; "Paid back before" list with each POP previewed through `/supplier-payments/{txId}/file`. After each step the page refreshes the lists/badge and hands the dialog the new owed amount. |
| Ledger drawer | For credit lines: header **Owed · Limit · Available**, buttons **Pay back** / **Change limit**; entries labelled "Credit limit", "Used on PO (to pay back)", "Paid back"; limit entries cannot be reversed; reversing a repayment explains the money returns to the wallet and is owed again. |
| Payment page | `SupplierCreditSuggestion.vue` — offers the PO company's **prepaid credit and/or credit line** ("*$38,000 available of $50,000*"); a **Pay with** choice when both exist; using the line shows "*must be paid back*" and the confirm dialog shows available/owed afterwards; `kind` is sent to `apply`. A fully used line shows a warning to pay it back. Other companies are labelled by kind. |
| PO pages | `purchase-orders/[id].vue` — indigo **"Credit line: $X available"** chip (links to the Credit lines tab for users with the menu permission); the other-company chip names the kind. `purchase-orders/index.vue` company picker and `ChangePoCompanyDialog.vue` also load `kind=CreditLine` balances: option subtitles / chips show both, "best company" = prepaid + line available. |
| Shared | `utils/supplierCredits.ts` — `SupplierCreditKind`, account fields (`kind`, `creditLimit`, `owed`, `oldestUnpaidAt`, `openRepaymentId`), `SupplierCreditLineInfo`, `SupplierCreditRepaymentRow`, `SupplierCreditSummary`, labels for `CreditLimit`/`Repayment`, `supplierCreditEntryLabel()`. |

Verified without running the app: the ten touched `.vue` files compile with `@vue/compiler-sfc`,
the util parses with esbuild, every endpoint/field used by the new UI matches §10.5. **Not opened in
a browser.**

Smoke test for E8 (after the migrations): Credit lines → *Set up credit line* Supplier A / Company A
$50,000 → create a PO for Supplier A under Company A, accept its PR → Payment Withdraw → *Pay with:
credit line* $20,000 → the Credit lines tab shows the red badge and "Owed $20,000" → *Pay back*
$12,000 from a wallet → upload POP (owed drops to $8,000 at once) → final bank amount → the wallet
shows the withdrawal and the bank fee.
