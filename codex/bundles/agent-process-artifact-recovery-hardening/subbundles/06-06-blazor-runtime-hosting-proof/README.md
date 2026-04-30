# 06 Blazor Runtime Hosting Proof

## Status

- `Completed`

Completion note: superseded by subbundle 07 for process-core implementation.

## Objective

Record the generated-app runtime failure as diagnostic evidence. This subbundle's earlier app-specific process-core repair direction is superseded by subbundle 07 because universal dispatch must not contain calculator, Blazor, or .NET-specific recipes.

## Covered Notes

- CalcApp returned HTTP 500 with `Cannot find the fallback endpoint specified by route values: { page: /_Host, area: }`.
- The process retried but still left an unusable calculator app.
- A working generated app, not only a buildable scaffold, is the requested outcome for the governed delivery lane.

## Prerequisites

- Subbundles 01-05 remain available as the prior artifact and retry hardening foundation.
- Current generated app exists at `C:\programovani\dotnet\calculatorblazor\CalcApp`.
- Dispatch proof code and seeded agent instructions are current after partial-file source moves.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\agent-process-artifact-recovery-hardening\inputs\03-2026-04-28-calcapp-runtime-failure.md`
- `C:\repositories\CanDoItAll\codex\bundles\agent-process-artifact-recovery-hardening\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\programming-workspace-analyst.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\delivery-qa-observer.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- The earlier scope repaired CalcApp directly and added process proof for a Blazor-specific invalid hosting shape.
- That is now treated as the wrong process-core repair. Runtime proof remains required, but framework-specific repair instructions must live in Blazor/.NET agents, skills, or tools.

## Dependency Impact

- Reopens the earlier completion claim because the old proof allowed a generated app to build and test while failing at runtime.
- Downstream QA and release approval must treat startup/runtime smoke as required proof for generated UI applications.
- Recovery guidance must resolve the actual target deliverable instead of assuming a fixed sample folder or framework.

## Validation Depth

- App build and test proof for `CalcApp`.
- Runtime HTTP proof against `/` and query-backed calculator results.
- Browser proof with screenshot-backed inspection for the repaired calculator route.
- Focused integration tests for dispatch detection of legacy Blazor Server hosting and explicit component package references.
- Bundle validator rerun after source-reference repair.

## Implementation Steps

1. Reproduce the HTTP 500 against the original generated `CalcApp`.
2. Restore `Program.cs` to `AddRazorComponents` plus `MapRazorComponents<App>()` and remove legacy `MapFallbackToPage("/_Host")` hosting.
3. Convert `Home.razor` to a static SSR GET-backed calculator route with clear/reset and divide-by-zero handling.
4. Harden implementation-proof detection and recovery prompts for legacy Blazor Server host rewrites.
5. Run app build, tests, runtime smoke, browser proof, targeted integration tests, and bundle validation.
6. Record the new proof and final gate decision.

## Do Not Do

- Do not fix this by adding `Pages/_Host.cshtml` to satisfy the wrong hosting model.
- Do not make the static SSR calculator depend on `@onclick` handlers.
- Do not accept build/test proof without runtime route proof for generated UI apps.
- Do not hardcode one calculator project name in recovery guidance.

## Acceptance Checklist

- CalcApp `/` returns HTTP 200 after repair.
- Calculator query flow renders a computed result and divide-by-zero message.
- `Program.cs` no longer contains `AddRazorPages`, `AddServerSideBlazor`, `MapBlazorHub`, or `MapFallbackToPage`.
- The process blocks/retries this invalid hosting shape with an actionable recovery reason.
- Browser validation analytics and subbundle gate rows are updated.

## Proof Required

- `dotnet build C:\programovani\dotnet\calculatorblazor\CalcApp\CalcApp.csproj /p:UseSharedCompilation=false`
- `dotnet test C:\programovani\dotnet\calculatorblazor\CalcApp.Tests\CalcApp.Tests.csproj /p:UseSharedCompilation=false`
- Runtime smoke with HTTP 200 for `/`, `/?left=6&right=7&operation=Multiply`, and divide-by-zero.
- Focused integration test run covering the new dispatch guards.
- Browser screenshot proof for the repaired route.

## Browser Validation Logging

- Add a browser-validation analytics row for route `http://127.0.0.1:<port>/`.
- Record viewport, Playwright actions, screenshot path, and whether the visual review passed.
- Screenshot review must confirm readable text, no clipping, visible computed result, and coherent layout.

## Progression Gate

- Bundle closure may stand only after subbundle 07 proves process-core neutrality with tests, scans, and bundle validation.

## Suggested Agent Prompt

```text
Implement subbundle 06 only. Repair the generated CalcApp runtime failure, add process guards for legacy Blazor Server host rewrites in net10 Blazor Web App attempts, and capture build, test, runtime, browser, and bundle-validation proof before closing.
```
