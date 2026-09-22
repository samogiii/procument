# Feature Plan — OurInventory module (stock purchasing + our own stock)

**Status:** Implemented — all epics (0–11) are built and tested. Only 7.6 (soft quote holds, optional) is deferred, and the production rollout steps in §10 are yours to run.
**Date:** 2026-09-22
**Last updated:** 2026-09-22
**Module:** `src/Modules/Procument.Module.OurInventory`
**Frontend:** `client/app/pages/our-inventory/**`

### Progress snapshot

- **Completed:** 55 of 56 checklist items — everything except 7.6 (optional soft quote holds).
- **Current capability:** the module foundation and schema are in place; Stock POs can be created, edited, submitted and approved; supplier documents can be stored against the PO; the existing PR → POP → final amount → wallet/payment flow supports Stock POs; and received Stock PO quantities are booked into the Our Stock ledger (track acceptance or manual receipt), with moving-average cost, serials, reservations and PO auto-completion.
- **Frontend:** Our Stock list + detail, Stock PO list / create / edit, Receive Stock, and Stock-PO sections on the existing PO page (approval, supplier documents, receipts) are live behind the `ourInventoryMenu` permission.
- **PDFs:** Stock PO PDF (approved POs only, ships to the PO's warehouse), Goods Receipt Note (whole PO or a single delivery), stock report / valuation PDF and Excel export.
- **Stock management (Epic 6):** opening-stock Excel import (validate-all, dry run, template), adjust with reason, move between warehouses, lot details, movements log; physical warehouse transfers move the stock record when the destination accepts them; Stock PO goods no longer appear in Ready-for-SN.
- **Sales (Epic 7):** Our Stock and incoming Stock PO chips on the RFQ page; a quote row from a chip keeps the lot; accepting the Sales Order reserves it (shortfall → normal sourcing row), stock rows never become POs, *Issue* ships it at average cost; cancelling releases it; Total-PN shows the stock cost.
- **Dashboard (Epic 10):** Action Center blocks (Stock PO waiting for PR / receipt, low stock) and Our Stock cards.
- **Tests (Epic 11):** `src/Procument.OurInventory.Tests` — 14 database tests (see §8 Epic 11).
- **Verification:** the .NET solution build and the Nuxt production build pass. Epic 9 was clicked through in the browser against a throwaway database (seeded, then dropped): stock list + filters + value total, lot detail, Stock PO list + status filter, create via Excel paste → submit → reject to draft → edit, approve, manual receive with serial validation, supplier PI upload with amount warning. Epic 5 was run end-to-end against a throwaway database created from all migrations (22 ledger/receipt scenarios + Stock PO create/update, all passing), which was dropped afterwards.
- **Still to do on your side:** apply the two migrations to a copy of production, then the rollout checklist in §10.

---

## 1. What we are building

Today every Purchase Order starts from a **customer**: RFQ → Quote → Sales Order (Invoice) → Procurement → PO.

OurInventory adds a second way to buy: **we buy for our own stock.** We already know the part number and the quantity. There is no customer, RFQ or Sales Order behind the purchase.

```
                    ┌───────────────── Existing (customer-driven) ────────────────┐
RFQ → Quote → Sales Order → Procurement → PO ─┐
                                               ├─► PR → POP → Wallet debit → Final amount → Payment Done
Stock need (PN + Qty) → Stock PO  ────────────┘         (existing supplier-payment flow, unchanged)
                    └───────────────── New (OurInventory) ────────────────────────┘

Stock PO ─► Supplier sends PI + documents ─► PR ─► POP ─► Paid
         ─► Supplier ships ─► Track number ─► Received in Warehouse ─► ✅ Our Stock (+Qty)

Our Stock ─► Shown on the RFQ page as an availability chip (like Inventory / Cap List)
          ─► Used on a Quote ─► Reserved when the Sales Order is accepted ─► Issued when shipped (−Qty)
```

### Key design decision: reuse `PurchaseOrders`, don't clone it

A Stock PO is a normal `PurchaseOrder` row with `Origin = "Stock"` and `InvoiceId = NULL`. The lines are normal `POItems` with `InvoiceItemId = NULL`.

Why:

| Needed for Stock PO | Already works on `PurchaseOrder` today |
|---|---|
| Payment Request (PR) + PR PDF | `PaymentRequest.POId` → `PaymentRequestsController`, `PaymentRequestDocument` |
| POP upload, wallet debit, bank fee, final amount | `SupplierPaymentsController` (`/supplier-payments/po/{poId}/…`) — already falls back to `po.PONumber` when there is no invoice |
| Wallet & buying company | `PurchaseOrder.PreferredWalletId`, `PurchaseOrder.CompanyPresetId`, `PaymentBoxes`, `PaymentTransactions` |
| Admin approval / payment approval | `AdminApproval`, `PaymentApproval` columns + endpoints |
| PO PDF | `PurchaseOrderDocument` + `/pdf/po` + `PoPdfGenerator.vue` |
| Tracking, warehouse receive, shipment notes | `POItemTrackNumber`, `ShippingService`, `WarehouseTransfer`, `ShipmentNote` |
| Status propagation | `PurchaseOrderStatusFlow.ApplyAsync` — `SyncPiItemsAsync` already skips lines with no `InvoiceItemId` |

A separate `StockPurchaseOrders` table would mean re-building PR, POP, wallets, approvals, PDF and shipping for a second PO type. That's a lot of duplicate code, and wallet reports would split in two.

**What the new module owns:** the stock itself (quantity on hand, cost, reservations, and the movement ledger), the Stock PO create screen and service, the receiving hook, and the RFQ/Quote integration.

> ⚠️ The existing `InventoryItem` table is **supplier** stock lists (CompanyId → `Supplier`), just like Cap List. It is **not** our stock. We keep it as it is and add new `OurStock*` tables. In the UI we call the new feature **"Our Inventory"** or **"Our Stock"** so users don't mix up the two.

---

## 2. Module layout & connections

```
Procument.Shared
   ▲
Catalog ◄── Identity
   ▲          ▲
   RFQ ───────┤
   ▲          │
Purchasing ◄──┘            (PurchaseOrder, POItem, PaymentRequest, Warehouse, TrackNumber)
   ▲     ▲
Sales    OurInventory  ◄── NEW (references Shared, Catalog, Identity, RFQ, Purchasing)
   ▲     ▲
   Procument.Data (AppDbContext)  ──►  Procument.API (Program.cs, PdfController)
```

Rules (matching the current architecture):

- **OurInventory → Purchasing** is allowed (it creates `PurchaseOrder`/`POItem` rows and reads `Warehouse`/`TrackNumber`).
- **Purchasing must NOT reference OurInventory** (that would be circular). When Purchasing needs OurInventory (availability chips, receiving), it uses **interfaces defined in Purchasing** and implemented in OurInventory:
  - `IPartAvailabilitySource` → `AvailabilityService` injects `IEnumerable<IPartAvailabilitySource>`
  - `IStockReceiptHandler` → `ShippingService` calls it when a track number becomes `Received in Warehouse`
- **OurInventory must NOT reference Sales** (wallets and invoices live there). Wallet work goes through the existing `SupplierPaymentsController` by `poId`. When Sales needs stock (reserve on Sales Order accept, issue on ship), it uses an `IStockReservationService` interface. Put the interface in **Procument.Shared**, implement it in OurInventory, and inject it into Sales.
- `Procument.Data` gets a project reference to OurInventory, and `AppDbContext` gets the new `DbSet`s and config.

---

## 3. Database

### 3.1 Changes to existing tables (1 migration)

| Table | Column | Type | Notes |
|---|---|---|---|
| `PurchaseOrders` | `Origin` | `nvarchar(20) NOT NULL DEFAULT 'Customer'` | `Customer` \| `Stock`. Backfill = `Customer`. Index `(Origin, Status)`. |
| `PurchaseOrders` | `DestinationWarehouseId` | `bigint NULL` FK → `Warehouses` | Where the stock should arrive. Pre-fills the track-number warehouse. |
| `PurchaseOrders` | `SupplierPIRef` | `nvarchar(100) NULL` | Supplier's PI number (shown on PR and PDF). |
| `PurchaseOrders` | `ExpectedDeliveryDate` | `datetime2 NULL` | Optional, for "incoming stock" on the RFQ chip. |
| `POItems` | `StockItemId` | `bigint NULL` FK → `OurStockItems` | Set when the line is received into stock (traceability). |

> Check before the migration: every query that assumes `PurchaseOrder.InvoiceId != null` or `POItem.InvoiceItemId != null` (Total-PN join, Procurement "unassigned items", Action Center blocks, payment queue customer columns). Stock POs are created **already attached** (`POId` is set), so they never appear in the Procurement "unassigned items" list. Total-PN starts from `InvoiceItem`, so Stock POs are excluded there automatically.

### 3.2 New tables (OurInventory module)

#### `OurStockItems` — one row per stock "lot" (part + condition + warehouse + owner company)

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint PK | |
| `PartNumberId` | bigint FK → `PartNumbers` | required |
| `Condition` | nvarchar(20) | NE / NS / OH / SV / AR … (same values as quotes) |
| `WarehouseId` | bigint FK → `Warehouses` | required |
| `CompanyPresetId` | bigint FK → `CompanyPresets` | which of our companies owns the stock (from the PO) |
| `QtyOnHand` | decimal(18,2) | kept up to date by the ledger only |
| `QtyReserved` | decimal(18,2) | sum of active reservations |
| `QtyAvailable` | computed (`QtyOnHand - QtyReserved`), persisted | used by RFQ chips |
| `AvgUnitCost` | decimal(18,4) | moving-average cost in USD |
| `CertName` | nvarchar(100) NULL | 8130 / EASA Form 1 / CoC … |
| `TagDate` | date NULL | |
| `BinLocation` | nvarchar(50) NULL | shelf/bin |
| `MinQty` | decimal(18,2) NULL | re-order point (for the "Low stock" view) |
| `Notes` | nvarchar(max) NULL | |
| `CreatedAt`, `UpdatedAt` | datetime2 | |
| `RowVersion` | rowversion | optimistic concurrency, so two issues can't take the same qty |

Unique index: `(PartNumberId, Condition, WarehouseId, CompanyPresetId, CertName)`. Other indexes: `PartNumberId`, `WarehouseId`.

#### `OurStockMovements` — the ledger (append-only; the source of truth)

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint PK | |
| `StockItemId` | bigint FK | |
| `Type` | nvarchar(20) | `Receipt` \| `Issue` \| `Adjust` \| `TransferOut` \| `TransferIn` \| `ReturnToSupplier` \| `CustomerReturn` \| `Opening` |
| `Qty` | decimal(18,2) | **signed** (+in / −out) |
| `UnitCost` | decimal(18,4) NULL | cost at the time of the movement |
| `POId` / `POItemId` | bigint NULL | for receipts from a Stock PO |
| `TrackNumberId` | bigint NULL | the receiving track number |
| `InvoiceItemId` | bigint NULL | for issues to a Sales Order line (no FK — Sales table) |
| `QuoteItemId` / `RFQItemId` | bigint NULL | traceability |
| `WarehouseTransferId` | bigint NULL | |
| `Reference` | nvarchar(100) | human-readable (PO-123, INV-45, ADJ-7) |
| `Reason` | nvarchar(200) NULL | required for `Adjust` |
| `CreatedBy` | bigint | user id |
| `CreatedAt` | datetime2 | |

Index: `(StockItemId, CreatedAt)`, `POItemId`, `InvoiceItemId`.
**Rule:** `QtyOnHand` is only changed together with a movement, in the same transaction. Nightly check (optional): `SUM(Qty)` per item = `QtyOnHand`.

#### `OurStockReservations` — qty held for a customer

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint PK | |
| `StockItemId` | bigint FK | |
| `Qty` | decimal(18,2) | |
| `Status` | nvarchar(20) | `Active` \| `Consumed` \| `Released` \| `Expired` |
| `RFQItemId`, `QuoteItemId`, `InvoiceItemId` | bigint NULL | what holds the stock |
| `ExpiresAt` | datetime2 NULL | soft hold for quotes (e.g. quote validity) |
| `CreatedBy`, `CreatedAt`, `ClosedAt` | | |

#### `OurStockSerials` (optional, for serialized parts)

`Id, StockItemId, SerialNumber, Status (InStock|Reserved|Issued), ReceiptMovementId, IssueMovementId`. Unique `(PartNumberId, SerialNumber)` through the item.

#### `PurchaseOrderDocuments` (in **Purchasing**, since both PO origins can use it)

Today supplier PI files are stored by **customer invoice number** (`DocumentsController …/proforma-invoice/{invoiceId}/supplier/{supplierId}/upload`), and a Stock PO has no invoice. So we need documents stored by PO:

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint PK | |
| `POId` | bigint FK → `PurchaseOrders` | |
| `Category` | nvarchar(30) | `SupplierPI` \| `SupplierInvoice` \| `Certificate` \| `PackingList` \| `Other` |
| `FileName` / `StoredName` | nvarchar(260) | stored via `DocumentStorageService.SaveFileInSupplierCategory(po.PONumber, supplier.Name, category, …)` |
| `DocumentNumber` | nvarchar(100) NULL | e.g. the PI number |
| `Amount` | decimal(18,2) NULL | PI amount (compared with the PO total) |
| `UploadedBy`, `CreatedAt` | | |

### 3.3 Migration

```bash
dotnet ef migrations add AddOurInventoryModule --project src/Procument.Data --startup-project src/Procument.API
dotnet ef database update --project src/Procument.Data --startup-project src/Procument.API
```

The seed runs inside the migration or at startup, and is idempotent:
- Catalog `Supplier` **"OUR STOCK"** (one per company preset if needed). Quotes must point at a catalog supplier (see the *Supplier Catalog-Only* rule), so applying a stock chip on an RFQ creates a quote row with this supplier.
- `MenuPermission` row for `ourInventoryMenu`.

---

## 4. Backend

### 4.1 Project

```
src/Modules/Procument.Module.OurInventory/
  Procument.Module.OurInventory.csproj     (refs: Shared, Catalog, Identity, RFQ, Purchasing)
  OurInventoryModule.cs                    AddOurInventoryModule()
  Entities/  OurStockItem.cs, OurStockMovement.cs, OurStockReservation.cs, OurStockSerial.cs
  DTOs/      StockPoDTOs.cs, StockItemDTOs.cs, StockMovementDTOs.cs, StockReservationDTOs.cs
  Services/
    StockPurchaseOrderService.cs     create/edit Stock PO (writes PurchaseOrder + POItems)
    StockLedgerService.cs            the ONLY place that changes QtyOnHand/QtyReserved
    StockReceiptHandler.cs           : IStockReceiptHandler   (called by ShippingService)
    StockAvailabilitySource.cs       : IPartAvailabilitySource (called by AvailabilityService)
    StockReservationService.cs       : IStockReservationService (called by Sales)
    StockReportService.cs            valuation, low stock, movement history
  Controllers/
    StockPurchaseOrdersController.cs  api/our-inventory/purchase-orders
    OurStockController.cs             api/our-inventory/stock
    StockMovementsController.cs       api/our-inventory/movements
    StockReservationsController.cs    api/our-inventory/reservations
```

Registration:
- `Program.cs`: `builder.Services.AddOurInventoryModule();` after `AddPurchasingModule()`, plus the controller assembly part (same way the other modules are added).
- `Procument.Data.csproj`: add a `ProjectReference`. `AppDbContext`: add `DbSet`s and `OnModelCreating` config (precision, indexes, rowversion, FKs with `DeleteBehavior.Restrict`).
- `Procument.Module.Sales.csproj`: **no** reference. It only uses the Shared interface.

### 4.2 Endpoints

**Stock Purchase Orders** — `api/our-inventory/purchase-orders`

| Method | Route | What it does |
|---|---|---|
| GET | `/` | Paged list of `Origin = Stock` POs (`PagedResult<T>`, server-side filters, `TotalAmountSum`) |
| GET | `/filter-options` | Exclude-self cascading filter options (same rule as the other 6 list pages) |
| POST | `/` | Create: `{ supplierId, companyPresetId, destinationWarehouseId, subject, supplierPIRef?, items:[{partNumberId \| partNumber, qty, unitPrice, condition}] }` → creates `PurchaseOrder{Origin=Stock, Status=Draft}` + POItems (`PORef` 1..n), resolves `PreferredWalletId` from the preset (same logic as `PurchaseOrderService.CreateAsync`), `PONumber = "SPO-{Id}"` |
| PUT | `/{id}` | Edit header/lines while `Draft` |
| POST | `/{id}/submit` | Draft → `Waiting For Admin Approval` |
| GET | `/suggest` | Re-order suggestions: items where `QtyAvailable < MinQty` |
| POST | `/{id}/receive` | Manual receive (no track number) — Admin only |

Everything after creation uses the **existing** endpoints with the PO id:
- `PATCH /purchase-orders/{id}/admin-approval`, `/status`, `/totals`, `/subject`, `/import-detail`
- `POST /paymentrequests/po/{poId}`, `PATCH /paymentrequests/{id}/amount|status`
- `POST /supplier-payments/po/{poId}/draft` → `/pop` → `/final-amount` (wallet debit, bank fee, Payment Done)
- `POST /purchase-orders/items/{poItemId}/track-numbers` (use `DestinationWarehouseId` as the default)
- `POST /pdf/po`, `POST /pdf/payment-request`

**PO documents** (Purchasing, both origins) — `api/purchase-orders/{poId}/documents`: `GET`, `POST` (multipart, `category`, `documentNumber`, `amount`), `GET /{docId}/file`, `DELETE /{docId}`.

**Stock** — `api/our-inventory/stock`

| Method | Route | What it does |
|---|---|---|
| GET | `/` | Paged stock list (PN, desc, condition, warehouse, company, on-hand, reserved, available, avg cost, value) |
| GET | `/filter-options` | Cascading filters |
| GET | `/{id}` | Detail + last movements + active reservations + incoming (open Stock PO lines for the same PN) |
| GET | `/by-part/{partNumberId}` | All lots of one part (used by RFQ/Quote dialogs) |
| POST | `/opening` | Opening balance / bulk Excel import (movement `Opening`) |
| POST | `/{id}/adjust` | ± qty with a required reason (movement `Adjust`) — Admin only |
| POST | `/{id}/transfer` | To another warehouse (`TransferOut` + `TransferIn`, or through the existing `WarehouseTransfer` when it's physically shipped) |
| PATCH | `/{id}` | Bin, MinQty, cert, notes |
| GET | `/valuation` | Stock value by company / warehouse |

**Movements** — `GET api/our-inventory/movements` (paged, filter by PN / type / date / reference).

**Reservations** — `POST api/our-inventory/reservations`, `POST /{id}/release`, `POST /{id}/consume` (issue).

### 4.3 Business rules

1. **Create:** a Stock PO needs a supplier from the catalog, a company preset and a destination warehouse. Each line needs PN, Qty > 0 and UnitPrice ≥ 0. If a PN is unknown, reuse the catalog "get-or-create PartNumber" helper.
2. **Status flow:** same `PurchaseOrderStatusFlow` as customer POs:
   `Draft → Waiting For Admin Approval → Waiting For Supplier Documents → Waiting For PR → Waiting For Payment → Payment Done → Ship to Warehouse → Received in Warehouse → Completed`.
   `EndUser` / `In Shop` fulfilment modes are hidden for Stock POs.
3. **Supplier documents:** if the uploaded PI amount ≠ PO total, show a warning before the PR is created (not a hard block).
4. **PR / wallet:** unchanged. The debit is recorded as a `PaymentTransaction` with `ToType = Supplier`, `PaymentRequestId` set and `InvoiceId = NULL`. The wallet page labels these as "Stock PO".
5. **Receiving:** in `ShippingService`, where a track becomes `Received in Warehouse` (around lines 191 and 447), call `IStockReceiptHandler.OnReceivedAsync(trackId)` **only when `po.Origin == "Stock"`**. The handler:
   - finds or creates an `OurStockItem` by (PN, condition, warehouse of the track, PO company preset, cert)
   - writes a `Receipt` movement with the **received** qty (`TrackNumberItem` qty, not the PO qty, so partial shipments work)
   - updates `AvgUnitCost = (oldQty*oldCost + rcvQty*landedUnitCost) / (oldQty + rcvQty)`
   - sets `POItem.StockItemId`
   - is **idempotent**: skip if a Receipt movement for this `TrackNumberId` + `POItemId` already exists
   - runs in the same DB transaction as the status change
6. **Landed cost** (phase 2, optional): `landedUnitCost = UnitPrice + (Shipping + Tax + ProcessingFee + BankFee) × lineValue / POValue / qty`.
7. **Reservations:** created when a Sales Order line uses stock (see 4.4). `QtyAvailable` can't go below 0 (checked with `RowVersion`, retry once).
8. **Issue:** when the Sales Order line ships from our warehouse (Shipment Note / Packing list), consume the reservation, write an `Issue` movement with `InvoiceItemId` and the cost. This cost is the COGS for the Total-PN margin.
9. **Cancel/Return:** cancelling a Stock PO before receipt has no stock effect. After receipt, return to supplier = `ReturnToSupplier` movement.
10. **Scope:** stock is visible to everybody who can see RFQs (availability chips only show PN / condition / qty / warehouse). Cost and value only show to Admin/SuperAdmin.

### 4.4 Integration with RFQ / Quote / Sales

| Where | Change |
|---|---|
| `AvailabilityService.GetPartAvailabilityAsync` | Inject `IEnumerable<IPartAvailabilitySource>` and add `OurStockRecords` to `PartAvailabilityResponse`: `{ label: "Our Stock · {warehouse}", qty: QtyAvailable, condition, certName, tagDate, price: AvgUnitCost (admin only), stockItemId }`. Also add **incoming** records (open Stock PO lines, not yet received) with `leadTime = ExpectedDeliveryDate`. |
| RFQ page chip | New chip colour (e.g. purple) before Inventory. Clicking → `applyAvailability(item, rec)` creates a quote row with supplier = **OUR STOCK**, qty = min(requested, available), price = avg cost, condition/cert from the lot, and stores `stockItemId` on the quote row. |
| `SupplierQuote` / `QuoteItem` | Add a nullable `SourceStockItemId` (in the same migration) so the link survives into the Sales Order. |
| Quote accepted → Sales Order | `InvoiceService`: for lines with `SourceStockItemId`, call `IStockReservationService.ReserveAsync(stockItemId, qty, invoiceItemId)`. If not enough stock, the line goes to Procurement as normal (partial reserve + remainder). |
| Procurement | Lines fully covered by a reservation get `ItemStatus = Ready` and **no POItem**. Show a "From Stock" badge. |
| Shipping to customer | Consume the reservation → `Issue` movement. |
| Total-PN | Stock-sourced lines show `Supplier = OUR STOCK` and cost = issue cost (instead of a PO price). |
| Part History dialog | Add an "Our Stock" tab (movements for the PN). |
| Dashboard / Action Center | New blocks: "Stock POs waiting for PR", "Stock POs waiting for payment", "Low stock (below MinQty)". |

---

## 5. PDF generation (QuestPDF, `src/Procument.API/Pdf/`)

| Document | Approach |
|---|---|
| **Stock PO** | Reuse `PurchaseOrderDocument`. `/purchase-orders/{id}/pdf-data` already builds it from the PO; for `Origin=Stock` hide the customer/invoice reference and show "Ship To: {DestinationWarehouse.ShipToAddress}". Branding comes from `CompanyPresetId`, as it does today. |
| **Payment Request** | Reuse `PaymentRequestDocument` unchanged; the reference line shows `SupplierPIRef` instead of the customer invoice. |
| **Goods Receipt Note (GRN)** — new | `StockReceiptDocument.cs`: PO#, supplier, warehouse, received date, lines (PN, desc, cond, qty ordered, qty received, cert, bin), signature boxes. Endpoint `POST /pdf/stock-receipt`. |
| **Stock Report** — new | `StockReportDocument.cs`: stock list / valuation by company and warehouse, filters as on screen. Endpoint `POST /pdf/stock-report`. Excel export as well (same payload). |
| Frontend | `StockPoPdfGenerator.vue` → wraps `PoPdfGenerator.vue` behaviour; `StockReceiptPdfGenerator.vue`; side-by-side form + preview layout like `FinalInvoicePdfGenerator.vue`. |

Register the new builders in `Pdf/Templates/PdfDocModelBuilders.cs` and the DTOs in `PdfRequestDTOs.cs`.

---

## 6. Frontend (Nuxt 3 + Vuetify)

### 6.1 Pages

| Route | File | Content |
|---|---|---|
| `/our-inventory` | `pages/our-inventory/index.vue` | Stock list with `DataListPage` (`serverSide`, `showTotalSum` = stock value, admin only), cascading `ColFilterMenu`, qty chips (available / reserved), actions: Adjust, Transfer, History. Toolbar: "New Stock PO", "Import opening stock", "Low stock", "PDF / Excel". |
| `/our-inventory/[id]` | `pages/our-inventory/[id].vue` | Lot detail: header card, movements table, reservations, incoming PO lines, serials. |
| `/our-inventory/purchase-orders` | `pages/our-inventory/purchase-orders/index.vue` | Stock PO list (status, supplier, company, warehouse, total, paid, PR status). Same columns/filters pattern as `purchase-orders/index.vue`. |
| `/our-inventory/purchase-orders/new` | `pages/our-inventory/purchase-orders/new.vue` | Create form: Supplier (catalog autocomplete), Company Preset picker (same component/logic as PO creation, with the "no wallet" warning), Destination Warehouse, Subject, Supplier PI ref, lines grid (PN autocomplete + paste from Excel, Qty, Unit Price, Condition, line total), totals (shipping/tax/fee). "Save draft" / "Submit for approval". A "Fill from low stock" button calls `/suggest`. |
| `/purchase-orders/[id]` | **reuse existing** `pages/purchase-orders/[id].vue` | Add `v-if="po.origin === 'Stock'"` guards: hide customer/invoice/fulfilment-mode blocks, show a "Stock PO" badge, destination warehouse, **Documents panel** (new `PurchaseOrderDocuments.vue`), "Received into stock" summary. The PR, POP, final amount, tracking and PDF sections work as they are. |
| `/our-inventory/movements` | `pages/our-inventory/movements.vue` | Ledger view (read-only), filters by PN / type / date / reference. |
| `/our-inventory/receive` | `pages/our-inventory/receive.vue` | For Inventory role users: incoming Stock PO lines for their warehouses → set qty received, bin, serials → prints the GRN. (It can also be a tab on `/shipping` if Inventory users prefer one place.) |

### 6.2 Components

- `StockPoLinesEditor.vue` — line grid with Excel paste (reuse the RFQ bulk-import parser).
- `PurchaseOrderDocuments.vue` — upload/list/preview supplier PI, invoice and certs (reuse `FileDropZone.vue` and `DocPreviewModal.vue`).
- `StockAdjustDialog.vue`, `StockTransferDialog.vue`, `StockReceiveDialog.vue`.
- `StockMovementsTable.vue` (used on the detail page and in Part History).
- `StockPoPdfGenerator.vue`, `StockReceiptPdfGenerator.vue`.

### 6.3 Changes to existing frontend

| File | Change |
|---|---|
| `pages/rfqs/[id]/index.vue` | Add the `ourStockRecords` chips (`avail-chip--ourstock`) and incoming-stock chips to the availability block (around line 710). `applyAvailability` passes `stockItemId`. The data comes from the existing `/availability/parts` call, so no extra request. |
| `pages/quotes/[id]/index.vue`, `rfqs/[id]/create-quote` | Show an "Our Stock" badge on rows with `sourceStockItemId`. |
| `pages/procurements/[id].vue` | "From Stock (reserved n)" badge; hide the supplier-sourcing UI for fully reserved lines. |
| `pages/purchase-orders/index.vue` | Default filter `origin=Customer` + a toggle to show Stock POs, or exclude them. |
| `pages/payment/index.vue` (wallet / payment queue) | Label rows "Stock PO" when `invoiceId == null && origin == 'Stock'`. |
| `components/PartHistoryDialog.vue` | "Our Stock" tab. |
| `layouts/default.vue` | Nav group **Our Inventory** (Stock, Stock POs, Movements, Receive) with the `ourInventoryMenu` flag; add `/our-inventory/receive` to the Inventory role nav. |
| `stores/auth.ts` | `ourInventoryMenu: []` in `DEFAULT_FEATURE_PERMISSIONS` + getter. |
| `pages/menu-access.vue` | `FEATURE_LABELS` / `FEATURE_ICONS` entry (`mdi-warehouse`). |
| `middleware/auth.global.ts` | Add `/our-inventory/receive` to `INVENTORY_ALLOWED_PREFIXES`. |

**Performance rules** (same as the other `DataListPage` screens): server-side paging, no big arrays passed into slots, scalars only in `#before-table`, and no template refs for data needed on first render.

---

## 7. Permissions & configuration

| Item | Value |
|---|---|
| Feature flag | `ourInventoryMenu` (MenuPermission). SuperAdmin always has it. |
| Create / edit Stock PO | Admin, SuperAdmin, and users with `ourInventoryMenu` |
| Admin approval | Admin, SuperAdmin (existing endpoint) |
| PR / POP / final amount | Payment role (existing) |
| Receive into stock | Inventory role (their warehouses only, via `UserWarehouse`), Admin |
| Adjust / opening stock | Admin, SuperAdmin only |
| See cost & value | Admin, SuperAdmin only (strip `AvgUnitCost`/`value` from DTOs for other roles) |
| `appsettings.json` → `OurInventory` section | `StockPoNumberPrefix: "SPO-"`, `OurStockSupplierName: "OUR STOCK"`, `QuoteReservationDays: 14`, `AllowNegativeStock: false`, `UseLandedCost: false` |
| Satellite sync | If satellite sync is on, add the 4 new tables to the sync table list (`SATELLITE_SYNC_ARCHITECTURE.md`). |
| Audit | Add the new entities to `BusinessAuditViewer` so changes show up in System Activity. |

---

## 8. Epics & tasks

Rough sizes: **S** ≤ 0.5 d, **M** ≈ 1 d, **L** ≈ 2–3 d.

### Epic 0 — Foundations
- [x] **0.1 (S)** Create the `Procument.Module.OurInventory` project, csproj refs, `OurInventoryModule.AddOurInventoryModule()`, register it in `Program.cs` and the solution.
- [x] **0.2 (S)** Add the `OurInventory` config section + `OurInventoryOptions` class.
- [x] **0.3 (S)** Seed the catalog supplier **OUR STOCK** and register the `ourInventoryMenu` feature key (idempotent supplier bootstrap; permissions remain per-user grants).
- [x] **0.4 (S)** Define `IPartAvailabilitySource`, `IStockReceiptHandler` (in Purchasing) and `IStockReservationService` (in Shared).

**Done when:** the API starts with the empty module, and the new interfaces resolve through DI.

### Epic 1 — Database
- [x] **1.1 (M)** Entities `OurStockItem`, `OurStockMovement`, `OurStockReservation`, `OurStockSerial`.
- [x] **1.2 (S)** `PurchaseOrderDocument` entity (Purchasing).
- [x] **1.3 (S)** New columns: `PurchaseOrder.Origin/DestinationWarehouseId/SupplierPIRef/ExpectedDeliveryDate`, `POItem.StockItemId`, `QuoteItem.SourceStockItemId` (+ `SupplierQuote` if the quote row lives there).
- [x] **1.4 (M)** `AppDbContext` config: precision, the unique lot index, rowversion, FKs (`Restrict`), the computed `QtyAvailable`.
- [x] **1.5 (S)** Migration `AddOurInventoryModule`, with a backfill of `Origin='Customer'`. Review the generated SQL.

**Done when:** the migration applies on a copy of prod data with no data changes to existing rows.

### Epic 2 — Stock Purchase Orders (backend)
- [x] **2.1 (L)** `StockPurchaseOrderService.CreateAsync/UpdateAsync/SubmitAsync` (preset → wallet resolution copied from `PurchaseOrderService.CreateAsync`, `SPO-{Id}` numbering, `PORef`).
- [x] **2.2 (M)** `StockPurchaseOrdersController` list + filter-options (exclude-self), paged, with `TotalAmountSum`.
- [x] **2.3 (S)** `/suggest` (low-stock → draft lines).
- [x] **2.4 (M)** Make the existing PO endpoints origin-aware: `/{id}` and `/enriched` return `origin` and the destination warehouse; the list endpoint accepts an `origin` filter; `/pdf-data` handles `InvoiceId == null`.
- [x] **2.5 (M)** Check every `InvoiceId`/`InvoiceItemId` assumption (payment queue, Action Center, Total-PN, procurement unassigned, TrackNumber summary) and add null-safe handling or explicit `Origin` filters.

**Done when:** a Stock PO can be created, edited, submitted and approved, and it shows on the existing PO detail page without errors.

### Epic 3 — Supplier documents (PI etc.)
- [x] **3.1 (M)** `PurchaseOrderDocumentsController` (upload/list/download/delete) using `DocumentStorageService.SaveFileInSupplierCategory(po.PONumber, …)`.
- [x] **3.2 (S)** Moving to "Waiting For PR" requires at least one `SupplierPI` document for Stock POs. Warn when PI amount ≠ PO total.

### Epic 4 — PR, payment & wallets
- [x] **4.1 (S)** Verify the PR create → amount → POP → final amount path for a Stock PO. The shared code path creates a separate bank-fee transaction and applies `Payment Done`; live database E2E remains covered by 11.2.
- [x] **4.2 (S)** `PaymentRequestDocument`: show `SupplierPIRef` when there is no customer invoice.
- [x] **4.3 (S)** Wallet transaction list and payment queue: label "Stock PO" + a link to the PO.
- [x] **4.4 (S)** Partial payments (`PARTIALLY PAID`) work for Stock POs.

**Done when:** paying a Stock PO debits the chosen wallet and records the POP and bank fee, exactly as a customer PO does.

### Epic 5 — Receiving into stock
- [x] **5.1 (L)** `StockLedgerService` (receipt / issue / adjust / transfer / reserve / release / consume; single transaction; moving-average cost). Lots are locked with `UPDLOCK` instead of a rowversion retry loop; the rowversion column stays as a backstop.
- [x] **5.2 (M)** `StockReceiptService` implements `IStockReceiptHandler`; `ShippingService` calls it inside the same transaction as submit / update / review / receive-all (Stock origin only, idempotent per track + line, partial qty, recount and rejection corrections).
- [x] **5.3 (M)** Manual receive endpoint for items that arrive without a track number.
- [x] **5.4 (S)** Serial capture on receive (manual receipt, plus an endpoint to attach serials to a track receipt).
- [x] **5.5 (S)** PO auto → `Completed` when all lines are fully received.

**Done when:** receiving a track for a Stock PO creates or updates exactly one stock lot and one Receipt movement, and doing it twice does nothing. ✅ Verified.

#### Epic 5 — as built

**Trigger.** Stock is booked when a track item is **Accepted** (review or Receive All), not when Inventory first submits a count. A submitted count is still pending expert review, so booking it early would put unverified quantities into stock. The received quantity is the accepted `ActualQty` (falling back to `ExpectedQty`, as Receive All does).

**Reconciliation, not append.** For each (track, Stock PO line) the handler compares the accepted quantity with what the ledger already holds for that pair:
- the first time → one `Receipt` movement at the line's `UnitPrice`, into the lot (PN, condition, track warehouse or PO destination, PO company preset);
- a later recount or a rejection → the difference is posted as an `Adjust` on the same lot, with a "Receipt correction on track …" reason;
- no difference → nothing. Calling it twice never double-counts (also enforced by the filtered unique index on `(TrackNumberId, POItemId)` for Receipts).

A correction that would remove stock already reserved or issued is refused, and the review/receive request returns **400** with the reason instead of accepting the track.

**Transactions.** `ShippingService.SubmitItems / UpdateItem / ReviewItem / ReceiveAndAcceptAll` now run their change and the stock booking in one execution-strategy transaction. `IStockReceiptHandler.OnReceivedAsync` gained a `userId` parameter (movements need a user), and `IShippingService.UpdateItemAsync` now takes the user id too.

**Completion.** For Stock POs, `ShippingService.CompletePoWhenEveryPartApprovedAsync` no longer decides; the receipt service does, on quantity: a line is `Completed` when received ≥ ordered, otherwise `Received in Warehouse`; the PO is `Completed` when every line is. A PO an admin already set to `Completed` (short delivery) is not reopened.

**Endpoints added**
| Method | Route | Roles |
|---|---|---|
| GET | `/api/our-inventory/purchase-orders/{id}/receipts` | all module roles — ordered / received / remaining per line + receipt movements + serials |
| POST | `/api/our-inventory/purchase-orders/{id}/receive` | Admin, SuperAdmin — `{ warehouseId?, note?, lines: [{ poItemId, qty, certName?, tagDate?, binLocation?, serials? }] }`; PO must be admin-approved; over-receipt is refused; serial count must equal qty |
| POST | `/api/our-inventory/receipts/{movementId}/serials` | Admin, SuperAdmin — `{ serials: [] }` |

`IStockReservationService` (Shared) is now implemented on top of the ledger: `ReserveAsync` is idempotent per Sales Order line and reserves partially when short; `ConsumeAsync` issues oldest reservations first; `ReleaseAsync` frees all of a line's active holds. Epic 7 only has to call it.

**Also fixed.** `StockPurchaseOrderService.Create/Update` opened a transaction outside the execution strategy, which `EnableRetryOnFailure` rejects at runtime. Both now run inside `CreateExecutionStrategy()` like the rest of the codebase.

**Known gaps (tracked for later epics)**
- **Warehouse transfers / Shipment Notes (→ 6.4):** accepted Stock PO track items still appear in the Ready-for-SN queue and can be moved by a `WarehouseTransfer`, but the ledger doesn't follow them yet. Transfer legs (`Origin = "Transfer"`) are deliberately skipped by the handler so they are not counted twice.
- **Landed cost (→ phase 2):** `UseLandedCost` is not applied yet; receipts use the PO line `UnitPrice`.
- **Inventory-role receiving (→ 9.7):** the manual receive and serial endpoints are Admin-only until warehouse scoping (`UserWarehouse`) is added with the receive page.
- **Automated tests (→ 11.1):** the Epic 5 scenarios were run with a throwaway harness; they still need to become a permanent test project.

### Epic 6 — Stock management
- [x] **6.1 (M)** `OurStockController`: list, filter-options, detail, by-part, patch.
- [x] **6.2 (M)** Opening balance + Excel bulk import (reuse the Inventory bulk-import pattern).
- [x] **6.3 (S)** Adjust (reason required, admin only).
- [x] **6.4 (M)** Transfer between warehouses (hook into `WarehouseTransferService` when shipped physically).
- [x] **6.5 (S)** Movements endpoint + valuation endpoint.
- [x] **6.6 (S)** Cost fields stripped for non-admin roles.

#### Epic 6 — as built
| Method | Route (all under `/api/our-inventory`) | Roles |
|---|---|---|
| GET | `stock/by-part/{partNumberId}` | all module roles |
| PATCH | `stock/{id}` — bin, min qty, tag date, notes (certificate is part of the lot key, so not editable) | Admin, SuperAdmin |
| POST | `stock/{id}/adjust` `{ qty (signed), reason }` — cannot remove reserved stock | Admin, SuperAdmin |
| POST | `stock/{id}/transfer` `{ targetWarehouseId, qty, reason? }` — moves the record now, keeps cost | Admin, SuperAdmin |
| POST | `stock/opening` `{ reference, dryRun, lines[] }` — every row validated first; 400 with `errors[{row, message}]` and nothing saved if any fails; unknown P/Ns added to the catalog; warehouse / company matched by name (case-insensitive) or id | Admin, SuperAdmin |

- **Physical transfers:** a `WarehouseTransfer` destination leg that carries Stock PO goods moves the accepted quantity from the lot at the transfer's source warehouse to the same lot key at the destination, in the same transaction as the acceptance (`StockReceiptService.ReconcileTransferLegAsync`). Like receipts it reconciles, so a recount or rejection moves the difference back and re-accepting does nothing. Stock stays at the source — and sellable — while in transit.
- **Ready-for-SN:** Stock PO goods are excluded; they leave a warehouse through a sale (*Issue*) or a transfer.
- **Screens:** *Import opening stock* on Our Stock (template download, .xlsx / .csv, *Check* then *Import*, row errors shown in place); *Adjust / Move / Edit* on the lot page; *Stock Movements* page (type / date / text filters) in the Our Inventory menu.
- **Ledger fix:** `ExecuteAsync` now clears the change tracker only on a retry, not on the first attempt — Procurement and Invoice services share the DbContext and still hold tracked rows when they call the reservation service.

### Epic 7 — RFQ / Quote / Sales integration
- [x] **7.1 (M)** `StockAvailabilitySource` (available lots + incoming PO lines) → `AvailabilityService` → `OurStockRecords` in `PartAvailabilityResponse`.
- [x] **7.2 (M)** Quote row keeps `SourceStockItemId` from `applyAvailability`; supplier = OUR STOCK.
- [x] **7.3 (L)** Sales Order accept → `ReserveAsync` (partial reserve + remainder to Procurement); Sales Order cancel → `ReleaseAsync`.
- [x] **7.4 (M)** Procurement: reserved lines = Ready / From Stock, no POItem.
- [x] **7.5 (M)** Ship to customer → `ConsumeAsync` → `Issue` movement with cost; Total-PN shows stock cost.
- [ ] **7.6 (S)** Soft quote holds with `ExpiresAt` + cleanup job (optional). *Deferred — the reservation model supports `ExpiresAt`, nothing uses it yet.*

**Done when:** a PN in stock shows a purple chip on an RFQ, and following it through Quote → Sales Order → Ship lowers the stock by the shipped qty. ✅ Verified (tests + browser).

#### Epic 7 — as built
**Migration `AddOurInventorySalesLinks`** (additive): `SupplierPartQuote.SourceStockItemId` (nullable FK → `OurStockItems`, indexed) and `ProcurementItems.FromStock` (bit, default 0).

**Chain**
1. **RFQ page:** purple chips *Our Stock · {warehouse} · {available}* and dashed *Incoming · {SPO} · {qty}* (from `/availability/parts`: `ourStockRecords`, `incomingStockRecords`). Clicking one adds a supplier-quote row with supplier **OUR STOCK**, the lot id (`sourceStockItemId`), qty = min(requested, available), price = average cost, condition / certificate from the lot. The bulk save stores the lot on the row.
2. **Quote:** `QuoteService` copies the row's lot onto `QuoteItem.SourceStockItemId`. The quote page badges these rows *Our Stock*.
3. **Sales Order accepted → Procurement created:** for a stock line (a lot on the quote, or supplier = OUR STOCK) the lot is resolved (the quoted lot, else the same part / condition with most available) and reserved against the Sales Order line.
   - fully covered → row `FromStock = true`, *Ready*, note "Reserved from Our Stock"; PI line status *Reserved from Stock*;
   - partly covered → the row keeps the reserved qty and the rest is split into the usual red *No Supplier* remainder;
   - nothing available → the OUR STOCK choice is removed and the row is sourced like any other line (note explains why).
4. **Never bought:** every materialise / finalise path skips `FromStock` rows and treats them as satisfied, so no POItem is ever created for OUR STOCK.
5. **Ship:** on the lot page an admin clicks **Issue** on the reservation → `Issue` movement at the lot's average cost (the line's cost of goods); when the line holds nothing more, its PI status becomes *Delivered to Customer*. **Release** gives it back to available.
6. **Cancel:** cancelling the Sales Order, removing its line, or cancelling the Procurement releases the reservation.
7. **Total-PN:** stock rows show supplier OUR STOCK, the issue cost when issued, and *Reserved n / Issued n from Our Stock* as shipping status. **Part History** has an *Our Stock* tab with the part's movements.

**Behaviour notes**
- The RFQ chip shows the average cost to anyone who can see RFQs, because it is the starting price of the quote line (decision 1 below). The Our Stock pages still hide cost from non-admins.
- Reservations are keyed by the Sales Order line; `IStockReservationService` (Shared) now also offers `ResolveStockLotAsync`, `GetLineStatusAsync` and `GetStockSupplierIdAsync`.
- A bug caught by the tests and fixed: a stock line with nothing in stock kept OUR STOCK selected and would have produced a PO to OUR STOCK.

### Epic 8 — PDF
- [x] **8.1 (S)** `PurchaseOrderDocument` Stock variant (ship-to warehouse, no customer ref).
- [x] **8.2 (M)** `StockReceiptDocument` (GRN) + `/pdf/stock-receipt`.
- [x] **8.3 (M)** `StockReportDocument` + `/pdf/stock-report` + Excel export.
- [x] **8.4 (M)** Frontend: PDF dialog changes for Stock POs, GRN / report preview through `useStockPdf` + `DocPreviewModal` (no separate generator components needed).

#### Epic 8 — as built

**8.1 Stock PO PDF.** The existing PO PDF dialog (`PoPdfGenerator.vue`) and `/pdf/po` now handle Stock POs:
- *Ship To* stays the PO's destination warehouse; picking a company only changes the letterhead (before, it overwrote Ship To with the company's own address).
- `/pdf/po` returns **409** for a Stock PO that is not admin-approved; the dialog shows "Preview only" and disables *Download*. Any download error is now shown in the dialog instead of only in the console.
- Downloading a Stock PO PDF **no longer changes its status**. For customer POs a download still moves the PO to *Waiting For Supplier Documents*; for a Stock PO that would have skipped approval or pulled a paid / received PO backwards. It still records the PO date.
- The generated PDF is saved under the PO number (`…/SPO-n/<supplier>/PO/`), next to the Stock PO supplier documents.
- Comments default to "Ref. your PI …" when the Stock PO has a supplier PI number.

