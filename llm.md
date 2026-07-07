# Procument — System Reference (LLM Context)

Aviation-parts procurement management platform.
**Backend:** ASP.NET Core (.NET 10), EF Core, SQL Server. Modular monolith under `src/Modules/Procument.Module.*`, API host at `src/Procument.API`, shared code at `src/Procument.Shared`.
**Frontend:** Nuxt 3 / Vue 3 (`<script setup>`) + Vuetify, at `client/app`.
**PDF:** QuestPDF (`src/Procument.API/Pdf`).
**Auth:** JWT with custom claims `role`, `bases` (comma-separated int[] = office/location scoping), `name` (username, used for feature-permission checks).

The whole app runs on a **single SQL Server database** (`AppDbContext`). There is also a **satellite/sync** mechanism (`SatelliteNode`, `SyncRegistry`) that replicates data to per-base satellite apps — but schema-wise it is one database. All PKs are `bigint IDENTITY` named `Id` unless noted. All string status columns are stored as `nvarchar`.

---

## 1. Database Schema — All Tables

Legend: 🔑 = primary key, → = foreign key, ⚑ = indexed, `?` = nullable.

### Module: Catalog (Parts, Customers, Suppliers, Company Presets)

**PartNumbers** — master part catalog
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| Name | nvarchar(200) | ⚑ required |
| NewName | nvarchar(200)? | populated by DB trigger `trg_PopulateNewName` |
| Description | nvarchar(1000)? | |
| Remark | nvarchar(max)? | |
| IsFavorite | bit | |
| SupplierId | bigint? | → Suppliers (Restrict) ⚑ |
| CreatedAt | datetime2 | |

**Alternatives** — alt names for a part → PartNumbers (Cascade). Cols: Name(200), PartNumberId, CreatedAt.

**PartNumberSuppliers** — junction PartNumber↔Supplier (many-to-many). Unique(PartNumberId, SupplierId). Both FKs Cascade.

**Suppliers** — vendors
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| Name | nvarchar(300) | ⚑ required |
| Email | nvarchar(200)? | Phone(50)?, Address(500)? |
| Contacts / Description / Dependency | nvarchar(max)? | |
| Status | nvarchar(450) | ⚑ required — **Pending / Approved / Rejected / Disabled** |
| Username | nvarchar(450)? | ⚑ requester username |
| RequestedByUserId | bigint? | supplier-request workflow |
| IsActive | bit | ModifyAt?, CreatedAt |

**Customers**
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| Name | nvarchar(300) | ⚑ required |
| CustomerCode | nvarchar(100)? | Base(int?) = office scope |
| Email(200)? / Emails(max)? / Phone(50)? | | |
| ContactPerson / Contacts / Country / CompanyType / CurrencyType / Website | nvarchar(max)? | |
| BillTo(500)? / ShipTo(500)? | | |
| ShippingAccount / Description | nvarchar(max)? | |
| ExWork | int? | default incoterm/ex-type (see enums) |
| TermsAndConditions / PITermsAndConditions | nvarchar(max)? | |
| IsActive | bit | ModifyAt?, CreatedAt |

**CompanyPresets** — issuing-company branding/bank presets for PDFs
Id, Name(300), Email(200)?, Phone(100)?, Website(300)?, Location(1000)?, LogoBase64/LogoMimeType(100)?, PrimaryColor(20) def `#1a2744`, AccentColor(20) def `#2563eb`, BankName/AccountNumber/BankAddress/SwiftCode/BeneficiaryName (max)?, FedexAccount?, ShipToAddress?/ShipToPhone?, TermsAndConditions?, CustomPdfHtml (TEXT)?, IsActive, SortOrder, CreatedAt, ModifyAt?.

**CompanyPresetBankAccounts** → CompanyPresets (Cascade). AccountName(200), AccountNumber(200)?, BankName(300)?, BankAddress(1000)?, SwiftCode(50)?, BeneficiaryName(300)?, SortOrder.

### Module: Identity (Users, Permissions)

**Users** — Id, Name(200), Email(200) ⚑unique, Password(500), **Role**(50), IsActive, ModifyAt?, CreatedAt.

**UserBases** → Users (Cascade). Base(int), Unique(UserId, Base). Maps a user to the bases/offices they can see.

