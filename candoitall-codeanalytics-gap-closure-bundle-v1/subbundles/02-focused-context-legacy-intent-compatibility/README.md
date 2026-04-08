# Focused context legacy intent compatibility

## Status

- `Completed`

## Objective

- Restore compatibility for stale callers that still send `intent = Behavior` while preserving the current `TroublePath` semantics and deterministic symbol-first guidance.

## Covered Inputs

- `REQ-04`
- `REQ-05`
- `finding-02-legacy-focused-context-behavior-intent-alias-fails.md`

## Prerequisites

- Prepared-stage bundle validation has passed.
- `subbundles/01-project-inventory-classification-and-filtering` does not block implementation here, but both fixes must close before subbundle 03.

## Exact Source References

- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\FocusedContextIntent.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Context.Strategy.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsCoordinator.cs`
- `C:\repositories\CanDoItAll\codex\skills\candoitall-codeanalytics-mcp\SKILL.md`

## Deliverables

- Compatibility handling for legacy `Behavior` intent requests.
- Proof that `TroublePath` still succeeds with the same explicit method seed path used in the parity work.
- Tests or harness proof that the alias no longer fails generically.

## Dependency Impact

- Subbundle 03 depends on this phase to close the second residual finding honestly.
- Weak proof here would leave stale clients broken even if the newer skill guidance works.

## Validation Depth

- `Targeted compatibility and regression proof`

## Implementation Steps

1. Add narrow alias handling for the historical `Behavior` intent.
2. Preserve the existing `TroublePath` and symbol-first query path behavior.
3. Add or update tests and harness proof for both the alias and the current enum path.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not broaden alias handling into free-form fuzzy parsing.
- Do not change the focused-context traversal strategy beyond the alias mapping needed to close the finding.
- Do not push the fix into prompt-only guidance and pretend that closes the compatibility gap.

## Acceptance Checklist

- `Behavior` resolves successfully to the same effective behavior path as `TroublePath`.
- `TroublePath` still returns the expected focused-context response for `ApplyExternalScoreAsync()`.
- The fix does not break current skill guidance or current enum-driven usage.

## Proof Required

- Host build proof
- Focused-context proof through the installed MCP or harness for both `Behavior` and `TroublePath`

## Browser Validation Logging

- `N/A`

## Progression Gate

- Closure work may continue only after alias proof succeeds and current `TroublePath` proof still passes.

## Suggested Agent Prompt

```text
Implement the focused-context legacy intent compatibility subbundle only. Close the `Behavior` alias gap narrowly, keep `TroublePath` behavior unchanged, and prove both paths against the installed MCP.
```
