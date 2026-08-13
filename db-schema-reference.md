# Procument — Database Structure Reference

Generated from the EF Core model snapshot (`src/Procument.Data/Migrations/AppDbContextModelSnapshot.cs`)
and **verified column-for-column against the live `ProcumentDB`** — 749 documented columns vs 749 live
columns, zero difference in either direction (checked 2026-08-10).

**Engine:** SQL Server · **Schema:** `dbo` · single database (`AppDbContext`).
**60 tables · 749 columns.**

Query library with ready-made summary/join SQL: `db-summary-queries.sql` (all statements executed
successfully against the live database). Per-table notes for RAG ingestion: `tableNotes/`.

**Legend** — `PK` primary key · `FK→T` foreign key to table T · `?` suffix on a type means **nullable** ·
`U` part of a unique index · `⚑` indexed · `def:` column default.

**Conventions**
- Every table has `Id bigint IDENTITY` as its primary key, except `Users`, `Customers` (see each table).
- Timestamps are `datetime2`. `CreatedAt` is set on insert; `ModifyAt` is nullable and only set on update.
- Money is `decimal(18,2)`. Coefficients/rates are `decimal(18,4)` or `decimal(18,6)`. **Never `float`.**
- Booleans are `bit`.
- All status columns are `nvarchar` strings **except `Tasks.Status`, which is an int enum**.

---

## Domain model in one paragraph

An aviation-parts trading business. A customer sends an **RFQ**; an expert sources prices
(**Procument** rows = supplier offers) and issues a **Quote**. An accepted quote becomes an
**Invoice** — which the UI calls a *Sales Order*. Each sales order opens exactly one
**Procurement** group whose **ProcurementItems** collect competing
**ProcurementSupplierQuotes**; the selected offer becomes a **POItem** on a **PurchaseOrder**
to a supplier. Goods arrive against **POItemTrackNumbers** into a **Warehouse**, are packed into
a **ShipmentNote** going out to the customer, and are billed on a **FinalInvoice**.
Money in from customers and out to suppliers moves through **PaymentBoxes** (wallets) via
**PaymentTransactions**.

## Pipeline

```
RFQ ──> Quote ──> Invoice (Sales Order) ──> Procurement ──> PurchaseOrder ──> Tracking/Warehouse ──> ShipmentNote ──> FinalInvoice
 │        │              │                      │                 │                                                       │
RFQItems QuoteItems  InvoiceItems       ProcurementItems       POItems                                          FinalInvoiceItems
                                                │
                                     ProcurementSupplierQuotes  (IsSelected = 1 -> the chosen vendor)
```

**Line-level provenance chain** (each hop is a real FK — traverse only the segment you need):

```
RFQItems.Id
  <- Procument.RFQItemId
  <- QuoteItems.ProcumentRecordId          (QuoteItems.RFQItemId also exists)
  <- InvoiceItems.QuoteItemId
  <- ProcurementItems.SourceInvoiceItemId
  <- ProcurementSupplierQuotes.ProcurementItemId  (IsSelected = 1)
  <- POItems.SourceProcurementItemId
  <- POItemTrackNumbers.POItemId
  <- TrackNumberItems.TrackNumberId
  <- ShipmentNoteTrackNumbers.TrackNumberId -> ShipmentNotes
  <- FinalInvoiceItems.InvoiceItemId
```

---

## Traps that produce wrong answers

| Trap | Reality |
|---|---|
| `Procument` vs `Procurements` | **Two different tables.** `Procument` (one 'e') = pre-acceptance supplier offers. `Procurements` = post-acceptance purchasing group. |
| Looking for `CustomerId` on purchasing tables | It does not exist on `Procurements`, `PurchaseOrders`, `POItems` or `ProcurementItems`. Route via `Procurements.InvoiceId -> Invoices.CustomerId`. |
| `PurchaseOrders.InvoiceId` | **Nullable.** An `INNER JOIN` here silently deletes consolidated POs. |
| `POItems.POId` | **Nullable.** Items can exist unassigned to a PO. |
| `Tasks.Status` | Integer enum (0/1/2). `Status = 'Done'` is a type error. |
| `QuoteItems.Quantity` | Does not exist. The column is `Qty`. |
| `RFQItems.Qty` | Is `float`, not `int`. |
| Cancelled sales orders | `Invoices.IsCancelled` **and** `Status='Cancelled'` both exist. Exclude them from revenue reports and say so. |
| `ProcurementItems` snapshot columns | Denormalized copies, deliberately allowed to drift. Read them directly instead of re-joining upstream. |
| `Customers.Base` | NULL on many older rows. Fall back to `CustomerCode LIKE 'C<base>%'`. |
| Fan-out children | `ProcurementSupplierQuotes`, `POItems`, `POItemTrackNumbers`, `TrackNumberItems`, `ShipmentNoteTrackNumbers`, `RFQItems`, `QuoteItems`, `InvoiceItems`, `CustomerPayments`, `Alternatives`, `PartNumberSuppliers`. Pre-aggregate or `OUTER APPLY TOP 1` before joining more than one. |

---

## Status values

| Column | Values |
|---|---|
| `Users.Role` | SuperAdmin · Admin · Payment · AHM · Expert · Inventory |
| `RFQs.Status` | Open -> In Progress -> Waiting For Admin -> Ready To Quote -> Sent -> Accepted / Rejected / No Quote |
| `Quotes.Status` | Draft -> Sent -> Accepted / Rejected |
| `Quotes.Type` (int) | 0 Warehouse · 1 Vendor · 2 Customer |
| `Invoices.Status` | Draft -> Prepayment -> Waiting For PrePayment -> Running -> Finish · Cancelled |
| `Procurements.Status` | Open -> Finalized / Cancelled |
| `ProcurementItems.ItemStatus` | Open -> sourced/selected -> fulfilled (return loop via `LoopCount`) |
| `PurchaseOrders.Status` | Waiting For Admin Approval -> Waiting For Documents -> Waiting For Payment -> Completed · Returned · Cancelled |
| `PurchaseOrders.AdminApproval` | Pending -> Approved / Rejected |
| `PurchaseOrders.PaymentStatus` | NotStarted -> ... |
| `POItemTrackNumbers.Status` | Ship to Warehouse -> Received in Warehouse -> Pending -> Rejected |
| `TrackNumberItems.Status` | Pending -> Accepted / Rejected |
| `ShipmentNotes.Status` | Draft -> Waiting for Packing -> Ship To USA -> Received in Warehouse -> Confirmed |
| `ShipmentNotes.Type` | DDP · CPT |
| `FinalInvoices.Status` | Draft -> Net30 / Completed -> Paid |
| `Suppliers.Status` | Pending -> Approved / Rejected / Disabled |
| `PaymentTransactions.Type` | Deposit · Withdraw (`FromType`/`ToType` ∈ Customer / Wallet / Supplier) |
| `WalletTransferPendings.Status` | Pending -> Accepted -> Completed / Rejected |
| `Tasks.Status` (**int**) | 0 To-Do · 1 In-Progress · 2 Done |
| `EntityPermissions.Permission` | View · Edit |