**UserCustomers** → Users (Cascade). CustomerId, Unique(UserId, CustomerId). Restricts a user to specific customers.

**UserWarehouses** → (Purchasing). UserId, WarehouseId, Unique(UserId, WarehouseId).

**EntityPermission** — per-record ACL. UserId → Users (Cascade), EntityName(100), EntityId(100 as string), Permission(50). Index(UserId, EntityName) & (EntityName, EntityId, UserId). Used mainly for RFQ assignment: EntityName="RFQ", Permission ∈ **View / Edit**.

**MenuPermissions** — feature-gating. Feature(100) ⚑, UserName(200), Unique(Feature, UserName). Grants a named feature flag to a username.

### Module: RFQ

**RFQs** (RFQHeader)
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| Name | nvarchar(300) | required |
| CustomerId | bigint | → Customers ⚑ |
| Status | nvarchar(450) | required — see RFQ status flow |
| ReceivedDate | datetime2 | LeadTime datetime2 = deadline |
| ExType | int? | incoterm/ex-type |
| Notes / NoQuoteReason / RejectionNote | nvarchar(max)? | |
| UserId | bigint? | ⚑ assigned expert; Index(UserId, Status) |
| CreatedAt ⚑ / ModifyAt? | | |

**RFQItems** → RFQs ⚑. PartNumberId ⚑, Qty(float), Unit(max)?, Condition(100)?, Alt(200)?, Priority(max)?, Note(max)?, IsHighlighted(bit).

**RFQUserReads** (Shared) — read receipts. RFQId, UserId, IsRead, UpdatedAt. Unique(RFQId, UserId).

### Module: Sales (Quotes, Invoices/Sales-Orders, Final Invoices, Payments)

**Quotes**
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| QuoteNumber | nvarchar(100) | ⚑unique required |
| RFQId | bigint | → RFQs ⚑ |
| CustomerId | bigint | ⚑ |
| UserId | bigint | ⚑ author |
| Status | nvarchar(50) | Draft / Sent / Accepted / Rejected |
| Type | int? | QuoteType enum (0 Warehouse, 1 Vendor, 2 Customer) |
| TypeAdditional | nvarchar(max)? | |
| TotalAmount / FinalPrice | decimal(18,2)? | |
| CoefYuan / ExchangeRateYuan | decimal(18,2)? | |
| ValidUntil? / SentAt? / RejectionNote? / ModifyAt? / CreatedAt ⚑ | | |

**QuoteItems** → Quotes ⚑. PartNumberId?, RFQItemId?, ProcumentRecordId? (source price record), Qty(int), UnitPrice/TotalPrice decimal(18,2), Condition(100)?, Alt(200)?, LeadTimeDays(int)?, SortOrder.

**Invoices** (a.k.a. **Sales Order** in UI)
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| InvoiceNumber | nvarchar(100) | ⚑unique |
| QuoteId | bigint | → Quotes ⚑ |
| CustomerId | bigint | ⚑ |
| Status | nvarchar(50) | Draft / Prepayment / Waiting For PrePayment / Running / Finish / Cancelled |
| PaymentStatus | nvarchar(max)? | |
| CustomerPONumber? / CustomerPODate? | | customer's PO reference |
| Subject(max)? | | |
| TotalAmount decimal(18,2); Tax? / Shipping? / ProcessingFee? / PrepaymentPercent? decimal(18,2) | | |
| DefaultBankAccountId? / DefaultDepositWalletId? | bigint? | |
| DeadlineDate? / DueDate? / PaidDate? | | |
| IsCancelled bit / CancelledAt? / CreatedAt ⚑ | | |

**InvoiceItems** → Invoices ⚑. QuoteItemId? ⚑, Qty(int), UnitPrice/TotalPrice, Discount(18,2)?, ExpectedDeliveryDate?. (Selling-price logic has new-path vs legacy-path discount semantics.)

**FinalInvoices** — the shipped/commercial invoice
Id, InvoiceNumber(100) ⚑unique, CustomerId ⚑, ProformaInvoiceId (→ the sales Invoice) ⚑, Status(50) — **Draft / Net30 / Completed / Paid**, TotalAmount(18,2), ShippingCost?/ShippingMethod(100)?, DueDate?/PaidDate?, Notes(2000)?, CreatedAt ⚑.

