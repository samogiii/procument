# Feature Plan — OurInventory module (stock purchasing + our own stock)

**Status:** Design, not started
**Date:** 2026-09-22
**Module:** `src/Modules/Procument.Module.OurInventory`
**Frontend:** `client/app/pages/our-inventory/**`

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
- [ ] **0.1 (S)** Create the `Procument.Module.OurInventory` project, csproj refs, `OurInventoryModule.AddOurInventoryModule()`, register it in `Program.cs` and the solution.
- [ ] **0.2 (S)** Add the `OurInventory` config section + `OurInventoryOptions` class.
- [ ] **0.3 (S)** Seed the catalog supplier **OUR STOCK** and the `ourInventoryMenu` permission (idempotent).
- [ ] **0.4 (S)** Define `IPartAvailabilitySource`, `IStockReceiptHandler` (in Purchasing) and `IStockReservationService` (in Shared).

**Done when:** the API starts with the empty module, and the new interfaces resolve through DI.

### Epic 1 — Database
- [ ] **1.1 (M)** Entities `OurStockItem`, `OurStockMovement`, `OurStockReservation`, `OurStockSerial`.
- [ ] **1.2 (S)** `PurchaseOrderDocument` entity (Purchasing).
- [ ] **1.3 (S)** New columns: `PurchaseOrder.Origin/DestinationWarehouseId/SupplierPIRef/ExpectedDeliveryDate`, `POItem.StockItemId`, `QuoteItem.SourceStockItemId` (+ `SupplierQuote` if the quote row lives there).
- [ ] **1.4 (M)** `AppDbContext` config: precision, the unique lot index, rowversion, FKs (`Restrict`), the computed `QtyAvailable`.
- [ ] **1.5 (S)** Migration `AddOurInventoryModule`, with a backfill of `Origin='Customer'`. Review the generated SQL.

**Done when:** the migration applies on a copy of prod data with no data changes to existing rows.

### Epic 2 — Stock Purchase Orders (backend)
- [ ] **2.1 (L)** `StockPurchaseOrderService.CreateAsync/UpdateAsync/SubmitAsync` (preset → wallet resolution copied from `PurchaseOrderService.CreateAsync`, `SPO-{Id}` numbering, `PORef`).
- [ ] **2.2 (M)** `StockPurchaseOrdersController` list + filter-options (exclude-self), paged, with `TotalAmountSum`.
- [ ] **2.3 (S)** `/suggest` (low-stock → draft lines).
- [ ] **2.4 (M)** Make the existing PO endpoints origin-aware: `/{id}` and `/enriched` return `origin` and the destination warehouse; the list endpoint accepts an `origin` filter; `/pdf-data` handles `InvoiceId == null`.
- [ ] **2.5 (M)** Check every `InvoiceId`/`InvoiceItemId` assumption (payment queue, Action Center, Total-PN, procurement unassigned, TrackNumber summary) and add null-safe handling or explicit `Origin` filters.

**Done when:** a Stock PO can be created, edited, submitted and approved, and it shows on the existing PO detail page without errors.

### Epic 3 — Supplier documents (PI etc.)
- [ ] **3.1 (M)** `PurchaseOrderDocumentsController` (upload/list/download/delete) using `DocumentStorageService.SaveFileInSupplierCategory(po.PONumber, …)`.
- [ ] **3.2 (S)** Moving to "Waiting For PR" requires at least one `SupplierPI` document for Stock POs. Warn when PI amount ≠ PO total.

### Epic 4 — PR, payment & wallets
- [ ] **4.1 (S)** Test the PR create → amount → POP → final amount path on a Stock PO. Expected: no code change, bank fee transaction created, `Payment Done` applied.
- [ ] **4.2 (S)** `PaymentRequestDocument`: show `SupplierPIRef` when there is no customer invoice.
- [ ] **4.3 (S)** Wallet transaction list and payment queue: label "Stock PO" + a link to the PO.
- [ ] **4.4 (S)** Partial payments (`PARTIALLY PAID`) work for Stock POs.

**Done when:** paying a Stock PO debits the chosen wallet and records the POP and bank fee, exactly as a customer PO does.

### Epic 5 — Receiving into stock
- [ ] **5.1 (L)** `StockLedgerService` (receipt / issue / adjust / transfer / reserve / release; single transaction; rowversion retry; moving-average cost).
- [ ] **5.2 (M)** `StockReceiptHandler` + hook in `ShippingService` at both "Received in Warehouse" points (Stock origin only, idempotent per track + line, partial qty).
- [ ] **5.3 (M)** Manual receive endpoint for items that arrive without a track number.
- [ ] **5.4 (S)** Serial capture on receive (optional table).
- [ ] **5.5 (S)** PO auto → `Completed` when all lines are fully received.

**Done when:** receiving a track for a Stock PO creates or updates exactly one stock lot and one Receipt movement, and doing it twice does nothing.

### Epic 6 — Stock management
- [ ] **6.1 (M)** `OurStockController`: list, filter-options, detail, by-part, patch.
- [ ] **6.2 (M)** Opening balance + Excel bulk import (reuse the Inventory bulk-import pattern).
- [ ] **6.3 (S)** Adjust (reason required, admin only).
- [ ] **6.4 (M)** Transfer between warehouses (hook into `WarehouseTransferService` when shipped physically).
- [ ] **6.5 (S)** Movements endpoint + valuation endpoint.
- [ ] **6.6 (S)** Cost fields stripped for non-admin roles.

