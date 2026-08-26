# Admin Dashboard

Branch: `feature/admin-dashboard` (created from `main`)

## Purpose
Full-control back-office dashboard for Ranalo Credit staff/super-admins. Oversees all
other roles (customer care, dealers, agents, customers) and system-wide configuration.

## Audience / Role
Internal admin / super-admin users only. Should sit behind the strictest auth policy
in `LoginController` / existing cookie auth (see `CookieHelper.cs`).

## Suggested pages
- Overview / KPI landing page (active contracts, overdue payments, enrolments today,
  device stock, revenue collected vs. target)
- User & role management (admins, customer care, dealers, agents)
- Contracts management (list/search/filter, drill into `ContractController` data)
- Payments & collections oversight (aggregate view over `PaymentsController`,
  `CollectionsController`)
- Commissions oversight (aggregate view over `CommissionsController`)
- Device / Knox management (link into `SumsungKnox` services, `DevicesController`)
- Reports (wrap existing `ReportsController` / `Models/Reports`)
- System status / audit log (existing `SystemStatus` views as a starting point)

## DashLite components to reuse
- `wwwroot/assets/css/dashlite.css` + `theme.css` (already referenced by the app)
- Sidebar nav + topbar layout from DashLite's admin demo (nk-sidebar / nk-header)
- DashLite card, data-table, and chart (nk-chart) partials for KPI widgets
- Reuse existing `_Layout` if present under `Views/Shared`; extend rather than fork

## Suggested routing
- Controller: `AdminController` (new) or split into `AdminHomeController`,
  `AdminUsersController` etc. if it grows large
- Route prefix: `/admin/...`

## Notes / open questions
- Confirm whether "Agents" and "Customer" views already under `Views/Agents` and
  `Views/Customer` are admin-facing management screens (they look like it) — if so,
  this Admin Dashboard should link to them rather than duplicate them.
- Confirm role/permission model (single admin role vs. granular permissions).
