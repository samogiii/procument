# Shipping — Customer Bases per User, and Customer PickUp

This replaces the Internal / External Shipping split (`SHIPPING_SPLIT.md`, removed). There is
again **one Shipping menu**. What each user sees in it depends on the **customer bases**
granted to them in Menu Access.

| Base | Ships with | Extra |
|---|---|---|
| **1** (and lines with no customer, e.g. Stock POs) | T-ID and SO# | — |
| **2 – 5** | no T-ID / SO# | **PickUp**: the customer collects the parts at the warehouse |

---

## 1. Access — Menu Access page

Five features, one per base:

| Feature | Label |
|---|---|
| `shippingBase1` | Shipping — Base 1 (T-ID / SO#) |
| `shippingBase2` … `shippingBase5` | Shipping — Base 2 … 5 (PickUp) |

- A user sees the **Shipping** menu only once at least one base is granted. This applies to
  **every role, SuperAdmin included** (no bypass). Typing a shipping URL without a base gives 404.
- Inside the menu, each page keeps its role rules (Shipping Control / Ready for SN# / Shipment
  still need `shippingMenu`, Inventory sees Warehouse Shippings + Shipping Control, Expert SYD
  keeps its route allowlist).
- **The API filters**, not just the page: these endpoints return only lines in the user's bases.

| Endpoint | Rule |
|---|---|
| `GET /api/shipping/track-numbers` | track's base is granted |
| `GET /api/shipping/ready-for-sn` | part's base is granted |
| `GET /api/shipment-notes` | **any** part of the note is in a granted base |
| `GET /api/warehouse-transfers` | **any** line of the transfer is in a granted base |
| `GET /api/po-items/total-order` | invoice customer's base is granted (in SQL, so paging stays right) |

The migration carries the old grants over: `internalShipping` → Base 1,
`externalShipping` → Bases 2–5, then removes the old rows.

## 2. T-ID / SO# — Base 1 only

- **Ready for SN#**: a Base column is shown. T-ID / SO# fields appear in Create SN# only when
  every selected part is Base 1. Base 1 and Base 2–5 parts cannot share an SN# (page blocks it,
  API rejects it, and the API stores no T-ID / SO# on a Base 2–5 note).
- **Shipment / Shipping Control**: the T-ID / SO# columns are shown only to users who have Base 1.
  On a Base 2–5 note the cells show "—", the info card shows Destination only, and the edit
  dialog becomes "Edit Destination". The By T-ID tab, Finished "By T-ID" view and the Expert T-ID
  tree are shown only to users with Base 1.
- **Add Track Number** to a note lists only tracks of the same kind (Base 1 vs Base 2–5).
- **Total Order**: SO# / TID columns only for users with Base 1, blank on Base 2–5 rows.

## 3. PickUp (Base 2–5)

The customer comes to the warehouse, hands over documents, and leaves with the parts.

1. **Ready for SN#** → select accepted parts → **Pick Up**. Allowed when every selected part is
   Base 2–5, from one warehouse and one customer.
2. Upload the customer's documents (at least one, any file type) and an optional note
   (e.g. who collected the parts) → **Finish Pick Up**.
3. The API (`POST /api/shipment-notes/pickup`, multipart) creates a shipment note:
   - `Type = "PickUp"`, number `PU-yyyy-###`, `Status = "Confirmed"` — finished at once,
     no packing / AWB / customs steps;
   - the parts' tracks move to **Delivered to Customer** and leave the Ready for SN# queue;
   - the documents are stored as `ShipmentNoteDocuments` under `Documents/ShipmentNotes/{id}/`.
4. It shows in **Shipping Control → Finished** (and on the Shipment page) with a teal PickUp
   chip and a "Customer Documents" card — click a document to preview / download it; Admin and
   SYD can add more files later (`POST /api/shipment-notes/{id}/documents`).
5. The part's shipping trace ends with a "Customer pickup" leg.

Who: same roles as creating an SN# (Admin, SuperAdmin, Expert), limited to their granted bases.

## 4. Files

**Backend**
- `Purchasing/Services/ShippingBaseAccess.cs` — **new**. Loads a user's bases; base rules.
- `Purchasing/Services/ShipmentNoteService.cs` — base filter, Base 1 / 2–5 mixing check,
  `CreatePickupAsync`, document upload / download.
- `Purchasing/Services/ShippingService.cs`, `WarehouseTransferService.cs` — base filter; PickUp trace leg.
- `Purchasing/Controllers/ShipmentNotesController.cs` — `pickup`, `documents` endpoints.
- `Purchasing/Controllers/ShippingController.cs`, `WarehouseTransfersController.cs`,
  `Sales/Controllers/TotalPNController.cs`, `Sales/Services/TotalPNService.cs` — pass / apply the bases.
- `Purchasing/Entities/ShipmentNoteDocument.cs` — **new**; `ShipmentNote.PickupNote`, `Documents`.
- `Identity/Controllers/AuthController.cs` — `shippingBase1…5` in `KnownFeatures`.
- Migration `AddShipmentNotePickup` — `ShipmentNoteDocuments` table, `ShipmentNotes.PickupNote`, grant carry-over.

**Frontend**
- `composables/useShippingBases.ts` — **new** (replaces `useShippingScope.ts`). Base rules.
- `components/ShipmentNoteDocuments.vue` — **new**. Documents card.
- `stores/auth.ts` (`shippingBases`, `shippingAccess`), `layouts/default.vue`,
  `middleware/auth.global.ts`, `pages/menu-access.vue`, `nuxt.config.ts` (scoped routes removed).
- `pages/shipping/ready-for-sn.vue` — Base column, mixing check, PickUp dialog.
- `pages/shipment-notes/index.vue`, `pages/total-shipping/index.vue` — per-note T-ID / SO#, PickUp display.
- `pages/shipping/index.vue`, `pages/shipping/track-numbers/[id].vue` — scope code removed.

## 5. After deploying

1. Apply the migration (`dotnet ef database update`) and restart the backend.
2. On **Menu Access**, check the **Shipping — Base 1…5** cards. Grants from the old Internal /
   External menus were carried over; add the bases for anyone else who needs Shipping —
   **including SuperAdmins and Inventory users**. Without a base nobody sees the Shipping menu.