**FinalInvoiceItems** → FinalInvoices ⚑. InvoiceItemId? ⚑, PartNumberId? ⚑, Qty(int), UnitPrice/TotalPrice, Discount?, Condition(100)?, CertName(200)?, Carrier(200)?, TrackNumber(500)?.

**CustomerPayments** → Invoices ⚑. Amount(18,2), FileName(500) (proof-of-payment upload), Notes(1000)?, CreatedAt.

**PaymentBoxes** — wallets/bank boxes per company. CompanyPresetId ⚑, Name, Currency(10), BankName/AccountNumber/SwiftCode/BankAddress/BeneficiaryName(max)?, CreatedAt.

**PaymentTransactions** — money movement ledger
Amount(18,2), Type(20) **Deposit / Withdraw**, FromType(20) & ToType(20) ∈ **Customer / Wallet / Supplier**, FromCustomerId? / ToSupplierId? / PaymentBoxId / ToPaymentBoxId? / InvoiceId? / PaymentRequestId?, TxCurrency(10)?, ExchangeRate(18,6)?, IsAuto bit, Notes(1000)?, CreatedAt ⚑.

**WalletTransferPendings** — inter-wallet transfer approval workflow
FromBoxId ⚑ / ToBoxId ⚑, DepositAmount / WithdrawAmount(18,2), ExchangeRate(18,2)?, Status(20) ⚑ **Pending / Accepted / Completed / Rejected**, PopFileName(500)?, RejectionNote(1000)?, Created/Accepted/Completed By+At, Notes(1000)?.

### Module: Purchasing (Procurement, POs, Shipping, ILS, Inventory)

**ProcumentRecord** (table `Procument`) — supplier price/offer records per RFQ item (the sourcing sheet)
Id, RFQItemId ⚑, SupplierId ⚑, Type(50) def **"Procument"** ⚑, Price(18,2), Qty(float), UnitPrice?/TotalPrice?/ShippingCost?(float), Coef_1/2/3(float)?, FixPrice(18,2)?, Condition(100)?, Alt(200)?, CertName?, IsCertificated bit, LeadTime?, ShippingPoint?, Unit?, TagDate(date)?, Note/MyNotes?, ParentProcumentId? ⚑ (self-ref), SortOrder, UserId? ⚑, CreatedAt, UpdatedAt?.

**Procurements** — purchasing order-group derived from a sales Invoice
Id, ProcurementNumber(100), InvoiceId ⚑unique (→ Invoice one-to-one), Status(50) def **"Open"** ⚑ — **Open / Finalized / Cancelled**, Notes(2000)?, CreatedBy/FinalizedBy user & At, CreatedAt ⚑.

**ProcurementItems** → Procurements ⚑ — line items being sourced (snapshots RFQ/Quote/Supplier data)
Key cols: SourceInvoiceItemId ⚑, PartNumberId? ⚑, Qty/AcceptedQty(int), UnitPrice/AcceptedUnitPrice/QuoteUnitPrice/SupplierPrice(18,2), **ItemStatus**(50) def **"Open"** ⚑ (Open / …/ fulfilled), CurrentSupplierId? ⚑, FulfilledByPOItemId?, ExpectedDeliveryDate?, LoopCount(int) + LastReturnReason/LastReturnedAt (return loop), plus many `Rfq*`, `Quote*`, `Supplier*` snapshot columns and `Source*` provenance FKs, SortOrder, CreatedAt/UpdatedAt?.

**ProcurementSupplierQuotes** → ProcurementItems ⚑ — competing supplier quotes per item
SupplierId? ⚑, SupplierName(300), Price(18,2), Qty(float), ShippingCost?/ShippingPoint(200)?, Condition(100)?/Alt(200)?/CertName(200)?/LeadTime(100)?/Unit(50)?, **IsSelected** bit (Index ProcurementItemId, IsSelected), SourceProcumentRecordId?, AddedByUserId?, TagDate?, SortOrder, Note?, CreatedAt.

