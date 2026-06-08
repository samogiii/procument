# Inventory / Warehouse / Shipping Feature — Progress Handoff

## Status: ✅ FULLY COMPLETE (Backend + Frontend)

All backend and frontend work is done. The only remaining user action is to run the EF Core migration.

---

## Migration Command (user must run)
```bash
dotnet ef migrations add AddInventoryWarehouseShipping --project src/Procument.Data --startup-project src/Procument.API
dotnet ef database update --project src/Procument.Data --startup-project src/Procument.API
```

---

## What Was Built

### New Role
- `Inventory` role added to `UserRoles` in `src/Modules/Procument.Module.Identity/Entities/User.cs`
- Inventory users are scoped to their assigned Warehouses
- Auth middleware restricts them to `/shipping`, `/shipment-notes`, `/dashboard`
- Landing page redirect: `/` and `/dashboard` → `/shipping`

### New Database Entities (7 new tables)
| Entity | File |
|--------|------|
| `Warehouse` | `src/Modules/Procument.Module.Purchasing/Entities/Warehouse.cs` |
| `UserWarehouse` | `src/Modules/Procument.Module.Purchasing/Entities/UserWarehouse.cs` |
| `CompanyPresetWarehouse` | `src/Modules/Procument.Module.Purchasing/Entities/CompanyPresetWarehouse.cs` |
| `TrackNumberItem` | `src/Modules/Procument.Module.Purchasing/Entities/TrackNumberItem.cs` |
| `TrackNumberDocument` | `src/Modules/Procument.Module.Purchasing/Entities/TrackNumberDocument.cs` |
| `ShipmentNote` | `src/Modules/Procument.Module.Purchasing/Entities/ShipmentNote.cs` |
| `ShipmentNoteTrackNumber` | `src/Modules/Procument.Module.Purchasing/Entities/ShipmentNoteTrackNumber.cs` |

### Modified Existing
- `POItemTrackNumber.cs` — Added `WarehouseId`, `Status`, nav collections
- `AppDbContext.cs` — 7 new DbSets + full EF Core model config
- `PurchaseOrderService.cs` — Added "Issue" to allowed statuses

### Backend Services
| Service | File |
|---------|------|
| `IWarehouseService` + `WarehouseService` | `src/.../Services/WarehouseService.cs` |
| `IShippingService` + `ShippingService` | `src/.../Services/ShippingService.cs` |
| `IShipmentNoteService` + `ShipmentNoteService` | `src/.../Services/ShipmentNoteService.cs` |

All registered in `PurchasingModule.cs`.

### Backend Controllers
| Controller | Route |
|-----------|-------|
| `WarehousesController` | `/api/warehouses` |
| `ShippingController` | `/api/shipping` |
| `ShipmentNotesController` | `/api/shipment-notes` |

Modified: `CompanyPresetsController`, `PurchaseOrdersController`, `PdfController`

### File Storage Paths
```
Documents/TrackNumbers/{trackId}/docs/{fileName}           ← track-level docs
Documents/TrackNumbers/{trackId}/parts/{poItemId}/{fileName}  ← part-level docs
Documents/ShipmentNotes/{snId}/{fileName}                  ← SN# PDF
```

### Frontend Pages
| Page | File |
|------|------|
| Warehouses | `client/app/pages/warehouses.vue` |
| Total Shipping | `client/app/pages/total-shipping/index.vue` |
| Shipping (Inventory) | `client/app/pages/shipping/index.vue` |
| Ready for SN# | `client/app/pages/shipping/ready-for-sn.vue` |
| Shipment Notes | `client/app/pages/shipment-notes/index.vue` |

### Modified Frontend Files
- `client/app/layouts/default.vue` — New nav items + `inventoryOnly` filter
- `client/app/middleware/auth.global.ts` — Inventory role path guard
- `client/app/stores/auth.ts` — `isInventory` getter
- `client/app/pages/company-presets.vue` — Warehouse multi-select in dialog
- `client/app/pages/purchase-orders/[id].vue` — Warehouse autocomplete in Add Track dialog
- `client/app/components/PoPdfGenerator.vue` — Ship To Warehouse picker

---

## Workflow
1. Admin creates Warehouse → assigns Inventory users → links to Company Preset
2. PO PDF: "Ship To Warehouse" picker auto-fills SHIP TO block
3. Track Number added to PO item: select Destination Warehouse
4. Admin sees all tracks on `/total-shipping` (expandable rows with part entries)
5. Inventory user `/shipping`: sees their warehouse tracks, enters qty/availability, uploads docs
6. Qty mismatch → visual warning. Part unavailable → Reject Track → PO becomes "Issue"
7. Admin/Expert reviews parts (Accept/Reject) — via `POST /api/shipping/track-numbers/{id}/items/{itemId}/review`
8. Accepted → `/shipping/ready-for-sn` — Admin selects same-warehouse parts → Create SN#
9. SN# has TId, AWB Number, PDF upload, auto-generated SNNumber (SN-{year}-{seq})
10. Inventory sees SN# on `/shipment-notes`, can add more track numbers

---

## Key API Endpoints
```
# Warehouses
GET/POST/PUT/DELETE  /api/warehouses
GET/POST/DELETE      /api/warehouses/{id}/users/{userId}
GET/POST/DELETE      /api/company-presets/{id}/warehouses/{warehouseId}

# Track Number Summary (admin)
GET  /api/purchase-orders/track-numbers/summary?page=&pageSize=&warehouseId=&status=&search=

# Shipping (Inventory + Admin)
GET  /api/shipping/track-numbers
POST /api/shipping/track-numbers/{id}/items
PUT  /api/shipping/track-numbers/{id}/items/{itemId}
POST /api/shipping/track-numbers/{id}/reject
POST /api/shipping/track-numbers/{id}/documents
POST /api/shipping/track-numbers/{id}/parts/{poItemId}/documents
GET  /api/shipping/track-numbers/{id}/documents
GET  /api/shipping/documents/{docId}/file
DEL  /api/shipping/documents/{docId}
POST /api/shipping/track-numbers/{id}/items/{itemId}/review  (Admin/Expert)
GET  /api/shipping/ready-for-sn?warehouseId=

# Shipment Notes
GET  /api/shipment-notes?warehouseId=
POST /api/shipment-notes
GET  /api/shipment-notes/{id}
PUT  /api/shipment-notes/{id}
POST /api/shipment-notes/{id}/upload-pdf
GET  /api/shipment-notes/{id}/pdf-file
POST /api/shipment-notes/{id}/confirm
POST /api/shipment-notes/{id}/track-numbers/{trackId}
DEL  /api/shipment-notes/{id}/track-numbers/{trackId}

# PDF
POST /api/pdf/po?warehouseId=N   (optional warehouseId auto-fills Ship To from warehouse)
```
