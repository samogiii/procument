# Email to Gemini — Procurements Frontend Implementation

**From:** Sami (Frontend Senior Dev handoff)
**To:** Gemini
**Subject:** 🚀 Build the Procurements frontend — full spec inside
**Date:** 2026-04-24

---

Hey Gemini,

I'm your frontend senior dev on the Procument project. The backend for the new **Procurement** layer is fully shipped, built, and migrated — I need you to debug any wiring issues and build out all the frontend pieces. This brief is self-contained; read it end-to-end before writing code.

---

## 1. What this feature is

Between a Proforma Invoice being accepted and a Purchase Order being created, we now have a **Procurement** editing layer. It's a snapshot/clone of the full RFQ → Quote → selected-supplier chain that admins can edit (qty, unit price, supplier, supplier quotes) **without** touching the source `RFQItem`, `QuoteItem`, or `ProcumentRecord` rows. When admin clicks "Finalize", POItems are materialized from the edited snapshot and the Procurement becomes read-only.

Access: **SuperAdmin + Admin + explicitly assigned users**. Assignment is scoped to Procurement only — it does NOT propagate to the resulting PO/Quote/RFQ.

The backend is done. Your job is the UI layer.

---

## 2. Scope — what to build

### 2.1 New pages
- `client/app/pages/procurements/index.vue` — list page (paginated table)
- `client/app/pages/procurements/[id].vue` — detail page (expandable grid, modeled on `client/app/pages/procument/index.vue`)