**PurchaseOrders**
| Column | Type | Notes |
|---|---|---|
| Id 🔑 | bigint | |
| PONumber | nvarchar(100) | ⚑unique |
| SupplierId | bigint | ⚑ |
| InvoiceId | bigint? | ⚑ links back to sales Invoice |
| Status | nvarchar(50) | **Waiting For Admin Approval / Waiting For Documents / Waiting For Payment / Completed / Returned / Cancelled** |
| AdminApproval | nvarchar(20) def "Pending" | ⚑ **Pending / Approved / Rejected** + AdminApprovalBy/At/Note |
| PaymentApproval | nvarchar(max) | + PaymentApprovalBy/At/Note |
| PaymentStatus | nvarchar(20) def "NotStarted" | ⚑ + PaymentSubmittedBy/At |
| TotalAmount? / Tax? / Shipping? / ProcessingFee? | decimal(18,2) | |
| PreferredWalletId? | | |
| RejectionNote? / ReturnReason(1000)? / ReturnedBy/At? | | |
| PODate? / CreatedAt ⚑ | | |

**POItems** → PurchaseOrders (POId ⚑) — PO line items
PartNumberId? ⚑, ProcumentId? ⚑, InvoiceItemId?, SourceProcurementItemId? ⚑, SourceSupplierQuoteId?, Qty(int), UnitPrice/TotalPrice(18,2), Condition(100)?, Status(max)?, Note(max)?, PORef(int)?, SupplierId?, ReturnReason(1000)?/ReturnedAt? ⚑/ReturnedFromPOId? (return handling).

**POImportDetails** → PurchaseOrders ⚑unique (1:1) — banking/shipping import info: BankName/Account/Address/City/Country, SwiftCode, ABA, Incoterms(100)?, ShippingMethod(100)?, CourierName(200)?, FedExAccount(200)?, Wirefee(18,2)?, Notes(2000)?.

**PaymentRequests** — payment approval requests for POs. POId? ⚑, PRId?, CompanyPresetId? ⚑, Status(50)? — e.g. **"PENDING APPROVAL"**, IsActive, CreatedAt/ModifyAt?.

#### Shipping / Track Numbers / Warehouses

**Warehouses** — Id, Name(200), Type(30) def **"OurWarehouse"**, DisplayName?, Address(500)?, Email(200)?, Phone(50)?, ShipToAddress?, FedexAccount?, IsActive ⚑, CreatedAt.

**CompanyPresetWarehouses** — junction CompanyPreset↔Warehouse, Unique(CompanyPresetId, WarehouseId), Cascade.

**POItemTrackNumbers** → POItems ⚑ — inbound tracking (supplier→warehouse)
TrackNumber(200), Carrier(200)?, WarehouseId? ⚑, Status(50) ⚑ def **"Ship to Warehouse"** — **Ship to Warehouse / Received in Warehouse / Pending / …**, Notes(1000)?, CreatedAt.

**TrackNumberItems** → (TrackNumberId, POItemId) unique — per-item receiving review
ExpectedQty/ActualQty(int?), CertNeeded?/IsAvailable? bit, Status(20) ⚑ def **"Pending"** — **Pending / Accepted / Rejected**, ReviewedBy/At?, ReviewNote(1000)?, CreatedAt.

**TrackNumberBoxes** — box dims per track number. BoxNumber(int), LengthCm/WidthCm/HeightCm(10,2)?, WeightKg(10,3)?, Notes?, CreatedAt.

**TrackNumberDocuments** — uploaded docs (certs, etc). TrackNumberId ⚑ + POItemId?, FileName(500)/OriginalFileName(500), MimeType(100)?, FileSizeBytes, UploadedBy/At.

**ShipmentNotes** — outbound shipment to customer
Id, SNNumber(50) ⚑unique, WarehouseId ⚑, Status(50) ⚑ def **"Draft"** — **Draft / Waiting for Packing / Ship To USA / Received in Warehouse / Confirmed**, Type(10) def **"DDP"** (DDP / CPT / …), AWBNumber(200)?, TId(200)?, SONumber?, Destination?, PdfFileName(500)?, Customs file fields, CreatedBy/At.

**ShipmentNoteTrackNumbers** — junction ShipmentNote↔TrackNumber, Unique(ShipmentNoteId, TrackNumberId).
**ShipmentNoteBoxes** → ShipmentNotes ⚑ — packing boxes: BoxNumber, dims (10,2)/WeightKg(10,3), TrackNumberId?, Notes?.

