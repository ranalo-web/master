# Agent Dashboard

Branch: `feature/agent-dashboard` (created from `main`)

## Purpose
Self-service portal for Ranalo Credit sales/collections agents — their leads,
enrolments, commissions, and collections/follow-up work, scoped to themselves.

## Audience / Role
Internal or field sales/collections agents (not admins).

## Suggested pages
- Agent overview (my enrolments this month, my commission earned, my pending
  collections/follow-ups, target vs. actual)
- My customers / contracts list — scoped view over `ContractController` /
  `CustomerController` filtered by agent ID
- New enrolment flow — reuse/extend `EnrolmentsController`
- Collections / follow-up queue — reference `CollectionsController`, scoped to
  accounts assigned to this agent
- Commission statement — reference `CommissionsController`, scoped by agent
- Approvals status (for enrolments pending approver sign-off) — reference
  `ApproverController`

## DashLite components to reuse
- Existing `Views/Agents` folder likely already has admin-facing agent management
  screens — check those first for existing markup/controller patterns to extend
  rather than duplicate, then rebuild as agent-self-service views under this branch
- DashLite KPI/stat cards for overview, data-table + filters for lists, wizard for
  enrolment flow (shared pattern with Dealer Dashboard)

## Suggested routing
- Controller: `AgentDashboardController` (new) — distinct from the existing
  `AgentsController` (which appears to be admin CRUD over agent records)
- Route prefix: `/agent/...`

## Notes / open questions
- Clarify relationship to the existing `AgentsController`/`Views/Agents`: is that
  the admin's "manage agents" screen, while this new dashboard is what an agent
  sees when they log in themselves? Recommend keeping them separate.
- Confirm agent auth/scoping (claim-based `AgentId` on login).