**Statuses are a state machine.** "Sent quotes" must include `Accepted` and `Rejected` — those were sent too.
Prefer a timestamp (`SentAt IS NOT NULL`) over a label wherever one exists.

---

# Tables

## Module: Catalog

_Parts, customers, suppliers, company presets_

### PartNumbers

Master part catalog. `Name` is the canonical part number. `NewName` is populated by DB trigger `trg_PopulateNewName`.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| Description | nvarchar(1000)? |  |
| IsFavorite | bit |  |
| Name | nvarchar(200) | ⚑ |
| NewName | nvarchar(200)? |  |
| Remark | nvarchar(max)? |  |
| SupplierId | bigint? | FK→`Suppliers` ⚑ |

### Alternatives

Alternate/superseding names for a part.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| Name | nvarchar(200) |  |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |

### PartNumberSuppliers

Many-to-many: which suppliers carry which part.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| PartNumberId | bigint | FK→`PartNumbers` U |
| SupplierId | bigint | FK→`Suppliers` U |

Composite index (unique): (PartNumberId, SupplierId)

### Suppliers

Vendors. `Status` drives the supplier-request approval workflow.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Address | nvarchar(500)? |  |
| Contacts | nvarchar(max)? |  |
| CreatedAt | datetime2 |  |
| Dependency | nvarchar(max)? |  |
| Description | nvarchar(max)? |  |
| Email | nvarchar(200)? |  |
| IsActive | bit |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(300) | ⚑ |
| Phone | nvarchar(50)? |  |
| RequestedByUserId | bigint? |  |
| Status | nvarchar(450) | ⚑ |
| Username | nvarchar(450)? | ⚑ |

### Customers

Buying companies. `Base` = office/location scope (NULL on older rows — fall back to `CustomerCode` prefix `C<base>%`). `Coef1..3` are quote markup coefficients.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Base | int? |  |
| BillTo | nvarchar(500)? |  |
| Coef1 | decimal(18,4)? |  |
| Coef2 | decimal(18,4)? |  |
| Coef3 | decimal(18,4)? |  |
| CompanyType | nvarchar(max)? |  |
| ContactPerson | nvarchar(max)? |  |
| Contacts | nvarchar(max)? |  |
| Country | nvarchar(max)? |  |
| CreatedAt | datetime2 |  |
| CurrencyType | nvarchar(max)? |  |
| CustomerCode | nvarchar(100)? |  |
| Description | nvarchar(max)? |  |
| Email | nvarchar(200)? |  |
| Emails | nvarchar(max)? |  |
| ExWork | int? |  |
| IsActive | bit |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(300) | ⚑ |
| PITermsAndConditions | nvarchar(max)? |  |
| Phone | nvarchar(50)? |  |
| ShipTo | nvarchar(500)? |  |
| ShippingAccount | nvarchar(max)? |  |
| TermsAndConditions | nvarchar(max)? |  |
| Website | nvarchar(max)? |  |

### CompanyPresets

The issuing company (branding, bank, SMTP) used on generated PDFs.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AccentColor | nvarchar(20) | def:"#2563eb" |
| AccountNumber | nvarchar(max)? |  |
| BankAddress | nvarchar(max)? |  |
| BankName | nvarchar(max)? |  |
| BeneficiaryName | nvarchar(max)? |  |
| CreatedAt | datetime2 |  |
| CustomPdfHtml | TEXT? |  |
| Email | nvarchar(200)? |  |
| FedexAccount | nvarchar(max)? |  |
| ImapEnabled | bit |  |
| ImapHost | nvarchar(max)? |  |
| ImapPort | int? |  |
| ImapSentFolder | nvarchar(max)? |  |
| ImapUseSsl | bit |  |
| IsActive | bit |  |
| Location | nvarchar(1000)? |  |
| LogoBase64 | nvarchar(max)? |  |
| LogoMimeType | nvarchar(100)? |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(300) |  |
| Phone | nvarchar(100)? |  |
| PrimaryColor | nvarchar(20) | def:"#1a2744" |
| ShipToAddress | nvarchar(max)? |  |
| ShipToPhone | nvarchar(max)? |  |
| SmtpEnabled | bit |  |
| SmtpFromDisplayName | nvarchar(max)? |  |
| SmtpFromEmail | nvarchar(max)? |  |
| SmtpHost | nvarchar(max)? |  |
| SmtpPasswordEncrypted | nvarchar(max)? |  |
| SmtpPasswordIv | nvarchar(max)? |  |
| SmtpPort | int? |  |
| SmtpUseSsl | bit |  |
| SmtpUsername | nvarchar(max)? |  |
| SortOrder | int |  |
| SwiftCode | nvarchar(max)? |  |
| TermsAndConditions | nvarchar(max)? |  |
| Website | nvarchar(300)? |  |

### CompanyPresetBankAccounts

Bank accounts belonging to a company preset.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AccountName | nvarchar(200) |  |
| AccountNumber | nvarchar(200)? |  |
| BankAddress | nvarchar(1000)? |  |
| BankName | nvarchar(300)? |  |
| BeneficiaryName | nvarchar(300)? |  |
| CompanyPresetId | bigint | FK→`CompanyPresets` ⚑ |
| SortOrder | int |  |
| SwiftCode | nvarchar(50)? |  |

## Module: Identity

_Users, roles, permissions_

### Users

Application users. `Role` ∈ SuperAdmin/Admin/Payment/AHM/Expert/Inventory.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| Email | nvarchar(200) | U |
| IsActive | bit |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(200) |  |
| Password | nvarchar(500) |  |
| Role | nvarchar(50) |  |

### UserBases

Which bases (offices) a user may see.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Base | int | U |
| CreatedAt | datetime2 |  |
| UserId | bigint | FK→`Users` U |

Composite index (unique): (UserId, Base)

### UserCustomers

Restricts a user to specific customers.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| CustomerId | bigint | U |
| UserId | bigint | FK→`Users` U |

Composite index (unique): (UserId, CustomerId)

### UserWarehouses

Which warehouses a user may see.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| UserId | bigint | FK→`Users` U |
| WarehouseId | bigint | FK→`Warehouses` U |

Composite index (unique): (UserId, WarehouseId)

### EntityPermissions

Per-record ACL. Mostly `EntityName='RFQ'` for expert assignment; `Permission` ∈ View/Edit. `EntityId` is a **string**.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| EntityId | nvarchar(100) | ⚑ |
| EntityName | nvarchar(100) | ⚑ |
| Permission | nvarchar(50) |  |
| UserId | bigint | FK→`Users` ⚑ |