#### ILS (independent parts marketplace / quoting sub-system)

**ILSCustomers** — mirror of Customers for the ILS flow (Name(300), CustomerCode, BillTo/ShipTo(2000), TermsAndConditions(2000), ShippingAccount(300), etc).
**ILSItems** — stock offers: PartNumberId ⚑, Price(18,2), Qty(float), Condition(100)?, CertName(200)?, AltPartNumber(200)?, Description(1000)?, LeadTime(100)?, TagDate?, ProcumentRecordId? ⚑, CreatedAt ⚑.
**ILSItemSerials** → ILSItems (Cascade) — per-serial detail: SerialNumber(200), Condition/LeadTime/Location/Notes, Price(18,2)?, Cert/Part image filenames, TagDate?.
**ILSQuotes** — Id, QuoteNumber(100), ILSCustomerId ⚑, Status(50) — **Draft / …**, RfqReference(200)?, BillTo/ShipTo/Notes(2000)?, TotalAmount(18,2), CreatedAt ⚑.
**ILSQuoteItems** → ILSQuote ⚑ — PartNumberId ⚑, ILSItemId?/ILSItemSerialId?, Qty(float), SellPrice/TotalPrice/BasePrice(18,2), Coef(18,4)?, Condition/CertName/LeadTime/SerialNumber/AltPartNumber/Notes.
**ILSProformaInvoices** — Id, PINumber(100), ILSCustomerId ⚑, Status(50) — **Open / Invoiced**, CustomerPONumber(200)?, Subject(300)?, BillTo/ShipTo/Notes(2000)?, TotalAmount(18,2), CreatedAt ⚑.
**ILSProformaInvoiceItems** → ILSProformaInvoice ⚑ — PartNumberId ⚑, ILSItemId?/ILSItemSerialId?, Qty(float), SellPrice/TotalPrice(18,2), SourceQuoteId?/SourceQuoteItemId?, Condition/CertName/LeadTime/SerialNumber/AltPartNumber/Notes.

**CapListItems** — capability list (parts a company can supply). CompanyId ⚑ (→ Supplier), PartNumberId ⚑, Description(1000)?, IsRepair bit ⚑, ProcumentRecordId? ⚑, CreatedAt ⚑.
**InventoryItems** — on-hand stock. CompanyId ⚑, PartNumberId ⚑, Condition(100)?, Description(1000)?, Qty(float), Price(18,2)?, SerialNumber(max)?, CreatedAt ⚑.

### Module: Tasks

**Tasks** (TaskItem) — Id, Title(300), Description(2000)?, AssignedTo(100)/AssignedToUserId?, CreatedByCode(100), **Status (int enum)** ⚑ (0/1/2… = To-Do/In-Progress/Done), IsActive, CreatedAt ⚑, ModifyAt?.

### Module: Shared / Infrastructure

**AuditLogs** — Action(100), ActionCategory?, EntityName(100)/EntityId(100) ⚑, EntityDisplayName?, OldValues/NewValues/AffectedColumns/ContextData/Details?, RelatedEntityType/Id?, IPAddress(50)?, UserId? ⚑, UserName?, Timestamp ⚑.
**Notifications** — UserId ⚑, Type/Message/EntityName/EntityNumber, EntityId, IsRead/IsDismissed bit (Index UserId, IsDismissed), TriggeredByUser?, RejectionNote?, CreatedAt.
**UserPushSubscriptions** — Web-push: UserId, Endpoint, P256dh, Auth, CreatedAt.
**SatelliteNodes** — per-base replication targets: Name(200), BaseNumber(int) ⚑, EndpointUrl(500), PublicKey, SharedSecret?, IsActive, LastSyncAt?.
**SyncRegistries** — sync bookkeeping: EntityName(100), MainAppId/SatelliteAppId/SatelliteNodeId, LastSyncAt, LastSyncHash?.

---

## 2. All Status Values & Enumerations

### Roles (`User.Role`, JWT `role` claim)
`SuperAdmin` · `Admin` · `Payment` · `AHM` · `Expert` · `Inventory`
- **SuperAdmin** — everything (all `can()` checks auto-pass).
- **Admin** — approvals, most operations (`isAdmin` = Admin|SuperAdmin).
- **Payment / AHM** — payment/wallet menus (`isPayment` = Payment|AHM|SuperAdmin).
- **Expert** — tight route allowlist (RFQ→PO pipeline only); special user `SYD` gets extra ILS/shipping/inventory pages.
- **Inventory** — shipping/warehouse pages only.

