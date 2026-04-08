# Member behavior and source inspection parity

## Status

- `Completed`

## Objective

- Close the method-behavior gap by adding stable source and member inspection surfaces and by fixing or safely bypassing the failing member-focused context path.

## Covered Inputs

- `REQ-04`
- `REQ-05`
- Zyphonote Scenario 4 member-behavior failure

## Prerequisites

- `subbundles/02-project-and-solution-navigation-parity`
- Verified proof that the new project-navigation tools build and work.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsCoordinator.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsTools.cs
- C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Context.cs
- C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Context.SeedResolution.cs
- C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Symbols.Source.cs
- C:\repositories\zyphonote\bundles\2026-04-08-codeanalytics-vs-sharptools-evaluation\subbundles\03-codeanalytics-mcp-scenario-runs\findings\finding-02-focused-context-member-query-failed.md

## Deliverables

- New document or source inspection tool surfaces needed for SharpTools-style analysis parity.
- A fixed or replaced method-behavior path that no longer fails on realistic member queries.
- Targeted tests or harness proof for the member-oriented flow.

## Dependency Impact

- Scenario 4 rerun proof depends on this subbundle.
- Skill guidance depends on the exact tool flow that emerges here.
- If this phase closes weakly, the final rerun will still require shell reads for behavior questions and the parity claim will be overstated.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Reproduce the member-focused failure or confirm the exact current failure mode.
2. Extend sibling abstractions and application services with the missing document and source inspection surface.
3. Fix focused context for member seeds if the bug is localized; otherwise add a deterministic tool path that can answer the same class of question.
4. Expose the new surface through the host MCP and validate it on realistic member queries.

## Scope Exceptions

- If focused context cannot be made reliable in this pass, the exception must be explicit and replaced with a deterministic behavior-oriented MCP path.

## Do Not Do

- Do not leave Scenario 4 dependent on generic invocation failures.
- Do not force users back to shell file reads when the MCP can expose the same inspection path directly.

## Acceptance Checklist

- A realistic member-behavior query no longer fails with a generic invocation error.
- The MCP exposes at least one stable source-inspection path comparable to SharpTools raw document reading.
- The new path is sufficient to answer Scenario 4 without manual shell inspection.

## Proof Required

- Build or test proof in `C:\repositories\CanDoItAll.CodeAnalsis`
- Build proof in `C:\repositories\CanDoItAll`
- One targeted validation on a realistic member such as `ApplyExternalScoreAsync()`

## Browser Validation Logging

- N/A

## Progression Gate

- The member-behavior path is stable enough to use in the Zyphonote rerun, and the new source-inspection path works through the MCP.

## Suggested Agent Prompt

```text
Implement the member behavior and source inspection parity subbundle only. Close the member-focused failure honestly: fix it if the bug is small, otherwise add a deterministic MCP path and validate it on a real method.
```
