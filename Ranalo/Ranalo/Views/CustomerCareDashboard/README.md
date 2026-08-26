# Customer Care Dashboard

Branch: `feature/customer-care-dashboard` (created from `main`)

## Purpose
Support-agent facing dashboard for handling customer queries, disputes, and
day-to-day account servicing — narrower than the Admin Dashboard, read/action
access only on the accounts assigned/searched for.

## Audience / Role
Internal customer care / support staff.

## Suggested pages
- Customer search (by name, phone, ID, contract number) — reuse `CustomerController`
- Customer 360 view: profile, active contract(s), payment history, device status
- Payment lookup & manual reconciliation view (read-mostly, escalate to admin for
  write actions) — reference `PaymentsController`, `MpesaController`
- Contract details view (status, schedule, arrears) — reference `ContractController`
- Device / Knox status (locked/unlocked, last check-in) — reference `SumsungKnox`,
  `DevicesController`
- Enrolment status lookup — reference `EnrolmentsController`
- Ticket / complaint log (new — may need a lightweight `SupportTicket` model if one
  doesn't exist yet)
- Collections notes / follow-up log tied to `CollectionsController`

## DashLite components to reuse
- DashLite "user profile" / timeline card patterns for the Customer 360 view
- DashLite search + data-table components for the customer lookup page
- DashLite tabs component to split a customer record into Profile / Payments /
  Device / Contract / Notes tabs

## Suggested routing
- Controller: `CustomerCareController` (new)
- Route prefix: `/customer-care/...`

## Notes / open questions
- Decide read vs. write boundaries: should customer care be able to trigger refunds,
  device unlocks, or payment plan edits, or only view + escalate?
- Confirm if a ticketing system already exists elsewhere in the solution (e.g. in
  `Ranalo.Woocommece.Api`) before building a new one.
