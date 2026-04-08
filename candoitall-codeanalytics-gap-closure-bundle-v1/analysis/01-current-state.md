# Current State

## Existing Bundle Context

- `C:\repositories\CanDoItAll\candoitall-codeanalytics-zyphonote-parity-bundle-v1` is already completed and validated.
- That earlier bundle raised two residual findings instead of leaving them implicit.
- The installed MCP already scores `47 / 50` on the five Zyphonote benchmark scenarios, so this follow-on work is precision and compatibility cleanup, not a broad parity rewrite.

## Relevant Host MCP Surface

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsCoordinator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsTools.cs`
- `C:\repositories\CanDoItAll\codex\skills\candoitall-codeanalytics-mcp\SKILL.md`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`

## Relevant Sibling Library Surface

- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Queries\SolutionInventoryQuery.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Queries\ProjectInventoryQuery.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Responses\ProjectInventoryItem.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\Responses\ProjectLinkItem.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Inventory.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\FocusedContextIntent.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Application\Services\CodeAnalyticsApplicationService.Context.Strategy.cs`
- `C:\repositories\CanDoItAll.CodeAnalsis\tests\CanDoItAll.CodeAnalytics.Tests.Unit\ApplicationFacts.cs`

## Observed Gap Mechanics

- Inventory currently returns a single `ReferencedByProjects` set that mixes product, test, and benchmark callers.
- No first-class project classification exists in `ProjectFact` or the inventory response surfaces today.
- Focused-context intent currently exposes enum values like `Auto` and `TroublePath`; the historical `Behavior` alias is not accepted cleanly.
- The alias failure likely occurs at or before host input normalization because the existing library only understands the enum values in `FocusedContextIntent`.