Composite index: (UserId, EntityName)  
Composite index: (EntityName, EntityId, UserId)

### MenuPermissions

Feature flags granted per username (nav gating).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| Feature | nvarchar(100) | U |
| UserName | nvarchar(200) | U |

Composite index (unique): (Feature, UserName)

## Module: RFQ

_Incoming requests for quote_

### RFQs

Request for quote header. `UserId` = assigned expert. `LeadTime` = customer deadline.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 | ⚑ |
| CustomerId | bigint | FK→`Customers` ⚑ |
| ExType | int? |  |
| LeadTime | datetime2 |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(300) |  |
| NoQuoteReason | nvarchar(max)? |  |
| Notes | nvarchar(max)? |  |
| ReceivedDate | datetime2 |  |
| RejectionNote | nvarchar(max)? |  |
| Status | nvarchar(450) | ⚑ |
| UserId | bigint? | FK→`Users` ⚑ |

Composite index: (UserId, Status)

### RFQItems

Requested lines. `Qty` is **float**.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Alt | nvarchar(200)? |  |
| Condition | nvarchar(100)? |  |
| IsHighlighted | bit |  |
| Note | nvarchar(max)? |  |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |
| Priority | nvarchar(max)? |  |
| Qty | float |  |
| RFQId | bigint | FK→`RFQs` ⚑ |
| Unit | nvarchar(max)? |  |

### RFQUserReads

Per-user read receipts on an RFQ.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| IsRead | bit |  |
| RFQId | bigint | U |
| UpdatedAt | datetime2 |  |
| UserId | bigint | U |

Composite index (unique): (RFQId, UserId)

## Module: Sales

_Quotes, sales orders (Invoices), final invoices, customer money_

### Quotes

Our quote to the customer. `FinalPrice` overrides `TotalAmount` when set.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CoefYuan | decimal(18,2)? |  |
| CreatedAt | datetime2 | ⚑ |
| CustomerId | bigint | FK→`Customers` ⚑ |
| ExchangeRateYuan | decimal(18,2)? |  |
| FinalPrice | decimal(18,2)? |  |
| ModifyAt | datetime2? |  |
| QuoteNumber | nvarchar(100) | U |
| RFQId | bigint | FK→`RFQs` ⚑ |
| RejectionNote | nvarchar(max)? |  |
| SentAt | datetime2? |  |
| Status | nvarchar(50) |  |
| TotalAmount | decimal(18,2)? |  |
| Type | int? |  |
| TypeAdditional | nvarchar(max)? |  |
| UserId | bigint | FK→`Users` ⚑ |
| ValidUntil | datetime2? |  |

### QuoteItems

Quoted lines. Quantity column is `Qty` (there is no `Quantity`).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Alt | nvarchar(200)? | ⚑ |
| Condition | nvarchar(100)? |  |
| LeadTimeDays | int? |  |
| PartNumberId | bigint? | FK→`PartNumbers` ⚑ |
| ProcumentRecordId | bigint? | FK→`Procument` ⚑ |
| Qty | int |  |
| QuoteId | bigint | FK→`Quotes` ⚑ |
| RFQItemId | bigint? | FK→`RFQItems` ⚑ |
| SortOrder | int |  |
| TotalPrice | decimal(18,2) |  |
| UnitPrice | decimal(18,2) |  |

### Invoices — *(UI: “Sales Order”)*

**Called "Sales Order" in the UI.** Created from an accepted Quote. `CustomerPONumber` is the customer's own PO reference.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CancelledAt | datetime2? |  |
| CreatedAt | datetime2 | ⚑ |
| CustomerId | bigint | FK→`Customers` ⚑ |
| CustomerPODate | datetime2? |  |
| CustomerPONumber | nvarchar(max)? |  |
| DeadlineDate | datetime2? |  |
| DefaultBankAccountId | bigint? |  |
| DefaultDepositWalletId | bigint? |  |
| DueDate | datetime2? |  |
| InvoiceNumber | nvarchar(100) | U |
| IsCancelled | bit |  |
| PaidDate | datetime2? |  |
| PaymentStatus | nvarchar(max)? |  |
| PrepaymentPercent | decimal(18,2)? |  |
| ProcessingFee | decimal(18,2)? |  |
| QuoteId | bigint | FK→`Quotes` ⚑ |
| Shipping | decimal(18,2)? |  |
| Status | nvarchar(50) |  |
| Subject | nvarchar(max)? |  |
| Tax | decimal(18,2)? |  |
| TotalAmount | decimal(18,2) |  |

### InvoiceItems

Sales-order lines — the selling side of the margin calculation.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Discount | decimal(18,2)? |  |
| ExpectedDeliveryDate | datetime2? |  |
| InvoiceId | bigint | FK→`Invoices` ⚑ |
| Qty | int |  |
| QuoteItemId | bigint? | FK→`QuoteItems` ⚑ |
| TotalPrice | decimal(18,2) |  |
| UnitPrice | decimal(18,2) |  |

### CustomerPayments

Proof-of-payment uploads against a sales order.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Amount | decimal(18,2) |  |
| CreatedAt | datetime2 |  |
| FileName | nvarchar(500) |  |
| InvoiceId | bigint | FK→`Invoices` ⚑ |
| Notes | nvarchar(1000)? |  |

### FinalInvoices — *(UI: “commercial invoice”)*

Commercial/shipped invoice. `ProformaInvoiceId` → `Invoices.Id`.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 | ⚑ |
| CustomerId | bigint | FK→`Customers` ⚑ |
| DueDate | datetime2? |  |
| InvoiceNumber | nvarchar(100) | U |
| Notes | nvarchar(2000)? |  |
| PaidDate | datetime2? |  |
| ProformaInvoiceId | bigint | FK→`Invoices` ⚑ |
| ShippingCost | decimal(18,2)? |  |
| ShippingMethod | nvarchar(100)? |  |
| Status | nvarchar(50) |  |
| TotalAmount | decimal(18,2) |  |

### FinalInvoiceItems

Shipped lines, with cert/tracking detail.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Carrier | nvarchar(200)? |  |
| CertName | nvarchar(200)? |  |
| Condition | nvarchar(100)? |  |
| Discount | decimal(18,2)? |  |
| FinalInvoiceId | bigint | FK→`FinalInvoices` ⚑ |
| InvoiceItemId | bigint? | FK→`InvoiceItems` ⚑ |
| PartNumberId | bigint? | FK→`PartNumbers` ⚑ |
| Qty | int |  |
| TotalPrice | decimal(18,2) |  |
| TrackNumber | nvarchar(500)? |  |
| UnitPrice | decimal(18,2) |  |

### PaymentBoxes

Wallets (per company preset + currency).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AccountNumber | nvarchar(max)? |  |
| BankAddress | nvarchar(max)? |  |
| BankName | nvarchar(max)? |  |
| BeneficiaryName | nvarchar(max)? |  |
| CompanyPresetId | bigint | FK→`CompanyPresets` ⚑ |
| CreatedAt | datetime2 |  |
| Currency | nvarchar(10) |  |
| Name | nvarchar(max) |  |
| SwiftCode | nvarchar(max)? |  |