### RFQ Status (`RFQs.Status`)
`Open` → `In Progress` → `Waiting For Admin` → `Ready To Quote` → `Sent` → `Accepted` / `Rejected` / `No Quote`
- `Open`/`In Progress`: active; auto-expires to `No Quote` past lead time (`RfqAutoExpireService`).
- `Waiting For Admin`: expert submitted, awaiting admin.
- `No Quote`: declined with `NoQuoteReason`.

### Quote Status (`Quotes.Status`)
`Draft` → `Sent` → `Accepted` / `Rejected`.
**QuoteType** enum (`Quotes.Type`): `Warehouse=0`, `Vendor=1`, `Customer=2`.

### Invoice / Sales-Order Status (`Invoices.Status`)
`Draft` → `Prepayment` → `Waiting For PrePayment` → `Running` → `Finish` → (`Cancelled`). `IsCancelled`/`CancelledAt` flag cancellations.

### Procurement Status (`Procurements.Status`)
`Open` → `Finalized` / `Cancelled`.
**ProcurementItem.ItemStatus:** `Open` → (sourced/selected) → fulfilled; supports return loop (`LoopCount`, `LastReturnReason`).

### Purchase Order (`PurchaseOrders`)
- **Status:** `Waiting For Admin Approval` → `Waiting For Documents` → `Waiting For Payment` → `Completed`; plus `Returned`, `Cancelled`. (`Open`/`Not Started` seen during creation.)
- **AdminApproval** (nvarchar(20), def `Pending`): `Pending` → `Approved` / `Rejected`.
- **PaymentStatus** (nvarchar(20), def `NotStarted`).
- **POItem.Status** + return fields (`ReturnedAt`, `ReturnedFromPOId`).

### Shipping
- **POItemTrackNumber.Status** (def `Ship to Warehouse`): `Ship to Warehouse` → `Received in Warehouse` → `Pending` (review) → `Rejected`.
- **TrackNumberItem.Status** (def `Pending`): `Pending` → `Accepted` / `Rejected`.
- **ShipmentNote.Status** (def `Draft`): `Draft` → `Waiting for Packing` → `Ship To USA` → `Received in Warehouse` → `Confirmed`.
- **ShipmentNote.Type** (def `DDP`): `DDP` / `CPT`.

### Final Invoice (`FinalInvoices.Status`)
`Draft` → `Net30` / `Completed` → `Paid`.

### Supplier (`Suppliers.Status`)
`Pending` → `Approved` / `Rejected` / `Disabled` (supplier-request approval workflow).

### Payments / Wallets
- **PaymentTransaction.Type:** `Deposit` / `Withdraw`; **FromType/ToType:** `Customer` / `Wallet` / `Supplier`.
- **WalletTransferPending.Status:** `Pending` → `Accepted` → `Completed` / `Rejected`.
- **PaymentRequest.Status:** e.g. `PENDING APPROVAL` → `Approved`.

### ILS
- **ILSQuote.Status:** `Draft` → …
- **ILSProformaInvoice.Status:** `Open` → `Invoiced`.

### Task Status (`Tasks.Status`, int)
`0` To-Do / `1` In-Progress / `2` Done (integer-backed enum).

### EntityPermission.Permission (per-record ACL)
`View` / `Edit` — primarily for `EntityName="RFQ"` (expert assignment).

### ExType / ExWork (incoterms, int) & Priority
`Customers.ExWork`, `RFQs.ExType`, `RfqExType` = integer incoterm codes chosen per customer and inherited by the RFQ (auto-set when customer changes on the RFQ/bulk forms). `RFQItem.Priority` = free-text priority label per line.

---

## 3. Permission Management

Three independent layers:

### (a) Role — coarse, from JWT `role` claim
Frontend getters in `client/app/stores/auth.ts`: `isAdmin`, `isSuperAdmin`, `isPayment`, `isExpert`, `isInventory`. Backend controllers check `role == "Admin" || "SuperAdmin"` etc.

