# Admin Dashboard — template branch

Branch: `admin-dashboard-template` (created from `main`, off the tip of the
already-merged `feature/admin-dashboard` work).

## Status

This branch is a **holding point**, not the final design. It exists so this
round of layout/UX work isn't lost while the original branch(es) that had
the agreed profitability metrics designed on them are located and merged
in. Once those are found, pull the profitability section into this branch
(or this work into that one) rather than redoing it.

Everything below is still **placeholder/mock data** in
`AdminDashboardController.cs` / `DevPreviewController.cs` — wiring to real
services (`IApplicationReportService` and friends) is a separate follow-up
step, same as it was on the original branch.

Preview without logging in: `GET /dev-preview/admin-dashboard-live`
(Development only, see `DevPreviewController`).

The original scope notes for this dashboard (suggested pages, routing,
audience) are still in git history at commit `ce3f50a` if useful.

## What's implemented here

- **Top KPI cards** (Revenue, Total Accounts, Paying vs Non-Paying, Total
  Arrears): amount + period-over-period trend on one row, plus a
  target/ratio figure and a secondary metric sharing a second row.
- **Revenue & Accounts Growth**: real Chart.js dual-axis line chart
  (replaced a hand-drawn SVG that only ever plotted revenue).
- **Portfolio Composition**: Chart.js doughnut + matching legend (counts
  and %), Collection Rate and Portfolio at Risk (PAR30) with trend
  indicators.
- **Non-Payers / Slow Payers / Good Paying Customers / Dealer Performance /
  Agent Performance**: all five are real, consistent tables now (were ad
  hoc card lists for the watchlists). Every column is sortable
  (numeric-aware), and every table paginates — 5 rows by default, "Show N
  more" keeps adding pages, "Show less" collapses back to 5 at any point,
  and sorting preserves however many rows are currently expanded.

## What's still missing

- **Profitability metrics** — Net Profit, margin, commissions paid vs
  revenue, bad debt/write-offs, etc. Not built here; searched this repo's
  full git history (all branches, all commits) and found nothing beyond
  the original scope README's one-line mention of "revenue collected vs.
  target." The user is checking for a separate branch with these designed
  before we build them from scratch.

## Notes for whoever picks this up

- This DashLite CSS bundle (`wwwroot/assets/css/dashlite.css`) has several
  **duplicate, conflicting class definitions** that cost real debugging
  time — check for these before assuming a Bootstrap utility class does
  what you expect:
  - `.gap-1` / `.gap-2` etc. are defined twice: once as the real
    Bootstrap flex-gap utility, once as an unrelated legacy "spacer div"
    class that sets a hardcoded `height`. Avoid `gap-*` classes here;
    use inline `style="gap:...px"` instead.
  - `.tb-tnx-item` / `.tb-tnx-head` (the theme's transaction-table
    classes) force `display:flex; flex-wrap:wrap` below 768px, which
    silently turns any table using them into a stacked, non-scrolling
    mess on mobile. Plain `<table class="table">` inside
    `.table-responsive` does not have this problem.
  - `.change.up` / `.change.down` hardcode `color` with `!important`
    (up = green, down = red) regardless of any `text-success` /
    `text-danger` class also present. Fine when "up" is genuinely good;
    for metrics where a decrease is the good outcome (non-paying
    accounts, arrears), don't use the `.change` class — set the color
    directly instead.