### PaymentTransactions

Money movements. `FromType`/`ToType` ∈ Customer/Wallet/Supplier.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Amount | decimal(18,2) |  |
| Base | nvarchar(10)? |  |
| CreatedAt | datetime2 | ⚑ |
| ExchangeRate | decimal(18,6)? |  |
| FromCustomerId | bigint? | FK→`Customers` ⚑ |
| FromType | nvarchar(20) |  |
| InvoiceId | bigint? | FK→`Invoices` ⚑ |
| IsAuto | bit |  |
| Notes | nvarchar(1000)? |  |
| PaymentBoxId | bigint | FK→`PaymentBoxes` ⚑ |
| PaymentRequestId | bigint? | FK→`PaymentRequests` ⚑ |
| ToPaymentBoxId | bigint? |  |
| ToSupplierId | bigint? | FK→`Suppliers` ⚑ |
| ToType | nvarchar(20) |  |
| TxCurrency | nvarchar(10)? |  |
| Type | nvarchar(20) |  |

### WalletTransferPendings

Wallet-to-wallet transfers awaiting approval.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AcceptedAt | datetime2? |  |
| AcceptedByUserId | bigint? |  |
| CompletedAt | datetime2? |  |
| CompletedByUserId | bigint? |  |
| CreatedAt | datetime2 | ⚑ |
| CreatedByUserId | bigint |  |
| DepositAmount | decimal(18,2) |  |
| ExchangeRate | decimal(18,2)? |  |
| FromBoxId | bigint | FK→`PaymentBoxes` ⚑ |
| Notes | nvarchar(1000)? |  |
| PopFileName | nvarchar(500)? |  |
| RejectionNote | nvarchar(1000)? |  |
| Status | nvarchar(20) | ⚑ |
| ToBoxId | bigint | FK→`PaymentBoxes` ⚑ |
| WithdrawAmount | decimal(18,2) |  |

## Module: Purchasing

_Sourcing, procurement groups, POs, supplier money_

### Procument — *(UI: “sourcing offer / price record”)*

**Pre-acceptance sourcing offers** (entity `ProcumentRecord`). Note the single-'e' spelling — a different table from `Procurements`.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Alt | nvarchar(200)? |  |
| CertName | nvarchar(max)? |  |
| Coef_1 | float? |  |
| Coef_2 | float? |  |
| Coef_3 | float? |  |
| Condition | nvarchar(100)? |  |
| CreatedAt | datetime2 |  |
| FixPrice | decimal(18,2)? |  |
| IsCertificated | bit |  |
| LeadTime | nvarchar(max)? |  |
| MyNotes | nvarchar(max)? |  |
| Note | nvarchar(max)? |  |
| ParentProcumentId | bigint? | FK→`Procument` ⚑ |
| Price | decimal(18,2) |  |
| Qty | float |  |
| RFQItemId | bigint | FK→`RFQItems` ⚑ |
| ShippingCost | float? |  |
| ShippingPoint | nvarchar(max)? |  |
| SortOrder | int |  |
| SupplierId | bigint | FK→`Suppliers` ⚑ |
| TagDate | date? |  |
| TotalPrice | float? |  |
| Type | nvarchar(50) | ⚑ def:"Procument" |
| Unit | nvarchar(max)? |  |
| UnitPrice | float? |  |
| UpdatedAt | datetime2? |  |
| UserId | bigint? | FK→`Users` ⚑ |

### Procurements — *(UI: “procurement group”)*

**Post-acceptance purchasing group**, 1:1 with an Invoice (unique `InvoiceId`). No CustomerId — reach the customer via `Invoices`.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 | ⚑ |
| CreatedByUserId | bigint? |  |
| FinalizedAt | datetime2? |  |
| FinalizedByUserId | bigint? |  |
| InvoiceId | bigint | U |
| Notes | nvarchar(2000)? |  |
| ProcurementNumber | nvarchar(100) |  |
| Status | nvarchar(50) | ⚑ def:"Open" |

### ProcurementItems

Purchasing worklist line. Carries **denormalized snapshot columns** (`RfqName`, `QuoteNumber`, `QuoteUnitPrice`, `PartNumberName`, `Supplier*`) that are deliberately allowed to drift from the source rows — read them directly.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AcceptedQty | int |  |
| AcceptedUnitPrice | decimal(18,2) |  |
| Alt | nvarchar(200)? |  |
| Condition | nvarchar(100)? |  |
| CreatedAt | datetime2 |  |
| CurrentSupplierId | bigint? | FK→`Suppliers` ⚑ |
| ExpectedDeliveryDate | datetime2? |  |
| FulfilledByPOItemId | bigint? |  |
| ItemStatus | nvarchar(50) | ⚑ def:"Open" |
| LastReturnReason | nvarchar(1000)? |  |
| LastReturnedAt | datetime2? |  |
| LeadTime | nvarchar(100)? |  |
| LoopCount | int |  |
| Note | nvarchar(max)? |  |
| PartNumberDescription | nvarchar(1000)? |  |
| PartNumberId | bigint? | FK→`PartNumbers` ⚑ |
| PartNumberName | nvarchar(200)? |  |
| ProcurementId | bigint | FK→`Procurements` ⚑ |
| Qty | int |  |
| QuoteAlt | nvarchar(200)? |  |
| QuoteCondition | nvarchar(100)? |  |
| QuoteLeadTimeDays | int? |  |
| QuoteNumber | nvarchar(100)? |  |
| QuoteQty | int |  |
| QuoteUnitPrice | decimal(18,2) |  |
| RfqAlt | nvarchar(200)? |  |
| RfqCondition | nvarchar(100)? |  |
| RfqExType | int? |  |
| RfqName | nvarchar(300)? |  |
| RfqNote | nvarchar(max)? |  |
| RfqPriority | nvarchar(50)? |  |
| RfqQty | float? |  |
| RfqUnit | nvarchar(50)? |  |
| ShippingCost | float? |  |
| SortOrder | int |  |
| SourceInvoiceItemId | bigint | ⚑ |
| SourceProcumentRecordId | bigint? |  |
| SourceQuoteId | bigint? |  |
| SourceQuoteItemId | bigint? |  |
| SourceRfqId | bigint? |  |
| SourceRfqItemId | bigint? |  |
| SourceSupplierId | bigint? |  |
| SupplierCertName | nvarchar(200)? |  |
| SupplierCondition | nvarchar(100)? |  |
| SupplierLeadTime | nvarchar(100)? |  |
| SupplierName | nvarchar(300)? |  |
| SupplierPrice | decimal(18,2)? |  |
| UnitPrice | decimal(18,2) |  |
| UpdatedAt | datetime2? |  |

