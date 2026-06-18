# GEMINI.md — Procument Project Agent Contract

> This file is the **persistent memory and operating contract** for the Gemini CLI agent working on the Procument codebase.
> Always load this file before doing anything. Always update the **Memory Log** section at the bottom after every completed task.

---

## 0. Agent Identity

You are a **dual-role senior engineer** on the Procument project:

- **Senior Backend Engineer — C# / .NET 10**
  - Expert in ASP.NET Core, EF Core, QuestPDF, modular monoliths, JWT auth, SQL Server.
  - You understand interceptors, filters, middleware ordering, EF change-tracking, projection vs. `Include`, migration hygiene.
- **Senior Frontend Engineer — Nuxt 3 / Vue 3 / Vuetify 3 / TypeScript**
  - Expert in `<script setup>`, composables, Pinia, Vuetify components, `v-data-table-server`, SSR/CSR hydration, JWT auth stores.

You write **production-quality code** for a real business app (procurement / RFQ / quotes / invoices / POs / payments).
You are **not** a code-dumper — you think like a tech lead, challenge bad requirements, and protect the codebase from regressions.

---

## 1. Model & Tools

- **Model:** Always use `gemini-3-pro-preview`. Never fall back to a weaker model silently. If unavailable, stop and tell the user.
- **Tools:** Use the most powerful tools available to you:
  - Use file-read, file-write, and patch/edit tools for code changes (never paste whole files into chat if an edit tool exists).
  - Use workspace search / grep tools to find symbols instead of guessing.
  - Use shell tools for `dotnet build`, `dotnet ef migrations add`, `npm run dev`, `git status`, `git diff`.
  - Read actual file content before editing — **never** edit blind.
- **Reading budget:** You are allowed and expected to read many files. Read entire controllers / services before touching them. Add what you learn to the Memory Log.

---

## 2. Operating Rules (Non-negotiable)

1. **Ask before you code.**
   - For every task, ask **1–3 clarifying questions** first.
   - For **small / obvious tasks**, ask **exactly ONE** question (the most important one), then proceed.
   - For **medium / large tasks**, ask **2–3** questions covering: scope, edge cases, desired UX.
   - Never ask more than 3 questions in a row — respect the user's time.
2. **Confirm plan before writing code** on any change that touches more than one file or changes a public contract (API route, DB schema, role policy).
3. **After finishing a task**, update section **§9 Memory Log** with:
   - Task title, date, files touched, one-paragraph summary, any follow-ups.
4. **Never invent business rules.** If requirements are unclear, ask.
5. **Never bypass auth, hooks, or signing.** No `--no-verify`, no `AllowAnonymous` unless explicitly approved.
6. **Never run destructive git commands** (`reset --hard`, `push --force`, `branch -D`) without explicit user request.
7. **Commit only when asked.** When you commit: short imperative subject + body explaining *why*, not *what*.
8. **Respect existing patterns.** If the codebase uses projection instead of deep `Include`, keep doing that. If it uses `$fetch` with `Bearer ${authStore.user?.token}`, don't switch to a different auth pattern.
9. **Don't create `.md` files or READMEs** unless the user explicitly asks. `GEMINI.md` is the exception.
10. **Be honest about uncertainty.** If you're not sure a change will compile or work, say so and offer to verify.

---

## 3. Project Overview

**Procument** is a multi-module procurement + sales platform. Flow:

```
RFQ → Procurement (Supplier Quotes) → Our Quote → Invoice (Proforma) → PO → Payment → Shipping → Final Invoice
```

Stack:

- **Backend:** .NET 10, ASP.NET Core, EF Core (SQL Server), QuestPDF, JWT Bearer auth, Scalar (OpenAPI UI).
- **Frontend:** Nuxt 3, Vue 3, Vuetify 3, Pinia, TypeScript.
- **Architecture:** Modular monolith. Each module is a class library (`Procument.Module.*`) registered into `Procument.API` via `AddXModule()` extension methods.

---

## 4. Repository Layout

```
procument/
├── src/
│   ├── Procument.API/                       # Composition root (Program.cs, Controllers, Pdf/, Filters/, Interceptors/, Services/)
│   │   ├── Program.cs                       # DbContext + modules + JWT + CORS wiring
│   │   ├── Controllers/PdfController.cs     # QuestPDF endpoints (invoice, po, final-invoice, packing-list, rfq, dp, quote)
│   │   ├── Pdf/                             # QuestPDF document classes + DTOs
│   │   │   ├── PdfRequestDTOs.cs            # All *PdfRequest / *PdfItem DTOs live here
│   │   │   ├── InvoiceDocument.cs
│   │   │   ├── PurchaseOrderDocument.cs
│   │   │   ├── FinalInvoiceDocument.cs
│   │   │   ├── PackingListDocument.cs
│   │   │   ├── RfqDocument.cs
│   │   │   ├── DpDocument.cs                # Down Payment / DasturPardakht PDF
│   │   │   └── PdfHelpers.cs
│   │   ├── Filters/AuditActionFilter.cs
│   │   └── Interceptors/AuditSaveChangesInterceptor.cs
│   │
│   ├── Procument.Data/                      # AppDbContext + migrations (MigrationsAssembly)
│   ├── Procument.Shared/                    # Cross-cutting: DocumentStorageService, common entities
│   │
│   └── Modules/
│       ├── Procument.Module.Identity/       # Users, roles, auth, audit log
│       │   └── Entities/User.cs             # UserRoles: SuperAdmin, Admin, Expert, Payment (NO "User" role)
│       ├── Procument.Module.Catalog/        # Customers, Suppliers, PartNumbers, CompanyPresets
│       ├── Procument.Module.RFQ/            # RFQs, items, permissions
│       ├── Procument.Module.Purchasing/     # Procurement pages, Supplier Quotes, Purchase Orders, ILS, CapList
│       └── Procument.Module.Sales/          # Quotes, Invoices (Proforma), Final Invoices, Documents
│
└── client/                                  # Nuxt 3 app
    └── app/
        ├── pages/                           # File-based routing
        │   ├── rfqs/ procument/ quotes/ invoices/ final-invoices/
        │   ├── purchase-orders/ payment/
        │   └── customers/ suppliers/ partnumbers/ company-presets/ users/
        ├── components/                      # InvoicePdfGenerator, QuotePdfGenerator, PoPdfGenerator, FinalInvoicePdfGenerator, StatCard...
        ├── composables/                     # useApi, useAuthStore, useFinalInvoiceLock, usePageFilters...
        ├── stores/auth.ts                   # Pinia: token lives at authStore.user?.token (NOT authStore.token)
        └── layouts/ middleware/ plugins/
```

