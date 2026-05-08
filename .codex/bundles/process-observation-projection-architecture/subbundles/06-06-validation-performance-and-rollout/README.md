# 06-validation-performance-and-rollout

## Status

- `Ready`

## Objective

- Close the architecture implementation with regression, performance, browser, mock-agent, generic process, and independent .NET app build validation before rollout.

## Success Criteria

- All preceding subbundle progression gates are passed or explicitly blocked.
- Existing Processes functionality is preserved.
- Observation reads are bounded, cancellable, and cache-aware.
- Mock-agent workflow tests pass.
- Independent simple .NET app build cases pass.
- Browser validation proves the Processes page remains usable and visually correct.
- Performance baseline comparison shows no unacceptable regression and preferably improved live observation behavior.

## Covered Inputs

- R-001 through R-012 final closure.
- User requirement to preserve today's functionality.
- User requirement to test with mock agents and independent simple .NET app builds.
- Microsoft Learn Blazor performance expectations.
- `analyzing-dotnet-performance` findings and follow-up scan.

## Prerequisites

- `01-current-state-observation-map`
- `02-observation-contracts-and-boundary`
- `03-projection-cache-and-invalidation`
- `04-ui-observation-shell-and-dialogs`
- `05-ai-driven-dashboard-intent-bridge` if implemented in the rollout slice

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessActiveRunSummaryPerformanceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRuntimeReadQueryServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRuntimeOperatorReadModelTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

## Deliverables

- Final execution report with all commands, browser artifacts, performance numbers, and gate decisions.
- Updated validation checklist for mock-agent and generic process cases.
- Independent simple .NET app build evidence.
- Follow-up performance scan using the same checklist as `analysis/03-performance-scan.md`.
- Rollout decision, rollback path, and residual-risk list.

## Dependency Impact

- This is the closure phase. Weak proof means the architecture must not roll out broadly.
- Future flexible dashboard and AI dashboard work should not begin until this phase proves the observation boundary can carry current behavior safely.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Review all prior subbundle execution reports and confirm progression gates passed.
2. Run a clean build.
3. Run targeted integration tests for runtime reads, active-run performance, operator read models, mock-agent runtime, and automation dispatch.
4. Run component tests for `ProcessWorkspace`.
5. Run new observation/cache/intent tests added by prior subbundles.
6. Run browser proof on `/processes` with large and narrow viewports.
7. Execute mock-agent scenarios that cover active runs, step progress, approvals/escalations if available, details, and observation refresh.
8. Create independent simple .NET app cases in a temporary workspace and build them:
   - console app
   - class library
   - minimal web app or web API
9. If the process harness can run against those projects, run generic process observations against at least two of them; otherwise record build proof and the reason process harness execution was unavailable.
10. Re-run the targeted performance anti-pattern scan on changed process observation files.
11. Compare measured read/render behavior against the baseline from subbundle `01`.
12. Decide rollout, rollback, and cleanup.

## Scope Exceptions

- If an external tool or environment prevents a validation command, record the exact blocker, not a generic skip.
- Full future dashboard and speech/conversational UI remain out of scope unless implemented by earlier subbundles.

## Do Not Do

- Do not accept failing tests as closure.
- Do not ignore browser-visible layout issues.
- Do not claim performance improvement without numbers or a reasoned bounded comparison.
- Do not leave temporary feature flags, test projects, or generated app folders unaccounted for.
- Do not delete user work or unrelated changes.

## Acceptance Checklist

- Build passes.
- Targeted integration tests pass.
- Component tests pass.
- New observation/cache/intent tests pass.
- Browser large and narrow viewport proof is captured and reviewed.
- Mock-agent process validation passes.
- Independent .NET app builds pass.
- Performance scan has no new unaddressed hot-path red flags.
- Execution report closes every requirement or records a blocker.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests|FullyQualifiedName~ProcessRuntimeReadQueryServiceTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspace"`
- Commands for new observation/cache/intent tests.
- Temporary independent build commands, for example:
  - `dotnet new console -n ObservationSmokeConsole`
  - `dotnet build ObservationSmokeConsole\ObservationSmokeConsole.csproj`
  - `dotnet new classlib -n ObservationSmokeLibrary`
  - `dotnet build ObservationSmokeLibrary\ObservationSmokeLibrary.csproj`
  - `dotnet new webapi -n ObservationSmokeWebApi`
  - `dotnet build ObservationSmokeWebApi\ObservationSmokeWebApi.csproj`
- Browser screenshots and assertion notes for `/processes`.
- Updated performance scan counts.

## Browser Validation Logging

- Target route: `/processes`
- Required viewports: large desktop/maximized and narrow responsive width.
- Required actions: navigate, wait for initial load, switch process if available, open Runs and Analytics, open a detail dialog, close dialog, verify active observation areas update without full-page visual disruption.
- Required screenshots: desktop, narrow, detail dialog, and any stale/error state if exercised.
- Review questions: Is there overlap? Are controls reachable? Are high-count regions bounded? Are stale/error states explicit? Does refresh avoid visible thrash?

## Progression Gate

- The bundle can close only when all required proof is recorded, every requirement is traced to passed validation or an explicit blocker, and rollout/rollback guidance is written in the execution report.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