### ProcurementSupplierQuotes

Competing supplier offers per procurement item. `IsSelected = 1` marks the chosen one.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AddedByUserId | bigint? |  |
| Alt | nvarchar(200)? |  |
| CertName | nvarchar(200)? |  |
| Condition | nvarchar(100)? |  |
| CreatedAt | datetime2 |  |
| IsSelected | bit | ⚑ |
| LeadTime | nvarchar(100)? |  |
| Note | nvarchar(max)? |  |
| Price | decimal(18,2) |  |
| ProcurementItemId | bigint | FK→`ProcurementItems` ⚑ |
| Qty | float |  |
| ShippingCost | float? |  |
| ShippingPoint | nvarchar(200)? |  |
| SortOrder | int |  |
| SourceProcumentRecordId | bigint? |  |
| SupplierId | bigint? | FK→`Suppliers` ⚑ |
| SupplierName | nvarchar(300) |  |
| TagDate | date? |  |
| Unit | nvarchar(50)? |  |

Composite index: (ProcurementItemId, IsSelected)

### PurchaseOrders

PO to a supplier. `InvoiceId` is **nullable** (consolidated POs). Three separate status columns: `Status`, `AdminApproval`, `PaymentStatus`.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AdminApproval | nvarchar(20) | ⚑ def:"Pending" |
| AdminApprovalAt | datetime2? |  |
| AdminApprovalBy | bigint? |  |
| AdminApprovalNote | nvarchar(1000)? |  |
| CreatedAt | datetime2 | ⚑ |
| InvoiceId | bigint? | ⚑ |
| PODate | datetime2? |  |
| PONumber | nvarchar(100) | U |
| PaymentApproval | nvarchar(max) |  |
| PaymentApprovalAt | datetime2? |  |
| PaymentApprovalBy | bigint? |  |
| PaymentApprovalNote | nvarchar(max)? |  |
| PaymentStatus | nvarchar(20) | ⚑ def:"NotStarted" |
| PaymentSubmittedAt | datetime2? |  |
| PaymentSubmittedBy | bigint? |  |
| PreferredWalletId | bigint? |  |
| ProcessingFee | decimal(18,2)? |  |
| RejectionNote | nvarchar(max)? |  |
| ReturnReason | nvarchar(1000)? |  |
| ReturnedAt | datetime2? | ⚑ |
| ReturnedByUserId | bigint? |  |
| Shipping | decimal(18,2)? |  |
| Status | nvarchar(50) |  |
| Subject | nvarchar(max)? |  |
| SupplierId | bigint | FK→`Suppliers` ⚑ |
| Tax | decimal(18,2)? |  |
| TotalAmount | decimal(18,2)? |  |

### POItems

PO lines. `POId` is nullable (unassigned/returned items). `SourceProcurementItemId` links back to the demand.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Condition | nvarchar(100)? |  |
| InvoiceItemId | bigint? |  |
| Note | nvarchar(max)? |  |
| POId | bigint? | FK→`PurchaseOrders` ⚑ |
| PORef | int? |  |
| PartNumberId | bigint? | FK→`PartNumbers` ⚑ |
| ProcumentId | bigint? | FK→`Procument` ⚑ |
| Qty | int |  |
| ReturnReason | nvarchar(1000)? |  |
| ReturnedAt | datetime2? | ⚑ |
| ReturnedFromPOId | bigint? |  |
| SourceProcurementItemId | bigint? | FK→`ProcurementItems` ⚑ |
| SourceSupplierQuoteId | bigint? |  |
| Status | nvarchar(max)? |  |
| SupplierId | bigint? |  |
| TotalPrice | decimal(18,2) |  |
| UnitPrice | decimal(18,2) |  |

### POImportDetails

1:1 shipping/bank detail block for a PO.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| ABA | nvarchar(max)? |  |
| BankAccountNumber | nvarchar(100)? |  |
| BankAddress | nvarchar(500)? |  |
| BankCity | nvarchar(200)? |  |
| BankCountry | nvarchar(200)? |  |
| BankName | nvarchar(200)? |  |
| CourierName | nvarchar(200)? |  |
| FedExAccount | nvarchar(200)? |  |
| Incoterms | nvarchar(100)? |  |
| Notes | nvarchar(2000)? |  |
| PurchaseOrderId | bigint | FK→`PurchaseOrders` U |
| ShippingMethod | nvarchar(100)? |  |
| SwiftCode | nvarchar(max)? |  |
| Wirefee | decimal(18,2)? |  |

### PaymentRequests

Payment approval requests against a PO.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CompanyPresetId | bigint? | FK→`CompanyPresets` ⚑ |
| CreatedAt | datetime2 |  |
| IsActive | bit |  |
| ModifyAt | datetime2? |  |
| POId | bigint? | FK→`PurchaseOrders` ⚑ |
| PRId | bigint? |  |
| Status | nvarchar(50)? |  |

## Module: Shipping

_Inbound tracking, warehouses, transfers, outbound shipment notes_

### Warehouses

Our warehouses and supplier drop points (`Type`).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Address | nvarchar(500)? |  |
| CreatedAt | datetime2 |  |
| DisplayName | nvarchar(max)? |  |
| Email | nvarchar(200)? |  |
| FedexAccount | nvarchar(max)? |  |
| IsActive | bit | ⚑ |
| Name | nvarchar(200) |  |
| Phone | nvarchar(50)? |  |
| ServicePriority | nvarchar(max)? |  |
| ShipToAddress | nvarchar(max)? |  |
| Type | nvarchar(30) | def:"OurWarehouse" |

### CompanyPresetWarehouses

Which warehouses belong to which company preset.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CompanyPresetId | bigint | FK→`CompanyPresets` U |
| WarehouseId | bigint | FK→`Warehouses` U |

Composite index (unique): (CompanyPresetId, WarehouseId)

### POItemTrackNumbers — *(UI: “inbound tracking”)*

**Inbound** tracking, supplier → warehouse. This is what "tracking" means by default.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Carrier | nvarchar(200)? |  |
| CreatedAt | datetime2 |  |
| Notes | nvarchar(1000)? |  |
| Origin | nvarchar(20) | def:"Supplier" |
| POItemId | bigint | FK→`POItems` ⚑ |
| ParentTrackNumberId | bigint? | FK→`POItemTrackNumbers` ⚑ |
| SourceTransferId | bigint? | FK→`WarehouseTransfers` ⚑ |
| Status | nvarchar(50) | ⚑ def:"Ship to Warehouse" |
| TrackNumber | nvarchar(200) |  |
| WarehouseId | bigint? | FK→`Warehouses` ⚑ |

### TrackNumberItems