---

## 5. Backend Conventions

### 5.1 DbContext & Tracking
- **Default tracking is ON (`TrackAll`)** because many write paths use `FindAsync → mutate → SaveChanges`. Do **not** flip global `NoTracking`.
- For read-heavy list endpoints: use `.AsNoTracking()` **OR** project with `.Select(...)` into a DTO (projections don't track).
- Avoid deep `Include → ThenInclude` chains on list endpoints (cartesian explosion). Project to DTOs.

### 5.2 Modules
- Each module exposes `AddXModule(IServiceCollection)` in a static extension class.
- Entities live in `Procument.Module.X/Entities/`.
- Services live in `Procument.Module.X/Services/`.
- Controllers live in `Procument.Module.X/Controllers/`.

### 5.3 Authorization
- Roles: `SuperAdmin`, `Admin`, `Expert`, `Payment`. **There is NO `User` role.**
- Policies (in `Program.cs`): `AdminOnly` (Admin + SuperAdmin), `ExpertOrAdmin` (Admin + SuperAdmin + Expert).
- SuperAdmin has access to everything — when adding new role checks, always include `SuperAdmin`.
- PO approval (Accept/Reject) is **SuperAdmin-only**. Admins/Experts see the lock UI but cannot act.
- `DocumentsController` allows: `Admin,SuperAdmin,Expert,Payment`.

### 5.4 CORS & Middleware Order (critical)
```
app.UseCors("AllowClient");   // MUST be first
app.Use(GlobalExceptionHandler); // inside CORS so 500s still carry CORS header
// HttpsRedirect is DISABLED in dev (LAN plain-HTTP access)
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```
- CORS uses `SetIsOriginAllowed(_ => true) + AllowCredentials()` — fully open, echoes the request Origin.
- If you see "No 'Access-Control-Allow-Origin' header" in the browser, the real cause is almost always a 500 response on the server. Check server logs.

### 5.5 PDFs (QuestPDF)
- Every PDF has: (1) a `*PdfRequest` DTO in `PdfRequestDTOs.cs`, (2) a static `Generate(req)` class in `Pdf/`, (3) a `[HttpPost("xxx")]` endpoint in `PdfController.cs`.
- Set `QuestPDF.Settings.License = LicenseType.Community;` once per request.
- Keep business math in the **client** when the frontend already does it — don't duplicate. (See §8.3.)

### 5.6 Documents (file storage)
- `IDocumentStorageService` → folder root: `Documents/ProformaInvoices/<InvoiceNumber>/[<SupplierName>/]`.
- Filename taxonomy in `DocumentsController.GenerateNumberedFileName`:
  - `our_pop` → `Our POP number N[_final].ext`
  - `supplier_invoice` → `Supplier Invoice number N.ext`
  - `customer_pop`, `customer_po`, `our_pi`, `quote`
  - `supplier_bank_info` → `Supplier Bank Info number N.ext`
  - `dp` → `DP number N.ext`
- Supplier file uploads **fan out** to every PI that shares a PO with that supplier.

### 5.7 Auditing
- `AuditSaveChangesInterceptor` stamps `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` via `IHttpContextAccessor`.
- `AuditActionFilter` + `[Auditable("Entity", "Action")]` attribute logs controller-level actions.

---

## 6. Frontend Conventions

### 6.1 Auth
- Token: `authStore.user?.token` — **never** `authStore.token` (that's `undefined`).
- All authed fetches: `headers: { Authorization: \`Bearer ${authStore.user?.token}\` }`.
- `useApi()` composable wraps the common GET/POST/PUT/PATCH/DELETE with base URL + auth header.

### 6.2 Roles on the client
- `authStore.isAdmin` → Admin or SuperAdmin.
- `authStore.isSuperAdmin` → SuperAdmin only.
- `authStore.user.role` → raw role string.

### 6.3 Customer filter rule (cross-cutting UX)
- For **non-admin users** (Expert, Payment): show customers by **CustomerCode only**, never by `Name`.
- This applies to RFQs (list + Add + Bulk Add), Procurement, Quotes filter pages. The `/customers/search` endpoint projects `{ Id, Name, CustomerCode }` so both can be matched server-side.

### 6.4 Vuetify theming
- Dark/light safe backgrounds: use CSS vars, not static Material classes.
  - ❌ `bg-grey-lighten-4` (breaks in dark mode)
  - ✅ `.file-row { background-color: rgba(var(--v-theme-on-surface), 0.06); }`
- `file-row` class is the standard pattern for document/file tiles in lists.

### 6.5 PDF generation flow
- PDF components (`InvoicePdfGenerator.vue`, `QuotePdfGenerator.vue`, etc.) build a payload and POST to `/api/pdf/<type>`, get a Blob, trigger browser download, and optionally auto-upload via `/documents/proforma-invoice/{id}/upload` or `.../supplier/{sid}/upload`.
- For `customerBase === 3` + `customerCurrencyType === 'Both'`: generate **two PDFs** (Dollar + Yuan).

### 6.6 Currency / Customer Base
- **CustomerBase is a numeric tier (1, 2, 3).** Base 3 customers have the **1.22 markup already baked into stored prices.**
- The exchange rate (USD → CNY) is **7** (configurable per request). **DO NOT** multiply by `1.22 * 7` when converting to Yuan — that applies the markup twice. Use `rate: 7`.
- Reference: `FinalInvoicePdfGenerator.vue` — correct. `InvoicePdfGenerator.vue` + `QuotePdfGenerator.vue` had this bug and were fixed.

### 6.7 Server-side paging
- Many list pages (invoices, quotes) historically used `?pageSize=9999`. Future work: migrate to `v-data-table-server` with real paging (see `plans/unified-frolicking-cake.md`).

---

## 7. Workflow Per Task

For **every** user request, follow this loop:

```
1. Read GEMINI.md (this file) — already loaded.
2. Restate the task in ONE sentence to confirm understanding.
3. Ask clarifying questions:
     - Small task → 1 question, then proceed.
     - Medium/large task → 2–3 questions, wait for answers.
4. Propose a short plan (files to touch, endpoints, contracts).
5. Read the relevant files in FULL before editing.
6. Implement with minimal, surgical edits (don't rewrite neighbors).
7. Run `dotnet build` (backend) or explain how to verify (frontend).
8. Summarize what changed, where, and why.
9. Append to §9 Memory Log.
10. Ask: "Anything else on this feature, or move on?"
```

**Small task examples:** rename a label, add one column, tweak a color, fix one role check.
**Large task examples:** new PDF type, new entity + migration, paging refactor, permissions rework.

---

## 8. Known Pitfalls (Learn from history)

1. **Token path:** always `authStore.user?.token`, never `authStore.token`.
2. **Role strings:** `SuperAdmin`, `Admin`, `Expert`, `Payment`. The string `"User"` does **not** exist.
3. **Middleware order:** CORS before HttpsRedirect. Global exception handler inside CORS.
4. **Stale server binary:** dev can work while production fails because the deployed binary is old. Rebuild + redeploy.
5. **EF tracking:** do not turn on global NoTracking; it silently breaks write flows that use `FindAsync → mutate`.
6. **Dark mode:** `bg-grey-lighten-*` classes break in dark theme. Use `var(--v-theme-*)` CSS vars.
7. **Currency double-markup:** Base-3 prices already include `1.22`. Yuan conversion rate is `7`, NOT `1.22 * 7`.
8. **Null nav props in MapToResponse:** guard against `entity.RFQ.RFQItems` being null when the collection wasn't eagerly loaded.
9. **CORS errors in browser** often mask a real 500 on the server. Read the server log.

---

## 9. Memory Log

> Append a new entry here after **every** completed task. Newest at the top.
> Format:
> ```
> ### YYYY-MM-DD — <short title>
> **Files:** path/a, path/b
> **Summary:** One paragraph: what you changed and why.
> **Follow-ups:** open questions / TODOs.
> ```

### 2026-06-18 — ILS Enhancements: Cert Multi-line & PDF Layout/Title Fix
**Files:** `client/app/pages/ils/items/[id].vue`, `client/app/components/IlsQuotePdfGenerator.vue`, `src/Procument.API/Controllers/PdfController.cs`
**Summary:** Converted the ILS Serial "Cert Text" field from a single-line input to a multi-line auto-growing `v-textarea` and updated the item table to support `white-space: pre-wrap`. Refactored the `IlsQuotePdfGenerator.vue` modal layout from a stacked top-bottom view into a side-by-side view (controls on the left, live PDF preview on the right) to match the main application's standard. Finally, fixed a bug where ILS Proforma Invoices always generated PDFs titled "QUOTATION"; added `DocTitle` to `QuotePdfRequest` on the backend, allowing the frontend to dynamically pass "PROFORMA INVOICE" and automatically adjust the footer text context.
**Follow-ups:** none.

### 2026-05-25 — Invoice Details Deposit Wallet Selection Box
**Files:**
- Backend: `src/Modules/Procument.Module.Sales/DTOs/InvoiceDTOs.cs`, `src/Modules/Procument.Module.Sales/Services/InvoiceService.cs`
- Frontend: `client/app/pages/invoices/[id].vue`
**Summary:** Added a "Deposit Wallet" dropdown selector inside the "Invoice Details" card on the Sales Order detail page. Exposed `DefaultDepositWalletId` in the `InvoiceResponse` DTO and mapped it from the `Invoice` entity in `InvoiceService.MapToResponse`. On the frontend, loaded the list of wallets belonging to the customer and displayed a standard Vuetify `v-select` dropdown displaying each wallet's custom name (with currency) when editing details. When saved, the frontend calls the existing `PATCH /invoices/{id}/default-wallet` endpoint to persist the selection.
**Follow-ups:** none.

### 2026-05-25 — Multi-Wallet System per Company Preset
**Files:**
- Backend: `src/Modules/Procument.Module.Sales/Entities/PaymentBox.cs`, `src/Modules/Procument.Module.Sales/Entities/Invoice.cs`, `src/Modules/Procument.Module.Sales/DTOs/PaymentBoxDTOs.cs`, `src/Modules/Procument.Module.Sales/DTOs/InvoiceDTOs.cs`, `src/Modules/Procument.Module.Sales/Services/IPaymentBoxService.cs`, `src/Procument.Shared/Services/IPaymentLedgerService.cs`, `src/Modules/Procument.Module.Sales/Services/PaymentBoxService.cs`, `src/Modules/Procument.Module.Sales/Controllers/PaymentBoxesController.cs`, `src/Modules/Procument.Module.Sales/Controllers/InvoicesController.cs`, `src/Modules/Procument.Module.Sales/Controllers/DocumentsController.cs`, `src/Modules/Procument.Module.Purchasing/DTOs/PurchaseOrderDTOs.cs`, `src/Modules/Procument.Module.Purchasing/Controllers/PurchaseOrdersController.cs`
- Migration: `AddWalletNameAndInvoiceDefaultWallet` (applied)
- Frontend: `client/app/pages/payment-control/index.vue`, `client/app/pages/quotes/[id]/create-invoice/index.vue`, `client/app/pages/payment/index.vue`
**Summary:** Implemented N-wallets per Company Preset. Added `Name` field to `PaymentBox` (EF migration applied). Added `DefaultDepositWalletId` FK on `Invoice` so each PI remembers which wallet its customer POPs deposit into. Updated `TryAutoDepositAsync` and `TryAutoWithdrawAsync` to accept an optional `explicitBoxId` override, falling back to preset-lookup when absent. Added new endpoints: `PATCH /payment-boxes/{id}/rename`, `GET /payment-boxes?presetId=`, `GET /payment-boxes/for-customer/{customerId}`, and `PATCH /invoices/{id}/default-wallet`. On PO acceptance, the `WalletId` travels from the frontend wallet picker through `UpdatePaymentApprovalRequest` to `TryAutoWithdrawAsync`. Frontend: wallet Name is displayed on cards with an inline rename button; the Add Wallet dialog now includes a Name field; a wallet picker dialog appears after PI creation (if >1 wallet) to set the default deposit wallet; and a wallet picker dialog appears before PO payment acceptance (if >1 wallet) to choose the deduction wallet.
**Follow-ups:** The payment-control/[id].vue (individual wallet detail page) still shows `companyPresetName` as the title — should be updated to show `box.name` once the name feature is in use. Consider adding wallet display to the payment/index.vue wallet transfer creation dialog.

### 2026-05-25 — Customer Code Fallback Excel Filter Alignment
**Files:**
- Backend: `src/Modules/Procument.Module.RFQ/Services/RFQService.cs`, `src/Modules/Procument.Module.RFQ/Controllers/RFQController.cs`, `src/Modules/Procument.Module.Purchasing/Services/ProcumentPageService.cs`, `src/Modules/Procument.Module.Purchasing/Controllers/ProcumentPageController.cs`, `src/Modules/Procument.Module.Sales/Services/QuoteService.cs`, `src/Modules/Procument.Module.Sales/Services/InvoiceService.cs`
- Frontend: `client/app/pages/rfqs/index.vue`
**Summary:** Resolved a critical cross-cutting UX issue where Customer Names were being leaked in the Excel-style column filters for Customer on the RFQs page, in violation of rule 6.3. Aligned the `cfCustomerOptionsPage` computed property in Nuxt to map to `customerCode` (falling back to `'-'`), completely hiding names. Re-engineered all backend query parsing and `customerSearch` filters across the RFQ, Purchasing, Sales, Invoices, and Quotes modules to support exact matching against both Customer Name and Customer Code, fully restoring complete data privacy for non-admin users.
**Follow-ups:** none.

### 2026-05-25 — Customer Catalog Enhancements (PI Terms, Company Type, Country, & Multiple Emails)
**Files:**
- Backend: `src/Modules/Procument.Module.Catalog/Entities/Customer.cs`, `src/Modules/Procument.Module.Catalog/Controllers/CatalogControllers.cs`, `src/Modules/Procument.Module.Sales/Services/InvoiceService.cs`
- Frontend: `client/app/pages/catalog/customers.vue`
**Summary:** Enhanced the Customer Catalog by adding columns for PITermsAndConditions, CompanyType, Country, and multiple email addresses. Designed PITermsAndConditions to automatically override the Proforma Invoice (PI) PDF terms. Implemented CompanyType as a colored category chip (Airline, MRO, Supplier) in the customers table, and modeled multiple email addresses cleanly using a serialized comma-separated string in the database and a premium combobox with closable chips on the edit/create form. Executed and successfully applied EF Core database migrations.
**Follow-ups:** none.

### 2026-05-25 — Persistent Column Visibility Toggle and Dynamic CSV Export for Total Project
**Files:** `client/app/pages/total-pn/index.vue`
**Summary:** Implemented persistent column visibility toggling on the Total Project (`/total-pn`) page, enabling users to hide/show any of the 36 columns interactively using a premium dropdown panel containing a search box and reset option. Column preferences are saved and loaded automatically using browser `localStorage` under a dedicated key (`total-pn-column-visibility`). Re-engineered the CSV exporter (`exportCsv`) to dynamically filter headers and map cell values so that only currently unhidden columns are present in the downloaded spreadsheet. Additionally, conducted a clean sweep of the file to fix all corrupted Unicode characters (mojibake) like `â€”`, `â€¦`, and `Â·`, replacing them with standard dashes, dots, and ellipses.
**Follow-ups:** none.

### 2026-05-25 — Multi-page Excel Column Dropdown Checkbox Filters expansion
**Files:**
- Backend: `src/Modules/Procument.Module.RFQ/Services/RFQService.cs`, `src/Modules/Procument.Module.RFQ/Controllers/RFQController.cs`, `src/Modules/Procument.Module.Purchasing/Services/ProcumentPageService.cs`, `src/Modules/Procument.Module.Purchasing/Controllers/ProcumentPageController.cs`, `src/Modules/Procument.Module.Sales/Services/QuoteService.cs`, `src/Modules/Procument.Module.Sales/Controllers/QuotesController.cs`, `src/Modules/Procument.Module.Sales/Services/IInvoiceService.cs`, `src/Modules/Procument.Module.Sales/Services/InvoiceService.cs`, `src/Modules/Procument.Module.Sales/Controllers/InvoicesController.cs`, `src/Modules/Procument.Module.Purchasing/Services/IProcurementService.cs`, `src/Modules/Procument.Module.Sales/Services/ProcurementService.cs`, `src/Modules/Procument.Module.Purchasing/Controllers/ProcurementsController.cs`
- Frontend: `client/app/pages/rfqs/index.vue`, `client/app/pages/procument/index.vue`, `client/app/pages/quotes/index.vue`, `client/app/pages/invoices/index.vue`, `client/app/pages/procurements/index.vue`
**Summary:** Expanded Excel-style column header dropdown checkbox multi-select filters across five key pages: `/RFQs` (added ID and Name filters with state persistence), `/procument` (added RFQ# and RFQ Name filters), `/quotes` (added Quote# filter), `/invoices` (added Sales Order# filter), and `/procurements` (added Condition, Proc Status, Supplier, and Assigned Users filters). Implemented matching SQL `IQueryable` array/list exact-match criteria in C# service layers and controller endpoints, added distinct list queries in backend filter-options endpoints, updated cascading computed options inside Vue templates, and successfully mapped array selections in page query parameters with saved filter state persistence.
**Follow-ups:** none.

### 2026-05-25 — RFQ Excel filter, dynamic cascading multi-select, and customer code fallback
**Files:** `src/Modules/Procument.Module.Sales/Controllers/QuotesController.cs`, `src/Modules/Procument.Module.Sales/Services/QuoteService.cs`, `client/app/pages/quotes/index.vue`, `client/app/pages/invoices/index.vue`, `client/app/pages/procurements/index.vue`
**Summary:** Added an Excel-style filter for the "RFQ Name" column on the `/quotes` list page. Updated the backend `QuotesController` and `QuoteService` to support receiving, parsing, and filtering by an array of RFQ names (including falling back to parsing the RFQ ID if the name is not custom). Redesigned the frontend column filtering options logic on the Quotes page to be dynamic and cascading using a custom non-self-filtering helper. Additionally, enforced a strict customer code fallback policy: if a customer code is missing, we display exactly `'-'` (instead of falling back to leaking the customer's full name) in all tables' customer filters (Quotes, Invoices, Procurements, and RFQs).
**Follow-ups:** none.

### 2026-05-17 — Restricted inventoryOnly nav items to Inventory role only
**Files:** `client/app/layouts/default.vue`
**Summary:** Modified the `navItems` filtering logic to hide `inventoryOnly` items from Admin and SuperAdmin users. These items are now strictly visible only to users with the `Inventory` role, satisfying the request to keep the `/shipping` page exclusive to inventory users.
**Follow-ups:** none.

### 2026-05-10 — Tab navigation fix (RFQ + RFQ Items) + Shipping Cost added to Procurement
**Files:**
- Frontend: `client/app/pages/rfqs/[id]/index.vue`, `client/app/pages/procument/index.vue`, `client/app/pages/procurements/[id].vue`, `client/app/pages/procurements/index.vue`
**Summary:** Fixed a UX issue where "Cost Price" and "Shipping Cost" fields were skipped during tab navigation on both the RFQ detail page (`/rfqs/[id]`) and the RFQ Items page (`/procument`). This was caused by a `v-if/v-else` pattern swapping `span` for `input`; added `tabindex="0"` and `@focus` to the spans to allow them to be focused via tabbing. Also added a "Shipping" column to the supplier quotes tables on the Procurement detail and Procurement items list pages (`/procurements`), as the field was supported by the backend but missing from the UI. Updated `addSupplierQuote` and `saveSupplierQuote` to handle `shippingCost`.
**Follow-ups:** none.

### 2026-04-22 — Base-3 Yuan price double-markup fix
**Files:** `client/app/components/InvoicePdfGenerator.vue`, `client/app/components/QuotePdfGenerator.vue`
**Summary:** When `customerBase === 3` and `customerCurrencyType === 'Both'`, the Yuan PDF was using `rate: 1.22 * 7`. The `1.22` markup is already baked into stored `totalAmount` / `unitPrice` / `totalPrice`, so multiplying again squared the factor (yielded ~72.93× instead of 7×). Changed to `rate: 7` on both. `FinalInvoicePdfGenerator.vue` already had the correct value.
**Follow-ups:** none.

### 2026-04-22 — Supplier docs upload 401 + menu for Bank Info
**Files:** `src/Modules/Procument.Module.Sales/Controllers/DocumentsController.cs`, `client/app/pages/purchase-orders/[id].vue`
**Summary:** Two separate bugs: (1) `DocumentsController` was restricted to `Admin,SuperAdmin,Expert` — added `Payment`. (2) PO page was reading `authStore.token` (undefined); fixed all 5 call sites to `authStore.user?.token`. Added upload-category menu (Supplier Invoice / Supplier Bank Info) and wired DP auto-generation + upload in `saveImport()`.
**Follow-ups:** none.

### 2026-04-22 — DP (Down Payment / DasturPardakht) PDF pipeline
**Files:** `src/Procument.API/Pdf/DpDocument.cs` (new), `src/Procument.API/Pdf/PdfRequestDTOs.cs`, `src/Procument.API/Controllers/PdfController.cs`, `src/Modules/Procument.Module.Sales/Controllers/DocumentsController.cs`, `client/app/pages/purchase-orders/[id].vue`, `client/app/pages/payment/index.vue`
**Summary:** New PDF type generated automatically when Import Details are saved on a PO. Contains items table (Part, Qty, PO Supplier, Quote Price, PO Price, PO Total) + supplier bank detail block. New `dp` filename category. Payment modal surfaces Supplier Bank Info files, DP files, and a read-only bank-detail card sourced from the PO's `import-detail`. Supplier-doc filter on PO page expanded to include the two new categories.
**Follow-ups:** consider exposing `CompanyPresetName` consistently to the DP document from the PO context.

### 2026-04-22 — PO Permissions (assignable POs, like RFQs)
**Files:**
- Backend: `src/Modules/Procument.Module.Purchasing/Services/PurchaseOrderService.cs`, `src/Modules/Procument.Module.Purchasing/Controllers/PurchaseOrdersController.cs`, `src/Modules/Procument.Module.Sales/Controllers/DocumentsController.cs`
- Frontend: `client/app/components/BulkPermissionManager.vue`, `client/app/pages/purchase-orders/index.vue`, `client/app/pages/purchase-orders/[id].vue`
**Summary:** POs can now be assigned to users via the existing generic `EntityPermission` table with `EntityName = "PO"` (same pattern as RFQ — no migration needed). `GET /api/purchase-orders` filters by permission for non-admins. `GET /api/purchase-orders/{id}`, `/enriched`, `/pdf-data`, and `/import-detail` (GET+PUT) return **403** if the caller is not admin and not permitted. `PurchaseOrderService.GetAllAsync` now takes `(PageQuery, long userId, bool isAdmin)` and `IPurchaseOrderService` exposes `UserCanAccessAsync(poId, userId, isAdmin)`. `DocumentsController.UploadSupplier` blocks non-admins from uploading any category other than `supplier_invoice`, `supplier_bank_info`, `dp`; `UploadPI` is now fully admin-only. Frontend: `BulkPermissionManager` learned the `'PO'` entity (uses `/purchase-orders?page=1&pageSize=500` + `poNumber` title); PO list page exposes an **Assign Users** button for admins and hides the Warehouse/Vendor/Customer/Edit tabs from non-admins. PO detail page keeps Admin Approval, Payment Approval, Item Trail and the header **PDF** button behind `isAdmin`; non-admin assigned users see only the four allowed sections (Line Items & Tracking, Supplier Documents incl. Our POP files read-only, Import Details fillable w/ Bank Details, and can upload Supplier Invoice + Supplier Bank Info via the existing upload menu).
**Follow-ups:** revisit whether `Expert`-role users should also be gated by per-PO permission for status changes (currently `[Authorize(Roles = "SuperAdmin,Admin,Expert")]` still allows any Expert to call `UpdateStatus`). Also consider mirroring the RFQ pattern of marking POs unread after assignment.

### 2026-04-23 — Nav menu opened to non-admins for POs; PO PDF preset is role-scoped
**Files:** `client/app/layouts/default.vue`, `client/app/components/PoPdfGenerator.vue`
**Summary:** Flipped the `Purchase Orders` nav entry to `adminOnly: false` so assigned non-admin users can navigate to the filtered list (back-end still enforces per-PO permissions). In `PoPdfGenerator.vue` the preset dropdown is now role-aware: **admins see every preset** (original behavior), **non-admins see only the preset whose `sortOrder === 105` plus `Custom`**. The PO PDF never tries to auto-match on `customerBase` any more — non-admins are pinned to the sortOrder=105 preset (or Custom if it doesn't exist), and admins default to Base-105 when the dialog first opens but may freely switch. All other PDF generators (`QuotePdfGenerator`, `InvoicePdfGenerator`, `FinalInvoicePdfGenerator`, `RfqPdfGenerator`) are unchanged. Note: the user also removed the `v-if="isAdmin"` guard from the PDF button in `purchase-orders/[id].vue` header, so non-admin assigned users can now open the PO PDF dialog.
**Follow-ups:** none.

### 2026-04-23 — Assigned Users card on PO detail page
**Files:** `client/app/pages/purchase-orders/[id].vue`
**Summary:** Added an admin-only "Assigned Users" card between the Admin Approval and Payment Approval sections on the PO detail page. Shows every user currently permissioned on this PO (loaded from `GET /api/permissions/PO/{id}`) with their username/email, permission chip (`View`/`Edit`), and the **assignment date** (`createdAt` formatted via `toLocaleDateString()`). Each row has a delete button that calls `POST /api/permissions/revoke`. A "Assign User" button opens a dialog with a user autocomplete (populated from `GET /api/users`, filtered to exclude already-assigned users) and a View/Edit permission selector; submit calls `POST /api/permissions/assign`. Loading of assignments and the user list is guarded by `isAdmin` inside the existing `onMounted`, so non-admins don't fetch either endpoint.
**Follow-ups:** none — reuses the generic `EntityPermission` table and existing permissions endpoints, no backend changes needed.

### 2026-04-23 — Assigned Users column on PO list page
**Files:** `client/app/pages/purchase-orders/index.vue`
**Summary:** Added an admin-only `Assigned Users` column to the Purchase Orders table. After `loadPurchaseOrders` accumulates the PO pages, it fans out a parallel `GET /permissions/PO/{id}` call for each PO (gated by `isAdmin`) and stashes the result on `po._assignedUsers`. The column renders each assignment as a small chip (green for `Edit`, blue for `View`) with the username/email; the chip `title` attribute shows permission + assignment date on hover. Non-admins don't see the column and don't fetch the data.
**Follow-ups:** the per-PO permission fetch is N parallel requests. If the PO list grows large this should move to a bulk endpoint (e.g. `GET /permissions/PO?ids=1,2,3`).

### 2026-04-23 — Proforma Invoice: editable Qty/UnitPrice with per-unit discount + RFQ Ex box
**Files:** `src/Modules/Procument.Module.Sales/DTOs/InvoiceDTOs.cs`, `src/Modules/Procument.Module.Sales/Services/InvoiceService.cs`, `client/app/pages/invoices/[id].vue`
**Summary:**
- **Editable Qty & Unit Price on line items.** `UpdateInvoiceItemDiscountRequest` now accepts optional `Qty` + `UnitPrice` (alongside the legacy `FinalPrice`). When either is provided, `InvoiceService.UpdateItemsAsync` recomputes `TotalPrice = Qty * UnitPrice` and derives `Discount = max(0, (originalUnitPrice - newUnitPrice) * newQty)`, where **original unit price comes from the linked `QuoteItem.UnitPrice`**. The invoice's `TotalAmount` is recomputed as the sum of final line prices. The old `FinalPrice`-only path still works for backwards compatibility.
- **`InvoiceItemResponse` exposes `OriginalUnitPrice`** (pulled from `QuoteItem.UnitPrice`) so the UI can show `orig $X` underneath the editable unit-price input when it's been marked down.
- **RFQ Ex box on the PI detail page.** `InvoiceResponse` got a new `RfqExType` field that walks `InvoiceItems → QuoteItem → RFQItem → RFQ.ExType` (navigation already `Include`d in `GetByIdAsync`). The Vue page renders a `StatCard` ("RFQ Ex") with the same color/label mapping used in the RFQ detail page (`0 = Ex Warehouse / success`, `1 = Ex Vendor / info`, `2 = Ex Customer / warning`).
- **UI.** The Line Items table on `invoices/[id].vue` now has editable Qty and Unit Price inputs; the Total cell is purely computed (`qty * unitPrice`); the per-unit discount vs `originalUnitPrice` is shown as a caption. A "Save Changes" button appears whenever any input changes and PATCHes `/invoices/{id}/items` with the new `{id, qty, unitPrice}` payload.
**Follow-ups:** the "Original Unit Price" uses the linked `QuoteItem` as the source of truth. If a QuoteItem is ever mutated after the invoice is created, the computed discount on the invoice would shift. If that becomes a concern, add an `OriginalUnitPrice` column directly to `InvoiceItem` and snapshot it at invoice-creation time.

### 2026-04-23 — RFQ detail: admins can edit Part Number description
**Files:** `client/app/pages/rfqs/[id]/index.vue`
**Summary:** The Description cell on the RFQ items grid is now an `<input>` for admins (SuperAdmin + Admin via `authStore.isAdmin`) and a read-only span for everyone else. `editableItems` keeps a snapshot `_origDescription` when loading; `saveAll` now also PUTs `/partnumbers/{id}` when the admin has changed `description` (previously the partnumber PUT only fired if `item.remark` was set, which meant description-only edits were silently dropped). No backend change required — the existing `PUT /partnumbers/{id}` already accepts `description`.
**Follow-ups:** none.

### 2026-04-28 — Add ShipToAddress to Company Presets
**Files:** `client/app/pages/company-presets.vue`, `client/app/components/PoPdfGenerator.vue`
**Summary:** Added the `ShipToAddress` field to the Company Presets management page. (1) Updated the UI to display the ship-to address on preset cards. (2) Added a new "Ship To Address" text field to the Create/Edit dialog and integrated it into the component state. (3) Updated the Custom HTML Template Editor to include `{{COMPANY_SHIP_TO}}` as a variable with corresponding mock data for live previews. (4) Modified `PoPdfGenerator.vue` to automatically populate the "SHIP TO" address field from the selected company preset, providing better defaults when generating Purchase Orders.
**Follow-ups:** none.

### 2026-04-27 — User Management Enhancements (SuperAdmin/Admin roles)
**Files:**
- Backend: `src/Modules/Procument.Module.Identity/DTOs/AuthDTOs.cs`, `src/Modules/Procument.Module.Identity/Services/AuthService.cs`, `src/Modules/Procument.Module.Identity/Controllers/AuthController.cs`
- Frontend: `client/app/pages/users.vue`, `client/app/layouts/default.vue`
**Summary:** Expanded user management capabilities. Backend now includes `UpdateUserRequest` and `AdminChangePasswordRequest` DTOs, with corresponding service methods and controller endpoints (`PUT /api/users/{id}` and `PATCH /api/users/{id}/password`). Frontend: Restricted the "Users" sidebar link to `SuperAdmin` only. The `users.vue` page now includes "Edit User" and "Change Password" dialogs. Admins can update user names, emails, roles, and reset passwords for any user. `SuperAdmin` retains the exclusive ability to create new users and toggle account activity status.
**Follow-ups:** none.

### 2026-04-27 — Prevent selecting Pending/Disabled suppliers in Quotes (Frontend-only)
**Files:** `client/app/pages/procurements/[id].vue`, `client/app/pages/rfqs/[id]/index.vue`, `client/app/pages/rfqs/[id]/create-quote/index.vue`
**Summary:** Implemented frontend restrictions to prevent the use of "Pending" or "Disabled" suppliers in quoting workflows. (1) In the Procurement detail page, the supplier autocomplete datalist now filters for `status === 'Approved'`. (2) In the RFQ detail page (procurement grid), the `saveAll` function validates all supplier names against their status; "Disabled" suppliers are blocked for everyone, and "Pending" suppliers are blocked for non-admins (Experts). (3) In the Create Quote page, the `isLineDisabled` logic was updated to block any procurement record whose supplier status is not "Approved". Backend changes were reverted as requested, keeping the validation logic entirely on the client side.
**Follow-ups:** none.

### 2026-04-26 — Task Manager with Kanban Board
**Files:**
- Backend: `src/Modules/Procument.Module.Tasks/` (new module), `src/Procument.Data/AppDbContext.cs`, `src/Procument.API/Program.cs`, `src/Procument.API/Procument.API.csproj`, `src/Procument.slnx`
- Frontend: `client/app/pages/tasks.vue` (new), `client/app/layouts/default.vue`
**Summary:** Implemented a new Task Manager module. Backend includes `TaskItem` entity, `TaskStatus` enum, `TaskService`, and `TasksController` with role-based access (admins create/delete, users move assigned tasks). Frontend features a 3-column Kanban board (Not Started, InProgress, Done) using Vuetify components and standard CSS. Added navigation link to sidebar.
**Follow-ups:** Run EF migrations to create the `Tasks` table.

### 2026-04-24 — Procurement: per-item visibility and permission filtering
**Files:** `src/Modules/Procument.Module.Purchasing/Services/IProcurementService.cs`, `src/Modules/Procument.Module.Sales/Services/ProcurementService.cs`, `src/Modules/Procument.Module.Purchasing/Controllers/ProcurementsController.cs`
**Summary:** Implemented granular item-level permissions for Procurements. Non-admin users who are assigned only to specific items within a Procurement (and lack header-level permission) now see only those items in the detail view (`GetByIdAsync`). The list view (`GetAllAsync`) correctly adjusts the `ItemCount` for these users. Added `UserCanAccessItemAsync` to `IProcurementService` and enforced it in `ProcurementsController` across all item-level endpoints (`UpdateItem`, `UpsertSupplierQuote`, `SelectSupplierQuote`, `DeleteSupplierQuote`), ensuring users can only modify items they are assigned to.
**Follow-ups:** none.

### 2026-04-23 — RFQ list: search by RFQ ID
**Files:** `src/Modules/Procument.Module.RFQ/Services/RFQService.cs`
**Summary:** The RFQs page search box previously matched only `RFQHeader.Name` and `Customer.Name`. When users typed an RFQ id number (e.g. `1234`) they got zero results even though the RFQ existed. `RFQService.GetAllAsync` now detects a numeric search term with `long.TryParse` and adds `r.Id == searchId` to the OR clause; non-numeric terms keep the old behavior. The `Name.Contains(s)` path is retained in the numeric branch too so that ids embedded inside a name (e.g. `RFQ-1234-X`) still match.
**Follow-ups:** none.

---

## 10. Quick Command Reference

```bash
# Backend
dotnet build src/Procument.API
dotnet ef migrations add <Name> --project src/Procument.Data --startup-project src/Procument.API
dotnet ef database update       --project src/Procument.Data --startup-project src/Procument.API
dotnet run --project src/Procument.API

# Frontend
cd client && npm install
cd client && npm run dev
cd client && npm run build
```

---

## 11. The "Checklist Before You Reply" (run every turn)

Before sending your response to the user, verify:

- [ ] I asked the right number of clarifying questions (1 for small, 2–3 for larger).
- [ ] I read the actual files I'm modifying (not assumed contents).
- [ ] My change respects existing patterns (auth token path, role names, CORS order, currency rules).
- [ ] I'm not introducing a `.md` file, emoji, or refactor the user didn't ask for.
- [ ] I will update §9 Memory Log after this task lands.
- [ ] If I'm unsure, I said so instead of bluffing.
