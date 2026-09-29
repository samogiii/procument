# Shipping Split — Internal (Base 1) / External (Base 2+)

The single **Shipping** menu is split into two menus, based on the **customer base** of each part:

| Menu | Shows | SO# / T-ID |
|---|---|---|
| **Internal Shipping** | Customer Base **1**, plus parts with no customer at all (e.g. Stock POs) | Shown as before |
| **External Shipping** | Customer Base **2 and above** | Hidden everywhere |

This is a **view-only filter**. There is **no database change**: no new tables, no new columns, no migrations. The only backend change is an extra read-only `customerBase` value in some API responses.

---

## 1. Menus

Each menu has the same four pages as the old Shipping menu:

| Page | Internal URL | External URL | Original page it reuses |
|---|---|---|---|
| Warehouse Shippings | `/internal-shipping` | `/external-shipping` | `/shipping` |
| Shipping Control | `/internal-shipping/control` | `/external-shipping/control` | `/total-shipping` |
| Ready for SN# | `/internal-shipping/ready-for-sn` | `/external-shipping/ready-for-sn` | `/shipping/ready-for-sn` |
| Shipment | `/internal-shipping/shipment` | `/external-shipping/shipment` | `/shipment-notes` |
| Track detail | `/internal-shipping/track-numbers/:id` | `/external-shipping/track-numbers/:id` | `/shipping/track-numbers/:id` |

- The pages are **not copied**. Each existing page file is registered as two extra routes in `client/nuxt.config.ts` (`pages:extend` hook). The URL prefix decides which bases the page shows. These are real routes, not aliases: Vue Router treats two aliases of one page as the same location and drops the navigation between Internal and External.
- The old Shipping group is removed from the menu. So are the two extra "Shipping" / "Shipping Control" items that Inventory users had.
- The **old URLs** (`/shipping`, `/total-shipping`, …) still work and show **everything**. Links from notifications and the dashboard therefore don't break, but these URLs no longer have a menu entry.

## 2. Access management (Menu Access page)

Two new features appear on the **Menu Access** page:

- `internalShipping` — "Internal Shipping (Base 1)"
- `externalShipping` — "External Shipping (Base 2+)"

Rules:

- A user sees a menu **only if they are granted it in Menu Access**. This applies to **every role, SuperAdmin included** (no bypass).
- Typing the URL without the grant gives **404 Not Found**.
- Inside a granted menu, each page keeps the **same role rules as the original page**. For example:
  - Shipping Control / Ready for SN# / Shipment still require the `shippingMenu` permission.
  - Inventory users still see only Warehouse Shippings and Shipping Control.
  - Experts still follow their route allowlist (SYD keeps shipping access).
- An Inventory user landing on `/` or `/dashboard` goes to the first shipping menu they are granted, or to `/shipping` if they have neither.

## 3. What each page does in the split

### Warehouse Shippings
- Shows only tracks whose customer base belongs to the menu.
- **Tracks that have been received in the warehouse are hidden.** A track is hidden once its status is *Received in Warehouse* or any later status. This applies to both menus and to the old `/shipping` URL.
- Clicking a row opens the track detail inside the same menu.

### Ready for SN#
- Shows only accepted parts in the menu's bases.
- **External:** the T-ID and SO# fields are hidden in the Create SN# dialog and are saved as empty.
- After a new SN# is created, the page moves to the Shipping Control page of the same menu.

### Shipment (SN#) and Shipping Control
- An SN# is shown when **any** of its parts belongs to the menu's bases.
- The Total Order tab shows only rows in the menu's bases.
- The Transfers tab shows a transfer when any of its lines belongs to the menu's bases.
- The "Add Track Number" dialog lists only tracks in the menu's bases.
- **External only:**
  - The T-ID and SO# columns, the T-ID / SO# info cards and the T-ID / SO# edit fields are hidden. The dialog becomes "Edit Destination".
  - The **By T-ID** tab and the Finished "By T-ID" view are hidden.
  - The expert T-ID tree is replaced by the normal table.
  - Total Order hides the SO# and TID columns.
- Saved column filters are kept separately for each menu.

## 4. How the customer base is found (backend, read-only)

A `CustomerBase` (int?) property was added to these API responses:

| Response | Endpoint |
|---|---|
| `ShippingTrackResponse` | `GET /api/shipping/track-numbers`, track review |
| `ReadyForSnItemResponse` | `GET /api/shipping/ready-for-sn` |
| `ShipmentNoteTrackResponse` | `GET /api/shipment-notes`, `GET /api/shipment-notes/{id}` |
| `WarehouseTransferItemResponse` | `GET /api/warehouse-transfers` |
| `TotalPNRowResponse` | `GET /api/po-items/total-order` (read from the invoice's customer that is already loaded) |

`ShippingCustomerBases.LoadAsync` finds the base for each PO line:

1. PO line → the procurement item it was cloned from → its RFQ. If that link is missing, it falls back to the older link: PO line → ProcumentRecord → RFQ item → RFQ.
2. RFQ → Customer → `Base`.
3. No customer (e.g. Stock PO) → `null`, which counts as Internal.

**Performance note:** the first version used one query that joined 8 tables at once. It timed out on the full data. It was replaced by 4 small primary-key lookups (PO lines → procurement items / supplier quotes → RFQs → customer base). Each lookup sends its whole id list as a single JSON parameter (`EF.Parameter` → `OPENJSON`) instead of one parameter per id.

The list queries for tracks, shipment notes, warehouse transfers, and Total Order now split collection includes into separate SQL queries to avoid multiplying rows across collections. Ready for SN# checks assigned tracks with `NOT EXISTS` in SQL rather than downloading every assigned track ID first.

## 5. Base rules (one place to change)

In `client/app/composables/useShippingScope.ts`:

- Internal = base `1` or no base.
- External = any base `2` or higher.

## 6. Files changed

**Frontend**
- `client/nuxt.config.ts` — `pages:extend` hook that adds the `/internal-shipping/…` and `/external-shipping/…` routes.
- `client/app/composables/useShippingScope.ts` — **new**. Holds the scope names, URL prefixes, base rules, path mapping and `useShippingScope()`.
- `client/app/layouts/default.vue` — adds the two shipping menu groups and their visibility rules.
- `client/app/stores/auth.ts` — adds the `internalShipping` / `externalShipping` getters (no SuperAdmin bypass).
- `client/app/middleware/auth.global.ts` — 404s a shipping URL when the user lacks the grant; applies the role checks using the original page path.
- `client/app/pages/menu-access.vue` — labels and icons for the two features.
- `client/app/pages/shipping/index.vue` — base filter, hides received tracks.
- `client/app/pages/shipping/ready-for-sn.vue` — base filter, hides T-ID/SO# for External.
- `client/app/pages/shipping/track-numbers/[id].vue` — page key only (routes come from `nuxt.config.ts`).
- `client/app/pages/shipment-notes/index.vue` — base filter, hides T-ID/SO# for External.
- `client/app/pages/total-shipping/index.vue` — base filter on all tabs, hides T-ID/SO# for External.

**Backend**
- `src/Modules/Procument.Module.Purchasing/Services/ShippingCustomerBases.cs` — **new**. The read-only base lookup.
- `src/Modules/Procument.Module.Purchasing/Services/ShippingService.cs` — fills `CustomerBase` for tracks and Ready-for-SN items.
- `src/Modules/Procument.Module.Purchasing/Services/ShipmentNoteService.cs` — fills `CustomerBase` for SN# tracks.
- `src/Modules/Procument.Module.Purchasing/Services/WarehouseTransferService.cs` — fills `CustomerBase` for transfer lines.
- `src/Modules/Procument.Module.Sales/Services/TotalPNService.cs` — `CustomerBase` on Total Order rows.
- `src/Modules/Procument.Module.Purchasing/DTOs/WarehouseDTOs.cs`, `WarehouseTransferDTOs.cs`, `PurchaseOrderDTOs.cs` — the new `CustomerBase` properties.
- `src/Modules/Procument.Module.Identity/Controllers/AuthController.cs` — adds `internalShipping` and `externalShipping` to `KnownFeatures` so Menu Access can grant them.

## 7. After deploying

1. Restart the backend.
2. On **Menu Access**, grant **Internal Shipping** and/or **External Shipping** to each user who needs them, **including SuperAdmins**. Until then, nobody sees either menu.
