# 03-quality-operations-tab

## Status

- `Completed`

## Completion Evidence

- Quality operations tab is reachable from `/cognitive-memory`.
- Diagnostics, cluster planning, dream execution, aggregate apply, and paged quality result lists are wired to existing services.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter CognitiveMemoryPageTests --no-restore` passed.

## Objective

Add a Quality operations tab that exposes the new quality-foundation functions through explicit UI controls and result panels.

## Covered Inputs

- UI-06, UI-07, UI-08, UI-09, UI-10, UI-13.

## Prerequisites

- Subbundle 02 complete.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.Rendering.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs`

## Deliverables

- New Quality operations tab.
- Diagnostics, cluster planning, dream dry-run/persisted execution, and aggregate apply controls.
- Paged quality clusters, dream runs, aggregate candidates, and synthesized recall panels.
- Component tests for the new tab and action affordances.

## Dependency Impact

- Subbundle 04 uses this tab as one of the tab-by-tab layout targets.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Inject required quality services into the page.
2. Add quality operation state and action handlers.
3. Add the new tab component.
4. Wire pagers for quality lists.
5. Add component tests.

## Do Not Do

- Do not silently swallow quality operation errors.
- Do not expose restricted reference locators by default.
- Do not add medium/small layout tuning.

## Acceptance Checklist

- Quality operations tab exists and is reachable.
- All new quality functions have visible access or visible quality output.
- Aggregate apply is only enabled for approved candidates.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryPageTests" --logger "console;verbosity=minimal" -m:1`

## Browser Validation Logging

- Record `/cognitive-memory`, large desktop viewport, Quality operations tab, and result.

## Progression Gate

- Subbundle 04 may proceed only after quality operations are reachable.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add the Quality operations tab and explicit quality service actions without changing medium/small layouts.
```
