# performance-architecture-and-browser-closure

## Status

- `Completed`
- Checkpoint: `AC03 Approved`

## Objective

- Prove the completed dashboard is bounded, architecturally clean, buildable, behaviorally correct, and visually usable at the target desktop viewport.

## Success Criteria

- The requested performance scans and manual hot-path review find no unresolved dashboard bottleneck, sync-over-async, unbounded source call, overlapping refresh, or avoidable enrichment.
- Architecture checkpoint `AC03` confirms dependency direction, responsibility ownership, test seams, and no new partial/project/package/service-locator boundary.
- Targeted tests and the solution build pass, or any unrelated baseline limitation is precisely evidenced without hiding a feature failure.
- Playwright at `1440x900` proves populated, empty, failure, tab, countdown, force-refresh, navigation, and scroll-owner behavior with screenshots reviewed, not merely captured.
- Final bundle completed-stage validation passes and all traceability rows have implementation/proof evidence.

## Covered Inputs

- All normalized requirements, with emphasis on performance, refresh policy, architecture, and browser truth.
- Architecture checkpoint `AC03`, the `analyzing-dotnet-performance` deep pass, and `optimizing-dotnet-performance` validation.

## Prerequisites

- SB01 and SB02 are complete; checkpoints `AC01` and `AC02` passed.
- The dashboard is runnable with deterministic or representative data for browser proof.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Components/Pages/Home.razor`
- `repo://src/App/CanDoItAll.Web/Program.cs`
- `repo://src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- `repo://tests/Components/CanDoItAll.Tests.Components`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `bundle://architecture/04-csharp-testability-plan.md`
- `bundle://reviews/csharp-architecture-gate.md`

## UI Composition Contract

- Preserve the SB02 composition. This phase validates rather than redesigns it.
- Target `1440x900`; record page/document dimensions and verify no feature-owned overlay or nested scrolling.
- Review normal, alternate tab, empty, and refresh-failure states. No open-overlay screenshot is required because this feature must not introduce overlays.

## Deliverables

- Exact performance scan counts and manual classification for all changed dashboard hot-path files.
- Architecture gate with before/after dependency evidence and CodeAnalytics retry outcome.
- Build/test/browser command evidence and reviewed screenshots under `artifacts/dashboard/`.
- Updated traceability, execution report, subbundle statuses, and completed bundle validation.

## Dependency Impact

- This is the final closure gate. A failure reopens the owning earlier subbundle and invalidates only the dependent evidence named by that gate.

## Validation Depth

- Proof tier: `Behavioral` with governed C# architecture and performance review.
- Critical surface: complete dashboard behavior and operational load profile.

## Implementation Steps

1. Run the analyzing-dotnet-performance deep scans against changed C# files and manually classify every hit.
2. Review query plans/shape, bounds, cache invocation counts, cancellation, allocations, timer cadence, and error paths; optimize only evidenced issues.
3. Retry CodeAnalytics; if unavailable, record manual `.csproj`, namespace, dependency-direction, cycle-risk, and broad-call evidence.
4. Run focused tests, affected builds, and the solution-level build/test gate using a safe output configuration.
5. Read the Playwright skill, start or reuse a safe app instance, execute `1440x900` browser scenarios, capture screenshots, and inspect each screenshot.
6. Update the architecture gate, traceability, execution report, and bundle/subbundle statuses.
7. Run subbundle closure and final `--stage completed` bundle validation.

## Scope Exceptions

- Existing dependency-vulnerability warnings or a user-owned process locking Debug binaries are reported as baseline evidence, not silently modified in this dashboard bundle.
- No benchmark project is added unless ordinary proof identifies an unresolved hot path that requires measurement.

## Do Not Do

- Do not claim performance from code shape alone, dismiss scan hits without inspection, stop a user-owned process, hide failing tests, accept screenshot capture without visual review, or close with a user-visible requirement unproved.

## Acceptance Checklist

- [x] Every mandated scan has an exact count and manual disposition.
- [x] Cache/query invocation proof demonstrates the intended five-minute load ceiling and force-refresh exception.
- [x] Architecture gate is `Pass` with no unresolved blocking finding.
- [x] Targeted and solution validation evidence is recorded with baseline-only warnings separated.
- [x] Populated, process-tab, empty, and stale-refresh-error `1440x900` screenshots and DOM/scroll findings are reviewed; the failure fixture uses only its uniquely leased test database.
- [x] Bundle validator passes at completed stage with no placeholders or missing evidence.

## Proof Required

- Performance scan command/output summary and relevant source-line review.
- Targeted test results for cache, four data sources, quick action, and Home behavior.
- Release/alternate-output build evidence that is not invalidated by the already-running Debug host.
- Playwright DOM assertions, dimensions, screenshot paths, and inspection findings.
- Final CodeAnalytics or manual architecture evidence and completed validator output.

## Browser Validation Logging

- Routes: `/` and `/dashboard`; target viewport `1440x900`.
- Actions/assertions: load populated dashboard, inspect metrics/actions/projects/workflow tab, switch to Process tab, force refresh and observe countdown reset, then exercise deterministic empty and failure states.
- Screenshot paths: `bundle://evidence/SB03/home-dashboard-1440x900-populated.png`, `bundle://evidence/SB03/home-dashboard-1440x900-processes.png`, `bundle://evidence/SB03/home-dashboard-1440x900-empty.png`, and `bundle://evidence/SB03/home-dashboard-1440x900-refresh-error.png`.
- Review questions: are action cards truly square and centered; are key signals visible without excessive scrolling; do lists remain bounded; is status/mode hierarchy clear; does only the page scroll; are any controls clipped or overlays present?

## Progression Gate

- `AC03 Approved`. Source, dependency, performance, test, build, browser, and validator evidence is recorded in `bundle://evidence/SB03/performance-architecture-browser-closure.md` and `bundle://reviews/01-execution-report.md`.

## Closure Evidence

- `bundle://evidence/SB03/performance-architecture-browser-closure.md`
- `bundle://evidence/SB03/home-dashboard-1440x900-viewport.png`
- `bundle://evidence/SB03/home-dashboard-1440x900-populated.png`
- `bundle://evidence/SB03/home-dashboard-1440x900-processes.png`
- `bundle://evidence/SB03/home-dashboard-1440x900-empty.png`
- `bundle://evidence/SB03/home-dashboard-1440x900-refresh-error.png`
- `bundle://evidence/SB03/home-dashboard-390x844.png`

## Reopen Triggers

- Reopen SB01 for data/cache/query defects, SB02 for UI/timer/route/layout defects, or SB03 for incomplete performance/architecture/build/browser evidence. Re-run every invalidated downstream proof after correction.

## Suggested Agent Prompt

```text
Execute SB03 as the final gate. Do not redesign the feature without a reopen trigger. Run and classify the requested performance scans, prove architecture and builds/tests, inspect real-browser evidence at 1440x900, update all durable bundle records, and close only after completed-stage validation passes.
```