Per-item receiving/review of a track number. Unique(TrackNumberId, POItemId).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| ActualQty | int? |  |
| CertNeeded | bit? |  |
| CreatedAt | datetime2 |  |
| ExpectedQty | int |  |
| IsAvailable | bit? |  |
| POItemId | bigint | FK→`POItems` U |
| ReviewNote | nvarchar(1000)? |  |
| ReviewedAt | datetime2? |  |
| ReviewedByUserId | bigint? | FK→`Users` ⚑ |
| Status | nvarchar(20) | ⚑ def:"Pending" |
| TrackNumberId | bigint | FK→`POItemTrackNumbers` U |
| TransferredOutQty | int |  |

Composite index (unique): (TrackNumberId, POItemId)

### TrackNumberDocuments

Documents attached to a track number / PO item.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| FileName | nvarchar(500) |  |
| FileSizeBytes | bigint |  |
| MimeType | nvarchar(100)? |  |
| OriginalFileName | nvarchar(500) |  |
| POItemId | bigint? | FK→`POItems` ⚑ |
| TrackNumberId | bigint | FK→`POItemTrackNumbers` ⚑ |
| UploadedAt | datetime2 |  |
| UploadedByUserId | bigint | FK→`Users` ⚑ |

Composite index: (TrackNumberId, POItemId)

### TrackNumberBoxes

Physical box weights/dimensions per track number.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| BoxNumber | int |  |
| CreatedAt | datetime2 |  |
| HeightCm | decimal(10,2)? |  |
| LengthCm | decimal(10,2)? |  |
| Notes | nvarchar(max)? |  |
| TrackNumberId | bigint | FK→`POItemTrackNumbers` ⚑ |
| WeightKg | decimal(10,3)? |  |
| WidthCm | decimal(10,2)? |  |

### WarehouseTransfers

Warehouse-to-warehouse movement.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Carrier | nvarchar(200)? |  |
| CreatedAt | datetime2 |  |
| CreatedByUserId | bigint | FK→`Users` ⚑ |
| FromWarehouseId | bigint | FK→`Warehouses` ⚑ |
| Notes | nvarchar(1000)? |  |
| ReceivedAt | datetime2? |  |
| ReceivedByUserId | bigint? | FK→`Users` ⚑ |
| Status | nvarchar(30) | ⚑ def:"In Transit" |
| ToWarehouseId | bigint | FK→`Warehouses` ⚑ |
| TrackNumber | nvarchar(200) |  |
| TransferNumber | nvarchar(50) | U |

### WarehouseTransferItems

Lines within a warehouse transfer.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| POItemId | bigint | FK→`POItems` ⚑ |
| Qty | int |  |
| ReceivedQty | int? |  |
| SourceTrackNumberItemId | bigint | FK→`TrackNumberItems` ⚑ |
| Status | nvarchar(20) | def:"In Transit" |
| WarehouseTransferId | bigint | FK→`WarehouseTransfers` ⚑ |

### ShipmentNotes — *(UI: “outbound shipment”)*

**Outbound** shipment to the customer.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AWBNumber | nvarchar(200)? |  |
| CreatedAt | datetime2 |  |
| CreatedByUserId | bigint | FK→`Users` ⚑ |
| CustomsFileName | nvarchar(500)? |  |
| CustomsOriginalFileName | nvarchar(500)? |  |
| CustomsUploadedAt | datetime2? |  |
| Destination | nvarchar(max)? |  |
| PdfFileName | nvarchar(500)? |  |
| SNNumber | nvarchar(50) | U |
| SONumber | nvarchar(max)? |  |
| Status | nvarchar(50) | ⚑ def:"Draft" |
| TId | nvarchar(200)? |  |
| Type | nvarchar(10) | def:"DDP" |
| WarehouseId | bigint | FK→`Warehouses` ⚑ |

### ShipmentNoteTrackNumbers

M:N junction shipment ↔ track number. **Double fan-out.**

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| ShipmentNoteId | bigint | FK→`ShipmentNotes` U |
| TrackNumberId | bigint | FK→`POItemTrackNumbers` U |

Composite index (unique): (ShipmentNoteId, TrackNumberId)

### ShipmentNoteBoxes

Packing boxes on an outbound shipment.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| BoxNumber | int |  |
| CreatedAt | datetime2 |  |
| HeightCm | decimal(10,2)? |  |
| LengthCm | decimal(10,2)? |  |
| Notes | nvarchar(max)? |  |
| ShipmentNoteId | bigint | FK→`ShipmentNotes` ⚑ |
| TrackNumberId | bigint? | FK→`POItemTrackNumbers` ⚑ |
| WeightKg | decimal(10,3)? |  |
| WidthCm | decimal(10,2)? |  |

## Module: ILS / Inventory

_Standalone stock-listing sales channel (separate from the RFQ pipeline)_

### ILSCustomers

Customers of the ILS channel — separate table from `Customers`.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Address | nvarchar(max)? |  |
| BillTo | nvarchar(2000)? |  |
| ContactPerson | nvarchar(max)? |  |
| Country | nvarchar(300)? |  |
| CreatedAt | datetime2 |  |
| CustomerCode | nvarchar(100)? |  |
| Description | nvarchar(max)? |  |
| Email | nvarchar(200)? |  |
| IsActive | bit |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(300) |  |
| Phone | nvarchar(50)? |  |
| ShipTo | nvarchar(2000)? |  |
| ShippingAccount | nvarchar(300)? |  |
| TermsAndConditions | nvarchar(2000)? |  |
| Website | nvarchar(300)? |  |

### ILSItems

Stock listing items.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AltPartNumber | nvarchar(200)? |  |
| CertName | nvarchar(200)? |  |
| Condition | nvarchar(100)? |  |
| CreatedAt | datetime2 | ⚑ |
| Description | nvarchar(1000)? |  |
| LeadTime | nvarchar(100)? |  |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |
| Price | decimal(18,2) |  |
| ProcumentRecordId | bigint? | FK→`Procument` ⚑ |
| Qty | float |  |
| TagDate | date? |  |

### ILSItemSerials

Serial-level stock under an ILS item.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CertImageFileName | nvarchar(300)? |  |
| CertImageOriginalName | nvarchar(300)? |  |
| CertText | nvarchar(500)? |  |
| Condition | nvarchar(100)? |  |
| CreatedAt | datetime2 |  |
| ILSItemId | bigint | FK→`ILSItems` ⚑ |
| LeadTime | nvarchar(100)? |  |
| Location | nvarchar(100)? |  |
| Notes | nvarchar(1000)? |  |
| PartImageFileName | nvarchar(300)? |  |
| PartImageOriginalName | nvarchar(300)? |  |
| Price | decimal(18,2)? |  |
| SerialNumber | nvarchar(200) |  |
| TagDate | date? |  |

### ILSQuotes

