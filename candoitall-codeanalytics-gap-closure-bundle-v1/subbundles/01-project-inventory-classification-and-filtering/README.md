# Project inventory classification and filtering

## Status

- `Completed`

## Objective

- Add first-class project classification and a precise primary inventory answer path so product-architecture questions stop mixing test and benchmark projects into the main reverse-reference set.

## Covered Inputs

- `REQ-01`
- `REQ-02`
- `REQ-03`
- `finding-01-solution-inventory-mixes-product-and-test-projects.md`

## Prerequisites

- Prepared-stage bundle validation has passed.
- Prior parity bundle remains the source of truth for the residual finding.

## Exact Source References

- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Inventory.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Responses\ProjectInventoryItem.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Responses\ProjectLinkItem.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Queries\SolutionInventoryQuery.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Queries\ProjectInventoryQuery.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\tests\CanDoItAll.CodeAnalytics.Tests.Unit\ApplicationFacts.cs`
- `C:\repositories\CanDoItAll\codex\skills\candoitall-codeanalytics-mcp\SKILL.md`

## Deliverables

- Response-level project classification for inventory items and project links.
- A primary inventory answer path that separates product references from supporting references.
- Tests that prove product and supporting projects are both visible and correctly separated.
- Skill guidance updated if the recommended inventory interpretation changes.

## Dependency Impact

- Subbundle 03 depends on this phase to judge whether Scenario 1 is actually closed.
- Weak proof here would make the rerun look cleaner only because of an undocumented heuristic, which would invalidate the bundle closure claim.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add first-class project classification and supporting-project separation in the sibling inventory response path.
2. Keep factual visibility for supporting projects instead of silently dropping them.
3. Update tests to cover product, test, and benchmark classification and the primary reverse-reference answer path.
4. Update skill guidance if the recommended consumer interpretation changes.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not hardcode Zyphonote-specific project names into the classification logic.
- Do not silently discard supporting-project references without exposing them somewhere first-class.
- Do not change unrelated dependency or symbol-search behavior in this phase.

## Acceptance Checklist

- Inventory output identifies project role explicitly.
- The main reverse-reference answer for `Zyphonote.MusicTheory.Core` contains the six product projects from the answer key.
- Supporting projects such as tests and benchmarks remain observable in a first-class way.
- Unit tests lock the behavior down.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll.CodeAnalsis\tests\CanDoItAll.CodeAnalytics.Tests.Unit\CanDoItAll.CodeAnalytics.Tests.Unit.csproj --no-restore`
- One targeted inventory proof against Zyphonote after build or reinstall

## Browser Validation Logging

- `N/A`

## Progression Gate

- Downstream closure work may continue only after tests pass and a targeted inventory query shows a clean product-only primary answer with supporting-project visibility preserved.

## Suggested Agent Prompt

```text
Implement the project inventory classification and filtering subbundle only. Keep the logic in the sibling analysis library, preserve supporting-project visibility, and prove the Zyphonote Scenario 1 answer no longer needs client-side name filtering for the primary product answer.
```