### Epic 7 — RFQ / Quote / Sales integration
- [ ] **7.1 (M)** `StockAvailabilitySource` (available lots + incoming PO lines) → `AvailabilityService` → `OurStockRecords` in `PartAvailabilityResponse`.
- [ ] **7.2 (M)** Quote row keeps `SourceStockItemId` from `applyAvailability`; supplier = OUR STOCK.
- [ ] **7.3 (L)** Sales Order accept → `ReserveAsync` (partial reserve + remainder to Procurement); Sales Order cancel → `ReleaseAsync`.
- [ ] **7.4 (M)** Procurement: reserved lines = Ready / From Stock, no POItem.
- [ ] **7.5 (M)** Ship to customer → `ConsumeAsync` → `Issue` movement with cost; Total-PN shows stock cost.
- [ ] **7.6 (S)** Soft quote holds with `ExpiresAt` + cleanup job (optional).

**Done when:** a PN in stock shows a purple chip on an RFQ, and following it through Quote → Sales Order → Ship lowers the stock by the shipped qty.

### Epic 8 — PDF
- [ ] **8.1 (S)** `PurchaseOrderDocument` Stock variant (ship-to warehouse, no customer ref).
- [ ] **8.2 (M)** `StockReceiptDocument` (GRN) + `/pdf/stock-receipt`.
- [ ] **8.3 (M)** `StockReportDocument` + `/pdf/stock-report` + Excel export.
- [ ] **8.4 (M)** Vue generators `StockPoPdfGenerator.vue`, `StockReceiptPdfGenerator.vue`.

### Epic 9 — Frontend
- [ ] **9.1 (S)** Nav group, `auth.ts` flag/getter, `menu-access.vue` labels, `auth.global.ts` prefixes.
- [ ] **9.2 (L)** `/our-inventory` stock list (DataListPage, cascading filters, value chip).
- [ ] **9.3 (M)** `/our-inventory/[id]` detail + `StockMovementsTable`.
- [ ] **9.4 (L)** `/our-inventory/purchase-orders/new` + `StockPoLinesEditor` (Excel paste, preset picker, warehouse).
- [ ] **9.5 (M)** `/our-inventory/purchase-orders` list.
- [ ] **9.6 (M)** Origin guards on `purchase-orders/[id].vue` + `PurchaseOrderDocuments.vue`.
- [ ] **9.7 (M)** `/our-inventory/receive` + `StockReceiveDialog`.
- [ ] **9.8 (S)** Adjust / Transfer dialogs, `/our-inventory/movements`.
- [ ] **9.9 (M)** RFQ chips, Quote/Procurement badges, Part History tab, payment "Stock PO" labels.

### Epic 10 — Dashboard & reporting
- [ ] **10.1 (S)** Action Center blocks: Stock PO waiting for PR / payment / receipt, Low stock.
- [ ] **10.2 (S)** Dashboard stat cards: stock value, lots below MinQty, incoming value.

### Epic 11 — QA & rollout
- [ ] **11.1 (M)** Service tests: ledger math, moving average, idempotent receive, reservation race (rowversion).
- [ ] **11.2 (M)** E2E happy path: create SPO → approve → PI upload → PR → POP → final amount → track → receive → RFQ chip → quote → SO → ship.
- [ ] **11.3 (S)** Regression: customer PO flow, Total-PN, payment queue, Procurement unassigned list are unchanged.
- [ ] **11.4 (S)** Load opening stock, give `ourInventoryMenu` to pilot users, then open it to everyone.

### Suggested order

`E0 → E1 → E2 → E3 → E4 → E5 → E9 (9.1–9.7) → E8 → E6 → E7 → E10 → E11`

Milestone 1 (usable): E0–E5 + 9.1–9.7 + 8.1 → we can buy for stock, pay from a wallet and receive it.
Milestone 2: E7 → stock shows on RFQs and flows into sales.
Milestone 3: reporting, landed cost, serials, holds.

---

## 9. Open questions (answer before Epic 2)

1. **Ownership:** is stock owned per **company preset** (a separate lot per company), or shared across companies?
2. **Costing:** moving average (proposed) or FIFO? Include shipping/tax/bank fee in the cost (landed cost) from day 1?
3. **Approval:** do Stock POs need the same Admin approval as customer POs, or can a user with `ourInventoryMenu` go straight to PR?
4. **Numbering:** `SPO-{Id}` or share the `PO-{Id}` sequence with a badge?
5. **Quote price:** the price on an "Our Stock" quote row is our **cost**, and the sales margin is added on the quote as usual. Is that correct?
6. **Reservation timing:** reserve at **Quote** (soft hold) or only at **Sales Order accept** (proposed)?
7. **Existing `InventoryItem` table (supplier stock lists):** keep it as it is (proposed), or merge/rename it to "Supplier Stock" in the UI to avoid confusion?
8. **Serialized parts:** do we need per-serial tracking in Milestone 1?
