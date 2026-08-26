# Customer Dashboard

Branch: `feature/customer-dashboard` (created from `main`)

## Purpose
End-customer self-service portal — the person who bought a device on a Ranalo
Credit payment plan. Lets them view their contract, pay, and check device status
without contacting support.

## Audience / Role
External customers (the lowest-trust, most public-facing role — treat all inputs
as untrusted and scope everything strictly to the logged-in customer's own data).

## Suggested pages
- My contract overview (device, plan term, balance remaining, next due date) —
  reference `ContractController`
- Payment schedule / history — reference `PaymentsController`
- Make a payment (M-Pesa) — reference `MpesaController`
- Device status (locked/unlocked, whether payments are current) — reference
  `SumsungKnox`, `DevicesController`
- My profile / contact details — reference `CustomerController`
- Help / support contact (links into Customer Care flow, e.g. a contact form or
  WhatsApp/phone link)

## DashLite components to reuse
- DashLite's simpler "user dashboard" demo layout (fewer nav items than the admin
  demo) — lighter sidebar, single-account context, no cross-user data ever shown
- DashLite payment/invoice card & timeline components for schedule + history
- DashLite progress bar for "plan completion" (e.g. 5 of 12 months paid)

## Suggested routing
- Controller: `CustomerDashboardController` (new) — distinct from the existing
  `CustomerController` (which appears to be admin-side customer management)
- Route prefix: `/my/...` or `/customer/...`

## Notes / open questions
- Confirm customer login/auth mechanism (phone/OTP vs. email/password) — this is
  the one dashboard with public self-registration, so auth flow needs care.
- Confirm whether M-Pesa payment initiation from this dashboard hits
  `MpesaController` directly or needs a dedicated public-safe endpoint.
- All queries here must be scoped to `CustomerId` from the authenticated session —
  no admin-style search/browse of other customers.