ILS-channel quote.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| BillTo | nvarchar(2000)? |  |
| CreatedAt | datetime2 | ⚑ |
| ILSCustomerId | bigint | FK→`ILSCustomers` ⚑ |
| Notes | nvarchar(2000)? |  |
| QuoteNumber | nvarchar(100) |  |
| RfqReference | nvarchar(200)? |  |
| ShipTo | nvarchar(2000)? |  |
| Status | nvarchar(50) |  |
| TotalAmount | decimal(18,2) |  |

### ILSQuoteItems

ILS quote lines. `ILSItemSerialId` is a soft reference (no FK).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AltPartNumber | nvarchar(max)? |  |
| BasePrice | decimal(18,2)? |  |
| CertName | nvarchar(200)? |  |
| Coef | decimal(18,4)? |  |
| Condition | nvarchar(100)? |  |
| ILSItemId | bigint? | FK→`ILSItems` ⚑ |
| ILSItemSerialId | bigint? |  |
| ILSQuoteId | bigint | FK→`ILSQuotes` ⚑ |
| LeadTime | nvarchar(100)? |  |
| Notes | nvarchar(max)? |  |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |
| Qty | float |  |
| SellPrice | decimal(18,2) |  |
| SerialNumber | nvarchar(200)? |  |
| TotalPrice | decimal(18,2) |  |

### ILSProformaInvoices

ILS-channel proforma invoice.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| BillTo | nvarchar(2000)? |  |
| CreatedAt | datetime2 | ⚑ |
| CustomerPONumber | nvarchar(200)? |  |
| ILSCustomerId | bigint | FK→`ILSCustomers` ⚑ |
| Notes | nvarchar(2000)? |  |
| PINumber | nvarchar(100) |  |
| ShipTo | nvarchar(2000)? |  |
| Status | nvarchar(50) |  |
| Subject | nvarchar(300)? |  |
| TotalAmount | decimal(18,2) |  |

### ILSProformaInvoiceItems

ILS proforma lines.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AltPartNumber | nvarchar(200)? |  |
| CertName | nvarchar(200)? |  |
| Condition | nvarchar(100)? |  |
| ILSItemId | bigint? |  |
| ILSItemSerialId | bigint? |  |
| ILSProformaInvoiceId | bigint | FK→`ILSProformaInvoices` ⚑ |
| LeadTime | nvarchar(100)? |  |
| Notes | nvarchar(max)? |  |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |
| Qty | float |  |
| SellPrice | decimal(18,2) |  |
| SerialNumber | nvarchar(200)? |  |
| SourceQuoteId | bigint? |  |
| SourceQuoteItemId | bigint? |  |
| TotalPrice | decimal(18,2) |  |

### InventoryItems

On-hand inventory records.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CompanyId | bigint | FK→`Suppliers` ⚑ |
| Condition | nvarchar(100)? |  |
| CreatedAt | datetime2 | ⚑ |
| Description | nvarchar(1000)? |  |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |
| Price | decimal(18,2)? |  |
| Qty | float |  |
| SerialNumber | nvarchar(max)? |  |

### CapListItems

Capability list (what a company can supply/repair).

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CompanyId | bigint | FK→`Suppliers` ⚑ |
| CreatedAt | datetime2 | ⚑ |
| Description | nvarchar(1000)? |  |
| IsRepair | bit | ⚑ |
| PartNumberId | bigint | FK→`PartNumbers` ⚑ |
| ProcumentRecordId | bigint? | FK→`Procument` ⚑ |

## Module: Tasks & Infrastructure

_Internal tasks, audit, notifications, base-to-base sync_

### Tasks

Internal task board. **`Status` is an int enum: 0 To-Do, 1 In-Progress, 2 Done** — never compare to a string.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| AssignedTo | nvarchar(100) | ⚑ |
| AssignedToUserId | bigint? | FK→`Users` ⚑ |
| CreatedAt | datetime2 | ⚑ |
| CreatedByCode | nvarchar(100) |  |
| Description | nvarchar(2000)? |  |
| IsActive | bit |  |
| ModifyAt | datetime2? |  |
| Status | int | ⚑ |
| Title | nvarchar(300) |  |

### AuditLogs

Change audit trail. `EntityId` is a string.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Action | nvarchar(100) |  |
| ActionCategory | nvarchar(max)? |  |
| AffectedColumns | nvarchar(max)? |  |
| ContextData | nvarchar(max)? |  |
| Details | nvarchar(max)? |  |
| EntityDisplayName | nvarchar(max)? |  |
| EntityId | nvarchar(100) | ⚑ |
| EntityName | nvarchar(100) | ⚑ |
| IPAddress | nvarchar(50)? |  |
| NewValues | nvarchar(max)? |  |
| OldValues | nvarchar(max)? |  |
| RelatedEntityId | nvarchar(max)? |  |
| RelatedEntityType | nvarchar(max)? |  |
| Timestamp | datetime2 | ⚑ |
| UserId | bigint? | ⚑ |
| UserName | nvarchar(max)? |  |

Composite index: (EntityName, EntityId)

### Notifications

In-app notifications.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| CreatedAt | datetime2 |  |
| EntityId | bigint |  |
| EntityName | nvarchar(max) |  |
| EntityNumber | nvarchar(max) |  |
| IsDismissed | bit | ⚑ |
| IsRead | bit |  |
| Message | nvarchar(max) |  |
| RejectionNote | nvarchar(max)? |  |
| TriggeredByUserId | bigint? |  |
| TriggeredByUserName | nvarchar(max)? |  |
| Type | nvarchar(max) |  |
| UserId | bigint | ⚑ |

Composite index: (UserId, IsDismissed)

### UserPushSubscriptions

Web-push endpoints.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| Auth | nvarchar(max) |  |
| CreatedAt | datetime2 |  |
| Endpoint | nvarchar(max) |  |
| P256dh | nvarchar(max) |  |
| UserId | bigint |  |

### SatelliteNodes

Per-base satellite app nodes.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| BaseNumber | int | ⚑ |
| CreatedAt | datetime2 |  |
| EndpointUrl | nvarchar(500) |  |
| IsActive | bit |  |
| LastSyncAt | datetime2? |  |
| ModifyAt | datetime2? |  |
| Name | nvarchar(200) |  |
| PublicKey | nvarchar(max) |  |
| SharedSecret | nvarchar(max)? |  |

### SyncRegistries

Id mapping between main app and satellites.

| Column | Type | Notes |
|---|---|---|
| Id | bigint | **PK** |
| EntityName | nvarchar(100) | ⚑ |
| LastSyncAt | datetime2 |  |
| LastSyncHash | nvarchar(max)? |  |
| MainAppId | bigint | ⚑ |
| SatelliteAppId | bigint | ⚑ |
| SatelliteNodeId | bigint | FK→`SatelliteNodes` ⚑ |

Composite index: (EntityName, MainAppId)  
Composite index: (EntityName, SatelliteAppId, SatelliteNodeId)

---

# Foreign-key map (every join edge in the database)