### (b) Route allowlists — `client/app/middleware/auth.global.ts`
- **Inventory** → only `/shipping`, `/total-shipping`, `/dashboard`, `/attention` (landing → `/shipping`).
- **Expert** → `/rfqs`, `/procument`, `/quotes`, `/procurements`, `/purchase-orders`, `/tasks`, `/attention` (landing → `/rfqs`). Anything else = 404.
- **Expert user `SYD`** → the Expert set **plus** `/ils`, `/total-shipping`, `/shipment-notes`, `/shipping`, `/inventory`, `/catalog`.

### (c) Feature flags — `MenuPermissions` table → `/menu-permissions` API → `authStore.featurePermissions`
Checked via `authStore.can(feature)` (SuperAdmin always true; otherwise username must be in the feature's grant list). Managed in UI at `client/app/pages/menu-access.vue`.

**Known feature keys** (`DEFAULT_FEATURE_PERMISSIONS`):
`customerMenu`, `isAmir`, `newRFQ`, `ilsUsers`, `isPDFSelection`, `paymentMenu`, `companyPresets`, `syncApp`, `systemActivity`, `supplierRequests`, `capList`, `ils`, `shippingMenu`, `actionCenter`, `taskManager`.
Several menu getters combine role + flag, e.g. `paymentMenu = SuperAdmin || can('paymentMenu') || isPayment`; `ilsMenu = SuperAdmin || can('ils') || can('ilsUsers')`.

### (d) Data scoping
- **Bases** (`UserBases` + JWT `bases`): filters lists to the user's offices/locations.
- **UserCustomers**: restricts a user to specific customers.
- **UserWarehouses**: restricts a user to specific warehouses.
- **EntityPermission**: per-RFQ View/Edit assignment for experts.

Auditing: every audited action writes to `AuditLogs` (via `AuditActionFilter` / `AuditService`); users get `Notifications` + optional web-push (`UserPushSubscriptions`).

---

## 4. Roadmap — RFQ → Quote → Sales Order → Procurement → PO → Shipping → Final Invoice → Completed

```
CUSTOMER REQUEST
   │
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 1. RFQ (Request for Quote)                        table: RFQs        │
│    • Customer sends part list → create RFQ (Name, Customer, ExType,  │
│      ReceivedDate, LeadTime/deadline) + RFQItems (PN, Qty, Cond,     │
│      Priority, Alt).  Bulk import splits same-name RFQs by deadline  │
│      into ABA(1)/ABA(2).                                             │
│    • Admin assigns to Expert → EntityPermission(RFQ, View/Edit) +    │
│      RFQs.UserId.  Status: Open.                                    │
│    • Expert works it → In Progress. Past deadline → auto No Quote.   │
└─────────────────────────────────────────────────────────────────────┘
   │  Expert sources prices
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. SOURCING / PRICE RECORDS                       table: Procument   │
│    (ProcumentRecord)                              (Type="Procument") │
│    • Per RFQItem, record supplier offers: Price, Qty, Coef, Cert,   │
│      LeadTime, ShippingPoint.  Feeds the quote.                     │
│    • RFQ → Ready To Quote / Waiting For Admin (admin review).       │
└─────────────────────────────────────────────────────────────────────┘
   │
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. QUOTE                                          tables: Quotes,    │
│                                                          QuoteItems  │
│    • Build Quote from RFQ + price records. QuoteItem pulls          │
│      ProcumentRecordId for pricing. Coef/ExchangeRate (Yuan) applied.│
│    • Status: Draft → Sent (SentAt). Customer replies.               │
│    • RFQ Status → Sent, then Accepted / Rejected (RejectionNote).   │
└─────────────────────────────────────────────────────────────────────┘
   │  Quote Accepted
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 4. SALES ORDER  (UI "Invoice")            tables: Invoices,          │
│                                                  InvoiceItems        │
│    • Convert accepted Quote → Invoice (InvoiceNumber, CustomerPO#).  │
│    • Status: Draft → Prepayment → Waiting For PrePayment.           │
│    • Customer pays deposit → CustomerPayments (proof upload) →       │
│      PaymentTransactions (Deposit into PaymentBox/Wallet).          │
│    • Status → Running once confirmed.                               │
└─────────────────────────────────────────────────────────────────────┘
   │  Purchasing takes over (one Procurement per Invoice, 1:1)
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 5. PROCUREMENT GROUP              tables: Procurements,              │
│                                          ProcurementItems,          │
│                                          ProcurementSupplierQuotes  │
│    • Auto-create Procurement (Status Open) from the Invoice.        │
│    • Each ProcurementItem (from an InvoiceItem) snapshots RFQ/Quote │
│      data. Collect competing ProcurementSupplierQuotes; mark one    │
│      IsSelected=true.  ItemStatus tracks Open→selected.             │
│    • Finalize → Status Finalized → emit Purchase Orders.            │
└─────────────────────────────────────────────────────────────────────┘
   │  grouped by supplier
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 6. PURCHASE ORDER                 tables: PurchaseOrders, POItems,  │
│                                          POImportDetails            │
│    • One PO per supplier (PONumber, SupplierId, InvoiceId back-ref).│
│      POItems ← selected ProcurementItems (SourceProcurementItemId).│
│    • Status: Waiting For Admin Approval                             │
│         → (AdminApproval Pending→Approved)                          │
│         → Waiting For Documents  (POImportDetails: bank/ship info)  │
│         → Waiting For Payment                                       │
│         → (Payment submitted → PaymentTransactions Withdraw→Supplier)│
│         → Completed.   Bad goods → Returned (ReturnReason).         │
└─────────────────────────────────────────────────────────────────────┘
   │  supplier ships to our warehouse
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 7. INBOUND / RECEIVING            tables: POItemTrackNumbers,       │
│                                          TrackNumberItems,          │
│                                          TrackNumberBoxes/Documents │
│    • Add track numbers per POItem: Status Ship to Warehouse →       │
│      Received in Warehouse.                                         │
│    • Per-item review (TrackNumberItem): Expected vs Actual qty,     │
│      CertNeeded/IsAvailable → Pending → Accepted / Rejected.        │
│    • Upload certs/docs (TrackNumberDocuments); box dims for freight.│
└─────────────────────────────────────────────────────────────────────┘
   │  goods accepted & ready
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 8. OUTBOUND SHIPMENT              tables: ShipmentNotes,            │
│                                          ShipmentNoteTrackNumbers,  │
│                                          ShipmentNoteBoxes          │
│    • Build Shipment Note (SNNumber, Warehouse, Type DDP/CPT).       │
│    • Status: Draft → Waiting for Packing → Ship To USA →            │
│      Received in Warehouse → Confirmed.                             │
│    • Attach track numbers + packing boxes (weight/dims); AWB;       │
│      generate Packing List PDF; customs docs.                       │
└─────────────────────────────────────────────────────────────────────┘
   │  shipped to customer
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 9. FINAL INVOICE                  tables: FinalInvoices,           │
│                                          FinalInvoiceItems         │
│    • Generate commercial/Final Invoice from the sales Invoice       │
│      (ProformaInvoiceId). Items carry Carrier + TrackNumber.        │
│    • Status: Draft → Net30 / Completed → Paid (side-by-side PDF).   │
│    • Balance payment → CustomerPayments + PaymentTransactions.      │
└─────────────────────────────────────────────────────────────────────┘
   │  fully paid & delivered
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 10. COMPLETION                                                      │
│    • Sales Invoice → Finish;  PO → Completed;  FinalInvoice → Paid. │
│    • Reporting: Total-PN report joins InvoiceItem ⟵ ProcurementItem │
│      ⟵ POItem for full part-number margin/traceability.            │
└─────────────────────────────────────────────────────────────────────┘
```

### Quick provenance chain (traceability FKs)
`RFQItem` → `ProcumentRecord.RFQItemId` → `QuoteItem.ProcumentRecordId` → `InvoiceItem.QuoteItemId` → `ProcurementItem.SourceInvoiceItemId` → (`ProcurementSupplierQuote.IsSelected`) → `POItem.SourceProcurementItemId` → `POItemTrackNumber` / `TrackNumberItem` → `ShipmentNote` → `FinalInvoiceItem.InvoiceItemId`.

Money chain: `CustomerPayments`/`PaymentTransactions(Deposit→Wallet)` on the sales side; `PaymentTransactions(Withdraw→Supplier)` + `PaymentRequests` on the PO side; `WalletTransferPendings` for inter-wallet moves.
 