**8.2 Goods Receipt Note** — `GET /api/pdf/stock-receipt/{poId}[?movementIds=…]`
- Covers every receipt on the PO, or only the given receipt movements (a single delivery). Number `GRN-{PO}-{last movement id}`.
- Shows PO / PO date / supplier PI / received date(s), received from / at / booked by, and per line: ordered, **this note**, total received, remaining, certificate, bin, track numbers and serials. Includes recount corrections and receipt notes, plus three signature boxes.
- Buttons: **GRN** on the PO page's *Received into Our Stock* card; **Print GRN for this delivery** in the receive dialog right after receiving.
- Returns 404 "Nothing has been received on this Stock PO yet." when there is nothing to print.

**8.3 Stock report / valuation** — `GET /api/pdf/stock-report?{Our Stock filters}&filterLabel=…[&brandPresetId=]`
- Landscape A4, lots grouped by company → warehouse with subtotals, summary tiles, low-stock rows highlighted, active filters printed, page x of y.
- Admin / SuperAdmin get **Stock valuation** (avg cost + value columns and totals); other roles get **Stock report** without cost.
- Letterhead: the chosen preset, else the only company in the filter / result, else Base 105, else the first active preset.
- Data endpoint `GET /api/our-inventory/stock/valuation` (same filters, no paging) — closes 6.5. The Our Stock page's **Report** menu offers the PDF and **Export to Excel** (same columns; cost columns only for admins).

