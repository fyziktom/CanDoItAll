# 02-history-analytics-data

## Status

- `Blocked`

## Objective

- Ensure historical process analytics include completed-run cost graph data after refresh and expose bounded, typed graph data scopes for selected process and selected process-run views.

## Success Criteria

- One-day live-process history includes money graph points for completed priced runs after refresh.
- Analytics can be queried for all runs of a selected process within a chosen range.
- Analytics can be queried for one selected process run only.
- Queries are range-bounded except for an explicit `all` selection.
- Cached input tokens are included in aggregate statistics where token categories are surfaced.

## Covered Inputs

- N001 / R007: improve process statistics and graph semantics.
- N005 / R004: restore price graph data after finished run refresh.
- N006 / R005 / R007 / R008: provide selected-process all-runs graph data.
- N007 / R006 / R007 / R008: provide selected-run graph data.
- N008 / R008: make graph loads explicitly scoped and bounded.

## Prerequisites

- SB01 progression gate has passed.
- Persisted metrics include accurate input, cached input, output, and cost values.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Integration/ProcessRuntimeReadQueryServiceTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ChartsWrapperTests.cs`

## Deliverables

- Historical live analytics include completed priced runs in money series for selected windows.
- Strongly typed graph range and graph scope models, if existing models are insufficient.
- Process-scoped graph data retrieval for all runs in a selected range.
- Run-scoped graph data retrieval for a selected process run.
- Focused tests for completed-run money series and scoped query boundaries.

## Dependency Impact

- SB03 consumes this data contract. If SB02 is over-broad or incomplete, UI tabs either load too much data or show empty/misleading graphs.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Inspect `ProcessObservationService` history construction for completed-run filtering and money series creation.
2. Fix the historical money series so completed priced runs in the selected window contribute after refresh.
3. Add typed range/scope support for process and run graph datasets only if the existing query model cannot express it.
4. Include cached input tokens in aggregate statistics where token usage is displayed.
5. Add tests for one-day completed-run money graph data.
6. Add tests for process-scope and run-scope query boundaries.
7. Capture command and browser proof, then update the execution report.

## Scope Exceptions

- Do not build the final process workspace UI in this subbundle.
- Do not add new provider pricing rules unless SB01 proof shows an existing rule is incorrect.

## Do Not Do

- Do not load unbounded run history unless the user explicitly selected `all`.
- Do not duplicate graph aggregation logic inside UI components.
- Do not hide unknown costs by inventing estimated prices.

## Acceptance Checklist

- A completed priced run inside the one-day window creates a non-empty money graph series after refresh.
- Process-scope query includes multiple runs for one process and excludes other processes.
- Run-scope query includes exactly the selected run.
- Range selection constrains data by the expected cutoff.
- Unknown-price metrics remain absent from actual-cost totals rather than silently estimated.

## Proof Required

- `proof/SB02/manifest.md` summarizing analytics invariants and query bounds.
- Transcript for targeted analytics tests under `proof/SB02/transcripts/`.
- Browser screenshot or clear blocker note for `/processes/live` one-day money chart under `proof/SB02/browser/`.
- Updated execution-report row and browser analytics row for SB02.

## Browser Validation Logging

- Target route: `/processes/live`.
- Required viewport: large desktop, follow-up narrower viewport only if UI controls or chart wrapping changed.
- Required actions: navigate, select one-day history, verify money chart container/series when seeded data exists.
- Evidence path: `proof/SB02/browser/live-history-money-desktop.png`.
- Review questions: is the price graph visible, is the selected range correct, and is there no chart overlap or empty chart when priced data exists?

## Progression Gate

- SB03 must not start until tests prove scoped analytics and completed-run money series behavior, and browser proof or a documented data-seeding blocker is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
