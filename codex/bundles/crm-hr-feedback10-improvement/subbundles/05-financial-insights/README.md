# SB05 Financial Insights

## Status

- `Completed`

## Objective

- Add a task-first Financials tab that derives defensible sold opportunity metrics while representing unavailable purchase and invoice data honestly.

## Success Criteria

- Financials appears immediately after Overview for the selected account.
- Sold totals and month/year series use the first UTC transition to `Won`, grouped by currency.
- Won records missing amount or Won history are counted as incomplete rather than treated as zero or dated by fallback.
- Bought and overdue invoices use typed `Unavailable`; the sold/bought donut is unavailable while bought data is unavailable.
- Available chart series render through `CdaChart`; empty, incomplete, error, and unavailable states remain distinguishable.

## Covered Inputs

- `N009`; `R011`, `R013`, `R014`.

## Prerequisites

- SB04 and `CP-04` passed.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Models/CrmHrBusinessModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowOverviewPanel.razor`
- `repo://tests/Components/CanDoItAll.Tests.Components/ChartsWrapperTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/OpportunityConversionIntegrationTests.cs`
- `bundle://design/ui-proposals/07-financials-tab.png`

## UI Composition Contract

- Primary surface: Financials tab with a compact metric strip followed by month/year revenue bars.
- Stats treatment: per-currency sold cards; bought/overdue explicitly labelled unavailable; incomplete-data count visible.
- List/editor organization: read-only projection, no editor; chart and metric sections use existing BaseLib/Charts wrappers.
- Textarea/dialog sizing: N/A.
- First viewport: metric strip, period selector, and the first chart are useful at `1800x1100`; secondary distribution/status follows.
- Scroll owner: existing CRM detail pane; charts do not create nested scrolling.

## Deliverables

- Typed financial availability/result contracts and cohesive query service.
- Currency-separated sold aggregation and UTC month/year bucket projection.
- Financials component/tab using `CdaChart`.
- Tests for available, empty, unavailable, incomplete, mixed-currency, and error states.

## Dependency Impact

- SB06 final hardening and closure depend on truthful aggregate semantics and browser rendering.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-05`.

## C# Architecture Impact

- Adds a dedicated read projection rather than placing financial calculations in the CRM page or monolithic service.

## Boundary Ownership

- CRM/HR query service owns aggregation; component maps typed results to presentation; chart package owns rendering.

## Dependency Direction

- No new finance/invoice module dependency is invented; CRM/HR consumes Infrastructure and Charts only.

## Pattern Decision

- Typed availability discriminant plus query/projection service; no numeric sentinel values.

## Testability Contract

- Aggregation is directly testable with seeded opportunity/history records and the component is testable with supplied typed snapshots.

## Partial Class Policy

- No new service/page partial or nested projection class.

## Architecture Proof Required

- Direct projection/component tests, mixed-currency and missing-history negatives, old-page no-calculation assertion, package/setup check, build, rendered-chart browser assertion, no-new-partial audit.

## Implementation Steps

1. Add typed availability and financial snapshot contracts.
2. Implement per-account sold query using UTC Won-stage history and currency-separated aggregation.
3. Count incomplete won records without fallback dates or zero values.
4. Add Financials tab/component and CdaChart package/import.
5. Add unit/integration/component tests and rendered SVG/series browser proof.
6. Run `CP-05` and record unavailable-data decisions.

## Scope Exceptions

- Purchase records, invoices, currency conversion, forecasting, and accounting exports remain unavailable/out of scope.

## Do Not Do

- Do not infer bought data, render unavailable as zero, sum currencies, date sales by expected close/updated timestamp, or display a misleading 100%-sold donut.

## Acceptance Checklist

- [x] Financials tab placement is correct.
- [x] Sold truth is per currency and Won-transition dated.
- [x] Incomplete records are visible.
- [x] Bought/invoices/donut are explicitly unavailable.
- [x] Month/year charts render only defensible series.

## Execution Evidence

- Shipped behavior: `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmFinancialSnapshotQueryService.cs` owns the typed read projection and `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/CrmFinancialsPanel.razor` maps it to compact metrics, period controls, charts, incomplete counts, and typed unavailable states.
- Semantic positive proof: `Snapshot_uses_first_won_transition_groups_currencies_and_marks_incomplete_records` in `repo://tests/Integration/CanDoItAll.Tests.Integration/CrmFinancialSnapshotQueryIntegrationTests.cs` verifies ordered UTC recognition buckets, first-Won semantics, currency groups, and incomplete records.
- Adversarial negative proof: the integration test includes Won records without valid recognition inputs and mixed currencies; `repo://tests/Components/CanDoItAll.Tests.Components/CrmFinancialsPanelTests.cs` distinguishes unavailable purchase/invoice sources and renders retryable errors without leaking exception details.
- Integrity proof: subsequent commercial-value edits do not rewrite recognized value, covered by `repo://tests/Integration/CanDoItAll.Tests.Integration/OpportunityIntegrityIntegrationTests.cs`.
- Browser proof: `repo://output/playwright/crm-hr-feedback10/final-financials-1800x1100.png`.
- Progression decision: `CP-05 passed`; no fabricated purchase, invoice, conversion, or doughnut data was introduced.

## Proof Required

- Raw note owned: `N009`.
- Shallow-pass trap: decorative charts backed by hard-coded/updated-date/mixed-currency data.
- Adversarial negative proof: mixed USD/EUR, Won without amount, Won without transition, non-Won amount, and no purchase/invoice source.
- Semantic positive proof: seeded won transitions render ordered month/year per-currency bars.
- Anti-stub audit: no fake series, sentinel zero, fallback timestamp, TODO provider, or UI-side aggregation.

## Browser Validation Logging

- Route: `/crm-hr/crm?accountId=<seeded-account>`, Financials tab.
- Viewport: `1800x1100`.
- Actions: open Financials, switch month/year, inspect metric availability, assert nonblank chart SVG/series and unavailable text.
- Screenshots: `bundle://evidence/browser/SB05/financials-month.png`, `bundle://evidence/browser/SB05/financials-year.png`.
- Review: readable currency labels, chart hierarchy, no misleading donut, useful first viewport, no overlap/clipping, existing detail-pane scroll.

## Progression Gate

- SB06 starts only after `CP-05`, mixed-currency/missing-history tests, and real chart browser proof pass.

## Reopen Triggers

- Reopen for currency mixing, fallback dates, unavailable-as-zero, fake distribution, UI-owned aggregation, blank charts, or tab layout regression.

## Suggested Agent Prompt

```text
Implement SB05 only. Add the typed financial projection and Financials tab, derive sold values only from valid Won transitions per currency, expose purchase/invoice gaps as unavailable, prove real chart rendering, update CP-05/report, and stop if any metric requires fabricated data.
```