**Verified** against a throwaway database (seeded, then dropped): GRN for the whole PO and for a single delivery, valuation PDF, 404 for an unreceived PO, 409 for an unapproved Stock PO PDF, valuation totals.

### Epic 9 — Frontend
- [x] **9.1 (S)** Nav group, `auth.ts` flag/getter, `menu-access.vue` labels, `auth.global.ts` prefixes.
- [x] **9.2 (L)** `/our-inventory` stock list (DataListPage, cascading filters, value chip).
- [x] **9.3 (M)** `/our-inventory/[id]` detail + `StockMovementsTable`.
- [x] **9.4 (L)** `/our-inventory/purchase-orders/new` + `StockPoLinesEditor` (Excel paste, preset picker, warehouse).
- [x] **9.5 (M)** `/our-inventory/purchase-orders` list.
- [x] **9.6 (M)** Origin guards on `purchase-orders/[id].vue` + `PurchaseOrderDocuments.vue`.
- [x] **9.7 (M)** `/our-inventory/receive` + `StockReceiveDialog`.
- [x] **9.8 (S)** Adjust / Transfer dialogs, `/our-inventory/movements`.
- [x] **9.9 (M)** RFQ chips, Quote/Procurement badges, Part History tab, payment "Stock PO" labels.

#### Epic 9 (9.1–9.7) — as built

