# Dealer Dashboard

Branch: `feature/dealer-dashboard` (created from `main`)

## Purpose
Portal for retail/device dealers/partners who sell devices on Ranalo Credit's
payment plans — enroll customers, track device stock, and see commission earned
on sales made through them.

## Audience / Role
External dealer/partner users (device shops), scoped to their own store's data only.

## Suggested pages
- Dealer overview (devices sold this month, active customer contracts, pending
  approvals, commission earned)
- New customer enrolment flow — reuse/extend `EnrolmentsController`
- Contract status list for customers enrolled by this dealer — reference
  `ContractController`, scoped by dealer ID
- Device stock / inventory tracking (if applicable) — reference `DevicesController`
- Commission statement / payout history — reference `CommissionsController`,
  scoped by dealer
- Support / contact admin (simple contact form or link out)

## DashLite components to reuse
- DashLite "wizard" / multi-step form component for the enrolment flow
- DashLite stat/KPI cards for the overview page
- DashLite data-table for stock and commission statement lists

## Suggested routing
- Controller: `DealerController` (new)
- Route prefix: `/dealer/...`
- Needs a `DealerId` claim/scope on login so all queries are filtered per dealer —
  check whether a `Dealer` entity already exists in `DataStore/DataModels`, or needs
  to be added alongside `Agents`/`Customer`.

## Notes / open questions
- Confirm data model: is "dealer" a distinct entity from "agent", or a type of
  agent? This affects whether Dealer and Agent dashboards can share a base
  controller/layout.
- Confirm dealer onboarding/auth flow (self-signup vs. admin-provisioned).