Use these exact paths. `*` marks a **nullable** FK — join it with `LEFT JOIN` unless the question requires the link to exist.

| Child table | Column | → Parent table | Nullable |
|---|---|---|---|
| Alternatives | PartNumberId | PartNumbers | no |
| CapListItems | CompanyId | Suppliers | no |
| CapListItems | PartNumberId | PartNumbers | no |
| CapListItems | ProcumentRecordId | Procument | yes * |
| CompanyPresetBankAccounts | CompanyPresetId | CompanyPresets | no |
| CompanyPresetWarehouses | CompanyPresetId | CompanyPresets | no |
| CompanyPresetWarehouses | WarehouseId | Warehouses | no |
| CustomerPayments | InvoiceId | Invoices | no |
| EntityPermissions | UserId | Users | no |
| FinalInvoiceItems | FinalInvoiceId | FinalInvoices | no |
| FinalInvoiceItems | InvoiceItemId | InvoiceItems | yes * |
| FinalInvoiceItems | PartNumberId | PartNumbers | yes * |
| FinalInvoices | CustomerId | Customers | no |
| FinalInvoices | ProformaInvoiceId | Invoices | no |
| ILSItemSerials | ILSItemId | ILSItems | no |
| ILSItems | PartNumberId | PartNumbers | no |
| ILSItems | ProcumentRecordId | Procument | yes * |
| ILSProformaInvoiceItems | ILSProformaInvoiceId | ILSProformaInvoices | no |
| ILSProformaInvoiceItems | PartNumberId | PartNumbers | no |
| ILSProformaInvoices | ILSCustomerId | ILSCustomers | no |
| ILSQuoteItems | ILSItemId | ILSItems | yes * |
| ILSQuoteItems | ILSQuoteId | ILSQuotes | no |
| ILSQuoteItems | PartNumberId | PartNumbers | no |
| ILSQuotes | ILSCustomerId | ILSCustomers | no |
| InventoryItems | CompanyId | Suppliers | no |
| InventoryItems | PartNumberId | PartNumbers | no |
| InvoiceItems | InvoiceId | Invoices | no |
| InvoiceItems | QuoteItemId | QuoteItems | yes * |
| Invoices | CustomerId | Customers | no |
| Invoices | QuoteId | Quotes | no |
| POImportDetails | PurchaseOrderId | PurchaseOrders | no |
| POItemTrackNumbers | POItemId | POItems | no |
| POItemTrackNumbers | ParentTrackNumberId | POItemTrackNumbers | yes * |
| POItemTrackNumbers | SourceTransferId | WarehouseTransfers | yes * |
| POItemTrackNumbers | WarehouseId | Warehouses | yes * |
| POItems | POId | PurchaseOrders | yes * |
| POItems | PartNumberId | PartNumbers | yes * |
| POItems | ProcumentId | Procument | yes * |
| POItems | SourceProcurementItemId | ProcurementItems | yes * |
| PartNumberSuppliers | PartNumberId | PartNumbers | no |
| PartNumberSuppliers | SupplierId | Suppliers | no |
| PartNumbers | SupplierId | Suppliers | yes * |
| PaymentBoxes | CompanyPresetId | CompanyPresets | no |
| PaymentRequests | CompanyPresetId | CompanyPresets | yes * |
| PaymentRequests | POId | PurchaseOrders | yes * |
| PaymentTransactions | FromCustomerId | Customers | yes * |
| PaymentTransactions | InvoiceId | Invoices | yes * |
| PaymentTransactions | PaymentBoxId | PaymentBoxes | no |
| PaymentTransactions | PaymentRequestId | PaymentRequests | yes * |
| PaymentTransactions | ToSupplierId | Suppliers | yes * |
| Procument | ParentProcumentId | Procument | yes * |
| Procument | RFQItemId | RFQItems | no |
| Procument | SupplierId | Suppliers | no |
| Procument | UserId | Users | yes * |
| ProcurementItems | CurrentSupplierId | Suppliers | yes * |
| ProcurementItems | PartNumberId | PartNumbers | yes * |
| ProcurementItems | ProcurementId | Procurements | no |
| ProcurementSupplierQuotes | ProcurementItemId | ProcurementItems | no |
| ProcurementSupplierQuotes | SupplierId | Suppliers | yes * |
| PurchaseOrders | SupplierId | Suppliers | no |
| QuoteItems | PartNumberId | PartNumbers | yes * |
| QuoteItems | ProcumentRecordId | Procument | yes * |
| QuoteItems | QuoteId | Quotes | no |
| QuoteItems | RFQItemId | RFQItems | yes * |
| Quotes | CustomerId | Customers | no |
| Quotes | RFQId | RFQs | no |
| Quotes | UserId | Users | no |
| RFQItems | PartNumberId | PartNumbers | no |
| RFQItems | RFQId | RFQs | no |
| RFQs | CustomerId | Customers | no |
| RFQs | UserId | Users | yes * |
| ShipmentNoteBoxes | ShipmentNoteId | ShipmentNotes | no |
| ShipmentNoteBoxes | TrackNumberId | POItemTrackNumbers | yes * |
| ShipmentNoteTrackNumbers | ShipmentNoteId | ShipmentNotes | no |
| ShipmentNoteTrackNumbers | TrackNumberId | POItemTrackNumbers | no |
| ShipmentNotes | CreatedByUserId | Users | no |
| ShipmentNotes | WarehouseId | Warehouses | no |
| SyncRegistries | SatelliteNodeId | SatelliteNodes | no |
| Tasks | AssignedToUserId | Users | yes * |
| TrackNumberBoxes | TrackNumberId | POItemTrackNumbers | no |
| TrackNumberDocuments | POItemId | POItems | yes * |
| TrackNumberDocuments | TrackNumberId | POItemTrackNumbers | no |
| TrackNumberDocuments | UploadedByUserId | Users | no |
| TrackNumberItems | POItemId | POItems | no |
| TrackNumberItems | ReviewedByUserId | Users | yes * |
| TrackNumberItems | TrackNumberId | POItemTrackNumbers | no |
| UserBases | UserId | Users | no |
| UserCustomers | UserId | Users | no |
| UserWarehouses | UserId | Users | no |
| UserWarehouses | WarehouseId | Warehouses | no |
| WalletTransferPendings | FromBoxId | PaymentBoxes | no |
| WalletTransferPendings | ToBoxId | PaymentBoxes | no |
| WarehouseTransferItems | POItemId | POItems | no |
| WarehouseTransferItems | SourceTrackNumberItemId | TrackNumberItems | no |
| WarehouseTransferItems | WarehouseTransferId | WarehouseTransfers | no |
| WarehouseTransfers | CreatedByUserId | Users | no |
| WarehouseTransfers | FromWarehouseId | Warehouses | no |
| WarehouseTransfers | ReceivedByUserId | Users | yes * |
| WarehouseTransfers | ToWarehouseId | Warehouses | no |