**Pages**
| Route | What it does |
|---|---|
| `/our-inventory` | Stock lots (server-side `DataListPage`): search, Warehouse / Company / Condition filters (exclude-self options), *Available only*, *Low stock*. Admins also see avg cost, value and the all-pages stock value in the header. |
| `/our-inventory/[id]` | Lot detail: on hand / reserved / available / cost, lot fields, active reservations, incoming Stock PO lines for the part, serials, movement ledger. |
| `/our-inventory/purchase-orders` | Stock PO list: status, supplier, company, warehouse, total, **paid** (+ latest PR status), **received** progress bar, expected date. Drafts get an edit button. |
| `/our-inventory/purchase-orders/new` (`?id=` edits a draft) | Supplier (approved catalog suppliers), buying company, destination warehouse (only warehouses linked to the company, or all when none are linked), subject, supplier PI #, expected date, line grid with catalog P/N search, **Paste from Excel** (P/N · Qty · Price · Condition, header row skipped, bad rows listed), **Fill from low stock**. *Save draft* or *Save & submit for approval*. |
| `/our-inventory/receive` (admin) | Approved Stock PO lines still to arrive, grouped by PO and filterable by warehouse, with a **Receive** dialog (qty, certificate, tag date, bin, serials — serial count must equal qty). |
| `/purchase-orders/[id]` (existing) | For `origin = Stock`: *Stock PO* badge, *Ship to (Our Stock)* card instead of Sales Order, **Admin Approval** card (approve / reject with note), **Supplier Documents** panel (PI / invoice / certs / packing list / other, amount-mismatch warning) instead of the invoice-keyed Document Center, **Received into Our Stock** card (per-line receipts, links to lots, *Receive without track*), *Edit draft* button, no *Return*, no EndUser / In Shop / Sourcing statuses. |

