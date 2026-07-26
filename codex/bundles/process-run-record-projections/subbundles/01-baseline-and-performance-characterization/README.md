# baseline-and-performance-characterization

## Status

- `Completed`

## Objective

- Establish reproducible logical-I/O and behavior baselines for historic process lists, workspace Runs/Graphs/Analytics, API reads, project nodes, observation/usage readers, and projection catch-up.

## Success Criteria

- Pass 1 and Pass 2 findings are tied to exact call/query paths.
- Focused characterization tests expose foreground catch-up, deep-hydration counts, duplicate history reads, and Agent Framework detail enumeration where practical.
- The intended post-change budgets and no-deep-hydration assertions are explicit.

## Covered Inputs

- R09, R14; N001, N002, N007, N009.

## Prerequisites

- Prepared-stage bundle validator passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application\ProcessRuntimeProjectionQueryService.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application\ProcessWorkspaceShellProjectionService.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application\ProcessRuntimeProjectionCatchupService.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Services\RuntimeIntegration\AgentFrameworkProcessExecutionObservationReader.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Services\RuntimeIntegration\AgentFrameworkProcessRuntimeUsageTelemetryReader.cs`

## UI Composition Contract

- N/A: characterization changes no rendered UI.

## Deliverables

- Recorded two-pass performance review.
- Instrumented fakes/interceptors or focused tests that count relevant store/detail calls.
- Baseline and target budgets in the execution report.

## Dependency Impact

- SB02-SB06 rely on this evidence to distinguish real I/O reduction from cosmetic async/LINQ edits.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical characterization foundation for all performance claims.

## Implementation Steps

1. Preserve existing behavior with focused tests before production edits.
2. Count foreground catch-up, state/assignment/detail loads, history reads, and execution-summary scans.
3. Record which operations share a scoped EF context and cannot be parallelized.
4. Define new normal-list budgets of zero canonical state/assignment/execution-detail loads per result row.
5. Run the focused tests and record evidence.

## Scope Exceptions

- No production benchmarking percentage is claimed from static or unit-test evidence.

## Do Not Do

- Do not optimize production code in this phase.
- Do not add timing-only flaky assertions.

## Acceptance Checklist

- [x] Exact bottlenecks and positive existing patterns are recorded.
- [x] Behavioral tests/fakes prove the critical read-amplification seams.
- [x] Post-change budgets are reviewable.
- [x] Performance disclaimer remains in the bundle.

## Proof Required

- Targeted `dotnet test` filters for added/updated characterization tests.
- Execution-report entry with call/query counts and affected paths.

## Browser Validation Logging

- N/A: no browser-visible change.

## Actual Proof And Progression

- Entry and closure gates: `Pass`.
- `analysis/01-current-state.md` records the initial and final two-pass reviews, 13-file scan counts, shared-`DbContext` concurrency decision, and baseline-versus-final logical-I/O budgets.
- `ProcessProjectionPipelineTests.Runtime_workspace_list_only_skips_detail_history_metrics_and_runtime_enrichment`, dashboard exact-key batch tests, and terminal-record no-rebuild tests are the dependent-flow proof.
- Progression decision: `Completed; SB02 evidence was trusted and the final scan found no sync-over-async, unsafe parallelism, or culture-sensitive identifier matching.`

## Behavioral Semantic Adequacy

- Raw note owned: `N001`, `N002`, `N007`, and `N009`: slow expanding history use, architectural improvement, sequential/deep-load analysis, and mandatory performance/architecture gates.
- Shipped behavior: `analysis/01-current-state.md` identifies the old foreground replay/deep-hydration paths, records why a shared scoped `DbContext` is not parallelized, and defines deterministic post-change call/query budgets.
- Source proof: `ProcessRuntimeProjectionQueryService`, `ProcessWorkspaceShellProjectionService`, `AgentFrameworkProcessExecutionObservationReader`, and `AgentFrameworkProcessRuntimeUsageTelemetryReader` are the measured production seams; the final review follows their record-backed replacements and remaining explicit deep-detail paths.
- Test proof: `ProcessProjectionPipelineTests.Runtime_workspace_list_only_skips_detail_history_metrics_and_runtime_enrichment`, `Shell_projection_does_not_rebuild_incomplete_terminal_record_from_deep_history`, dashboard exact-key batch tests, and historic-cost tests assert observable call/load behavior.
- Shallow-pass trap: a stopwatch-only improvement, cosmetic LINQ rewrite, or `Task.WhenAll` over one scoped EF context could appear faster while preserving the same deep I/O or adding unsafe concurrency.
- Adversarial negative proof: counting/throwing collaborators reject unexpected history, metrics, runtime-enrichment, deep-rebuild, observation, and telemetry calls on compact paths.
- Semantic positive proof: explicit historic selection, dashboard fallback, and cost aggregation still return durable run data while satisfying the zero-per-row canonical-detail budget.
- Anti-stub audit: proof executes production query services and their real contracts; no delay-based assertion, test-only production branch, placeholder result, or compiler-only claim supplies the performance conclusion.

## Progression Gate

- SB02 may start only when baseline tests pass and target budgets are explicit.

## Reopen Triggers

- A later consumer reveals an uncharacterized normal history path or a claimed optimization lacks call/query-count proof.

## Suggested Agent Prompt

```text
Implement SB01 only. Add deterministic characterization evidence without changing production behavior. Record exact call/query budgets and stop if a meaningful seam cannot be instrumented safely.
```