### 2.2 Modify existing files
- `client/app/layouts/default.vue` — add a "Procurements" nav entry (icon: `mdi-clipboard-edit-outline`, NOT adminOnly — backend enforces per-item permissions)
- `client/app/components/BulkPermissionManager.vue` — extend `entityName` union to include `'Procurement'`, add `entityLabel` + `entityTitleKey` cases, add a `/procurements?page=1&pageSize=500` branch in `loadEntities()` (follow the existing `'PO'` case as a template)
- `client/app/pages/invoices/[id].vue` — add a "View Procurement" button in the header when a Procurement exists for that invoice. Fetch via `GET /procurements?search={invoiceNumber}` or surface `procurementId` on the invoice response (you may need to extend the invoice DTO; if backend work is required, flag it and I'll handle it).

---

## 3. API endpoints (already live)

Base path: `/api/procurements`. Auth: `Admin, SuperAdmin, Expert, Payment`.

| Method | Route | Body / Query | Returns |
|---|---|---|---|
| `GET` | `/procurements?page=1&pageSize=200&search=` | — | `PagedResult<ProcurementResponse>` |
| `GET` | `/procurements/{id}` | — | `ProcurementResponse` (full detail incl. items + supplier quotes + assigned users) |
| `PATCH` | `/procurements/{id}/items/{itemId}` | `UpdateProcurementItemRequest` | 200/400 |
| `POST` | `/procurements/{id}/items/{itemId}/supplier-quotes` | `UpsertSupplierQuoteRequest` | `ProcurementSupplierQuoteResponse` |
| `POST` | `/procurements/{id}/items/{itemId}/supplier-quotes/{sqId}/select` | — | 200 |
| `DELETE` | `/procurements/{id}/items/{itemId}/supplier-quotes/{sqId}` | — | 204 |
| `POST` | `/procurements/{id}/finalize` | `FinalizeProcurementRequest` (`{ notes? }`) | `FinalizeProcurementResponse` — **admin/superadmin only** |
| `POST` | `/procurements/{id}/cancel` | — | 200 — **admin/superadmin only** |

Use the existing `useApi()` composable (same pattern as `purchase-orders/index.vue`).

---

## 4. DTO shapes (keep types loose / `any` if you prefer, but here's the truth)

### `ProcurementResponse`
```ts
{
  id: number
  procurementNumber: string              // e.g. "PROC-123"
  status: 'Open' | 'InProgress' | 'Finalized' | 'Cancelled'
  createdAt: string
  finalizedAt?: string | null
  createdByUserId?: number | null
  finalizedByUserId?: number | null
  notes?: string | null

  invoiceId: number
  invoiceNumber?: string | null
  customerId?: number | null
  customerName?: string | null

  itemCount: number
  items: ProcurementItemResponse[]       // only populated on GET /{id}
  assignedUsers: ProcurementAssignedUser[]
  sourceAssignedUsers: ProcurementAssignedUser[]  // users assigned to source RFQ / Quote / Invoice
}
```

### `ProcurementItemResponse`
All three "snapshot" groups are **read-only** (display as provenance):
- RFQ snapshot: `sourceRfqId, sourceRfqItemId, rfqName, rfqExType, partNumberId, partNumberName, partNumberDescription, rfqQty, rfqCondition, rfqUnit, rfqPriority, rfqAlt, rfqNote`
- Quote snapshot: `sourceQuoteId, sourceQuoteItemId, quoteNumber, quoteUnitPrice, quoteQty, quoteCondition, quoteAlt, quoteLeadTimeDays`
- Supplier snapshot: `sourceProcumentRecordId, sourceSupplierId, supplierName, supplierPrice, supplierLeadTime, supplierCondition, supplierCertName, shippingCost`
- Invoice snapshot: `sourceInvoiceItemId, acceptedQty, acceptedUnitPrice`

**Editable** fields (the ones admins mutate — these drive the final POItems):
- `qty: number`
- `unitPrice: number`
- `currentSupplierId?: number | null` + `currentSupplierName?: string | null`
- `leadTime?: string`
- `condition?: string`
- `expectedDeliveryDate?: string | null`
- `alt?: string`
- `note?: string`
- `itemStatus: 'Open' | 'Sourcing' | 'Ready' | 'Cancelled'`

Also: `id, procurementId, sortOrder, createdAt, updatedAt, supplierQuotes: ProcurementSupplierQuoteResponse[]`

### `ProcurementSupplierQuoteResponse`
`id, procurementItemId, supplierId?, supplierName, price, qty, condition?, unit?, alt?, leadTime?, certName?, shippingCost?, note?, tagDate?, shippingPoint?, isSelected, sourceProcumentRecordId?, sortOrder, createdAt, addedByUserId?`

### `UpdateProcurementItemRequest` (PATCH body — all fields optional)
`qty?, unitPrice?, currentSupplierId?, leadTime?, condition?, expectedDeliveryDate?, alt?, note?, itemStatus?`

### `UpsertSupplierQuoteRequest` (POST body)
`id?` (pass to update; omit to create), `supplierId?`, `supplierName?`, `price, qty`, `condition?, unit?, alt?, leadTime?, certName?, shippingCost?, note?, tagDate?, shippingPoint?`

### `FinalizeProcurementResponse`
`{ procurementId, createdPOIds: number[], createdPOItemIds: number[] }`
POs are NOT grouped+created by finalize — it only drops unassigned `POItem`s (POId=null). The admin then groups them on `/purchase-orders` (existing flow). After finalize, redirect to `/purchase-orders` so the user can see their new unassigned items ready to be grouped.

---

## 5. UI patterns — follow these existing files

### For the list page (`procurements/index.vue`)
**Mirror** `client/app/pages/purchase-orders/index.vue` but simpler — no tabs, just one table.

Columns:
- `#` (row index)
- `Procurement Number` (monospaced, accent color — use `.cell-pn` class from purchase-orders page)
- `Invoice Number`
- `Customer`
- `Items` (count)
- `Status` (v-chip, colors below)
- `Assigned Users` (admin-only column — chips pattern from `purchase-orders/index.vue` lines 125-139)
- `Created` (localeDateString)
- row action: arrow button → `/procurements/{id}`

Status chip colors:
- `Open` → `grey`
- `InProgress` → `warning`
- `Finalized` → `success`
- `Cancelled` → `error`

Toolbar: for admin, show `[Assign Users]` button → opens `<BulkPermissionManager v-model="showAssignDialog" entity-name="Procurement" />`.

Per-row assigned-users loading: after list loads, for each procurement, parallel fetch `GET /permissions/Procurement/{id}` into `po._assignedUsers` (same pattern as `loadPurchaseOrders` lines 628-637).

### For the detail page (`procurements/[id].vue`)
**Mirror** `client/app/pages/procument/index.vue` (the RFQ-side procurement page that has the expandable grid with supplier-quote rows).

Header:
- Title: `PROC-123` + status chip
- Breadcrumb: Procurements > PROC-123
- Meta strip: Invoice #, Customer, Created, Finalized-at (if set)
- Action buttons (admin only):
  - `[Finalize → Create PO Items]` — confirm dialog, then POST `/procurements/{id}/finalize`, then redirect to `/purchase-orders`
  - `[Cancel]` — confirm dialog, POST `/procurements/{id}/cancel`
  - Disable both when `status` is `Finalized` or `Cancelled`.

For each `ProcurementItem` (expandable row):

**Collapsed row** shows:
- Sort order / #
- Part Number (accent) + Description tooltip
- `Qty` (inline editable `<input type="number">`, onChange PATCH with `{ qty }`)
- `UnitPrice` (inline editable, PATCH with `{ unitPrice }`)
- Total = qty × unitPrice
- Current Supplier name
- `ItemStatus` chip (menu to change → PATCH)
- Expand caret

**Expanded panel** has three sections:

**(A) Provenance box** — read-only 3-column card grid:
- **RFQ:** `rfqName`, `rfqExType` (0=Warehouse / 1=Vendor / 2=Customer), `rfqQty @ rfqCondition`, `rfqPriority`, `rfqAlt`, `rfqNote` — link to `/rfqs/{sourceRfqId}` if id present
- **Quote:** `quoteNumber`, `quoteUnitPrice × quoteQty = total`, `quoteCondition`, `quoteAlt`, `quoteLeadTimeDays`d — link to `/quotes/{sourceQuoteId}` if id present
- **Invoice acceptance:** `acceptedQty @ acceptedUnitPrice`

**(B) Source assigned users** — read-only v-chip list from `procurement.sourceAssignedUsers` filtered to entries whose `entityId` matches this item's sourceRfqId/sourceQuoteId. Shows who was already working this on the RFQ/Quote side.

**(C) Supplier quotes grid** — THE key interactive piece. Copy the `proc-grid` table from `client/app/pages/rfqs/[id]/index.vue` (same visual language):

Columns: `Order | Supplier | Alt P/N | Condition | Qty | Cert | Tag Date | Shipping Point | Shipping Cost | Lead Time | Note | Buy Price | Total | [ ● select radio ]`

Each row is a `ProcurementSupplierQuoteResponse`. Add-row button at bottom: opens an inline form row or a dialog for `UpsertSupplierQuoteRequest`. Existing rows are inline-editable — save on blur or with a per-row "Save" button.
- `select radio`: POST `/procurements/{id}/items/{itemId}/supplier-quotes/{sqId}/select` — backend flips `isSelected`, updates item's `currentSupplierId / unitPrice / leadTime`. Refetch the detail.
- Delete button per row (non-selected rows): DELETE endpoint.
- When status is `Finalized`/`Cancelled`, all inputs become read-only.

**(D) Per-item assigned users** — admin-only BulkPermissionManager scoped card, or a compact inline permission editor. Use `entity-name="Procurement"` with `EntityId = itemId` (the generic permissions endpoint accepts any string EntityId, so item-scoped assignments just use the item's id).

---

## 6. BulkPermissionManager changes

Open `client/app/components/BulkPermissionManager.vue`. Find the `entityName` prop union (look for `entityName: 'PO' | 'Quote' | 'RFQ' | ...`). Add `'Procurement'`.

Find `entityLabel` computed or switch. Add a case mapping `'Procurement'` → `'Procurement'`.
Find `entityTitleKey` (or equivalent). Add the case mapping `'Procurement'` → `procurementNumber`.
Find `loadEntities()`. There's a `switch (entityName)` with a `'PO'` branch that calls `/purchase-orders?page=1&pageSize=500`. Clone it for `'Procurement'` calling `/procurements?page=1&pageSize=500`. Map each row's `id` → `label` as `"${procurementNumber} — ${invoiceNumber ?? ''} ${customerName ?? ''}"`.

---

## 7. Layout nav

Open `client/app/layouts/default.vue`. Find the `navItems` array. Add:

```ts
{ title: 'Procurements', to: '/procurements', icon: 'mdi-clipboard-edit-outline', adminOnly: false },
```

Place it between "Invoices" and "Purchase Orders" in the order — it sits logically between the two in the workflow.

---

## 8. Invoice detail "View Procurement" link

Open `client/app/pages/invoices/[id].vue`. In the header action row, add a button:

```vue
<v-btn
  v-if="procurementId && (invoice.status !== 'Draft' && invoice.status !== 'Pending')"
  :to="`/procurements/${procurementId}`"
  variant="tonal"
  color="primary"
  prepend-icon="mdi-clipboard-edit-outline"
>
  View Procurement
</v-btn>
```

To get `procurementId`, on invoice load do:
```ts
const res = await api.get<any>(`/procurements?search=${invoice.invoiceNumber}&pageSize=1`)
procurementId.value = res?.items?.[0]?.id ?? null
```

Only run this fetch when the invoice is past Draft/Pending.

---

## 9. Debug checklist — before declaring done

Run through this:

1. **Build the frontend:** `cd client && npm run build` — fix any TS errors.
2. **Dev server:** `cd client && npm run dev` — open `/procurements`, confirm no console errors.
3. **Create a procurement:** in the DB or via UI, pick an Invoice currently in Draft → move to `Accepted`. Confirm a row appears on `/procurements`.
4. **Edit isolation:** edit an item's qty + unitPrice. Open the source RFQ + Quote pages — confirm those rows are UNCHANGED. This is the whole point of the feature; verify it.
5. **Supplier quote flow:** add a new supplier quote → select it → confirm `currentSupplierId` / `unitPrice` updates on the parent item.
6. **Finalize:** click "Finalize → Create PO Items". Confirm:
   - Status flips to `Finalized`
   - `/purchase-orders` "unassigned items" section now shows the new POItems
   - All inputs on the Procurement detail are disabled
7. **Permissions:** as SuperAdmin, assign a non-admin user to one Procurement item. Log in as that user — they should see ONLY that one Procurement on the list, and only that one item visible/editable.
8. **Nav:** "Procurements" link appears in sidebar for all roles.
9. **Invoice link:** open an accepted invoice — "View Procurement" button is there.

---

## 10. Things I already handled (don't duplicate)

- Entities, DTOs, EF Core config, migration `AddProcurementLayer` — **migrated to db already**
- `ProcurementService` (in `Procument.Module.Sales.Services` — lives in Sales because Purchasing can't reference Sales)
- `ProcurementsController` at `/api/procurements`
- DI registration in `SalesModule.AddSalesModule`
- `InvoiceService.UpdateStatusAsync` now delegates to `IProcurementService.CreateFromAcceptedInvoiceAsync` instead of creating POItems directly — the old auto-POItem block is gone
- `POItem.SourceProcurementItemId` FK added
- Backend build: **green**, 0 errors

---

## 11. Gotchas

- **Don't** wire up UI that writes back to RFQ / Quote / ProcumentRecord from the Procurement pages. The whole point is isolation.
- The `supplierName` on a supplier quote is denormalized — if `supplierId` is null (ad-hoc supplier), still show the name. When the user types a name that matches an existing `Supplier.Name`, the backend auto-links the id.
- `itemStatus` states: `Open | Sourcing | Ready | Cancelled`. `Cancelled` items are skipped during Finalize.
- Finalize only creates unassigned POItems (POId = null). POs are grouped by the user later on `/purchase-orders` via the existing group-and-create flow. Do NOT write grouping logic on the Procurement page.
- Use `authStore.isAdmin` for admin-only UI. Backend re-enforces — don't rely on frontend checks.

---

Ping me if:
- A DTO field is missing for something you need displayed (I can extend the response)
- You hit a CORS or auth edge case
- The Invoice page needs `procurementId` surfaced directly on its response (I can add it — currently you'd fetch via search)

Ship when all 9 debug checklist items pass. Take the rest of this task and run.

— Sami