**Components:** `StockPoLinesEditor`, `PurchaseOrderDocuments`, `StockMovementsTable`, `StockReceiveDialog`; helpers in `utils/stockPo.ts`.

**Permissions:** nav group *Our Inventory* (Our Stock, Stock POs, Receive Stock) is shown to anyone granted `ourInventoryMenu` in Menu Access, whatever their role (SuperAdmin always). *Receive Stock* is admin-only. `/our-inventory/**` is feature-gated in `auth.global.ts`.

**Backend added for these pages** (read side of Epic 6)
| Method | Route | Notes |
|---|---|---|
| GET | `/api/our-inventory/stock` | paged lots; `TotalAmountSum` = stock value (admins only) |
| GET | `/api/our-inventory/stock/filter-options` | exclude-self |
| GET | `/api/our-inventory/stock/{id}` | lot + reservations + incoming + serials |
| GET | `/api/our-inventory/movements` | paged ledger (`stockItemId`, `partNumberId`, `type`, dates, search) |
| GET | `/api/our-inventory/incoming` | approved, not-closed Stock PO lines with quantity left |

Cost fields (`avgUnitCost`, value, movement `unitCost`) are null for non-admin roles (6.6). The Stock PO list/detail now also return `paidAmount` (supplier payments via the PO's PRs), `prStatus`, `qtyOrdered`, `qtyReceived`, and bind the status filter from `status`.

**Behaviour changes to existing code**
- `/purchase-orders` (customer PO list) now requests `origin=Customer`, so Stock POs only appear under Our Inventory.
- Rejecting a Stock PO that is *Waiting For Admin Approval* (`PATCH /purchase-orders/{id}/admin-approval`) now puts it back to **Draft**, because Stock POs can only be edited as drafts. Customer POs are unchanged.
- There was no admin-approval UI anywhere before; the approval card is Stock-PO-only for now.

**Known gaps / decisions for you**
- **PO status is one field for two tracks.** A partial receipt sets the PO to *Received in Warehouse* even if it is still *Waiting For Supplier Documents / PR / Payment*, and a later *Payment Done* sets it back. Paid amount and received progress are shown separately on the list, so nothing is lost, but the status chip only shows the latest step. Worth deciding whether Stock POs should show receipt progress in its own field.
- The Nav *Receive Stock* page is admin-only because manual receiving is; Inventory-role users keep receiving through track numbers in *Warehouse Shippings*, which books stock automatically.
- Adjust / transfer / opening-stock dialogs and the standalone movements page are Epic 6 / 9.8.

### Epic 10 — Dashboard & reporting
- [x] **10.1 (S)** Action Center blocks: Stock PO waiting for PR / payment / receipt, Low stock.
- [x] **10.2 (S)** Dashboard stat cards: stock value, lots below MinQty, incoming value.

*As built:* new Action Center groups **Stock PO Waiting For PR** (approved, no PR yet — admins), **Stock PO Waiting For Receipt** (paid / shipping with quantity left — admins, and Inventory users for their own warehouses) and **Low Stock** (admins); Stock POs waiting for payment already appear in the existing *PO Awaiting Payment* block. Draft Stock POs are no longer listed under *PO Awaiting Admin Approval*. The dashboard shows *Our Stock value* (+ lots, reserved units), *Lots below minimum* and *Incoming on Stock POs* to admins who have Our Inventory access.

### Epic 11 — QA & rollout
- [x] **11.1 (M)** Service tests: ledger math, moving average, idempotent receive, reservation race (rowversion).
- [x] **11.2 (M)** E2E happy path: create SPO → approve → PI upload → PR → POP → final amount → track → receive → RFQ chip → quote → SO → ship.
- [x] **11.3 (S)** Regression: customer PO flow, Total-PN, payment queue, Procurement unassigned list are unchanged.
- [x] **11.4 (S)** Load opening stock, give `ourInventoryMenu` to pilot users, then open it to everyone. *(tooling and checklist ready — see §10; running it is yours)*

*As built:* **`src/Procument.OurInventory.Tests`** (xUnit, in the solution). Each run creates `ProcumentTest_{guid}` from all migrations on the SQL Server in `PROCUMENT_TEST_SQL`, uses the real services with `EnableRetryOnFailure` as in production, and drops the database afterwards. Without the variable the tests are skipped.

```bash
PROCUMENT_TEST_SQL="Data Source=.;User ID=sa;Password=…;TrustServerCertificate=True" dotnet test src/Procument.OurInventory.Tests
```

14 tests: track receipt / recount / rejection reconciliation; manual receipt (over-receipt, serials, moving average, completion); Stock PO create / update under the retrying strategy; reservations (idempotent, partial, consume, reserved stock protected); 5 parallel reservations never over-reserve; ledger sum = on hand; opening import (all-or-nothing, dry run, new P/Ns, serials); adjust + manual transfer + lot edit; warehouse-transfer leg moves stock once and Ready-for-SN skips stock goods; RFQ availability; Sales Order reserve + shortfall split + no PO + issue cost + *Delivered to Customer*; no-stock fallback; Sales Order cancel releases; **regression:** a normal supplier line still becomes a PO item.

The payment (PR → POP → final amount) and PDF steps of the happy path were exercised in the browser against a throwaway database in Epics 4, 8 and 9; the rest is covered by the tests above plus a browser pass over the Epic 6–10 screens.

### Suggested order

`E0 → E1 → E2 → E3 → E4 → E5 → E9 (9.1–9.7) → E8 → E6 → E7 → E10 → E11`

Milestone 1 (usable): E0–E5 + 9.1–9.7 + 8.1 → we can buy for stock, pay from a wallet and receive it.
Milestone 2: E7 → stock shows on RFQs and flows into sales.
Milestone 3: reporting, landed cost, serials, holds.

---

## 9. Decisions and remaining questions

### Decisions now encoded in the implementation

1. **Ownership:** stock lots are owned per **company preset**, warehouse, part number and condition.
2. **Costing:** Epic 5 will use moving-average cost. Landed-cost allocation is disabled initially (`UseLandedCost: false`).
3. **Approval:** Stock POs use the same Admin approval path as customer POs.
4. **Numbering:** Stock POs use the configurable `SPO-{Id}` prefix.
5. **Reservation timing:** hard reservation occurs at Sales Order acceptance; optional quote holds remain a later Epic 7 task.
6. **Supplier stock lists:** the existing `InventoryItem` model remains unchanged and separate from Our Stock.
7. **Negative stock:** disabled (`AllowNegativeStock: false`).
8. **Serials:** the optional serial entity exists in the schema; capture during receipt remains Epic 5.4.

9. **Quote price (default taken):** an Our Stock quote row starts at the lot's average cost and gets the normal margin on the quote. That is why the RFQ chip shows cost to quote builders.
10. **PO status (default taken):** Stock POs keep the single status field; paid amount and received progress are shown separately on the Stock PO list.
11. **Who receives (default taken):** Inventory-role users receive through track numbers only; manual *Receive* stays admin-only.
12. **Landed cost (default taken):** off (`UseLandedCost: false`); receipts use the PO line price.

Any of 9–12 can be changed later without data migration.

---

## 10. Rollout checklist

1. **Back up** the production database.
2. **Try the migrations on a copy** of production: `AddOurInventoryModule`, then `AddOurInventorySalesLinks` (both additive; existing POs are back-filled as `Origin = 'Customer'`).
   ```bash
   dotnet ef database update --project src/Procument.Data --startup-project src/Procument.API
   ```
3. **Run the tests** against a SQL Server you can create databases on (`PROCUMENT_TEST_SQL`, see Epic 11).
4. **Deploy** the API and frontend. On start-up the API creates the **OUR STOCK** catalog supplier if it is missing (`OurInventory:OurStockSupplierName`).
5. **Grant access** in *Admin → Menu Access → Our Inventory* to the pilot users (SuperAdmin always has it).
6. **Load opening stock** with *Our Stock → Import opening stock* (download the template; *Check* before *Import*). Set minimum quantities on the lots you want low-stock alerts for.
7. **Pilot:** raise one real Stock PO end to end (approve → PI → PR → POP → receive → GRN), then quote one RFQ line from Our Stock through to *Issue*.
8. **Open it up:** grant Our Inventory to everyone who needs it.
