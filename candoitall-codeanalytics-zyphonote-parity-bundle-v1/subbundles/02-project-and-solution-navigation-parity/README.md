# Project and solution navigation parity

## Status

- `Completed`

## Objective

- Add first-class direct project and solution navigation tools so architecture questions can be answered without abusing the usage-weighted dependency view.

## Covered Inputs

- `REQ-02`
- `REQ-03`
- `REQ-04`
- Zyphonote Scenario 1 direct project-reference gap

## Prerequisites

- `subbundles/01-findings-normalization-and-gap-inventory`
- Prepared-stage bundle validation has passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsCoordinator.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsTools.cs
- C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Abstractions\ICodeAnalyticsApplicationService.cs
- C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Domain\Facts\ProjectFact.cs
- C:\repositories\CanDoItAll.CodeAnalsis\src\CanDoItAll.CodeAnalytics.Workspace\Inventory\ProjectFileInventoryReader.cs
- C:\repositories\zyphonote\bundles\2026-04-08-codeanalytics-vs-sharptools-evaluation\subbundles\03-codeanalytics-mcp-scenario-runs\findings\finding-01-project-reference-scenario-gap.md

## Deliverables

- New abstractions and application-service queries for solution and project inventory.
- Host MCP tools and input models for the new project-navigation surface.
- Targeted tests that prove direct project references are returned cleanly.

## Dependency Impact

- Scenario 1 rerun proof depends entirely on this subbundle.
- Downstream skill guidance depends on the exact tool names and shapes created here.
- If the returned data still mixes direct references with usage weights, the rerun remains misleading.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extend sibling abstractions and application service with project and solution inventory queries.
2. Implement the new host coordinator, models, and MCP tools.
3. Add focused tests for direct project-reference answers on a controlled fixture or real snapshot.
4. Validate the new path against a real snapshot before closing the subbundle.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not overload `code_analytics_dependencies_get` with ambiguous semantics.
- Do not parse `.csproj` files in the host MCP when the sibling repo can own the logic.

## Acceptance Checklist

- The MCP exposes a clean direct-reference answer path for projects.
- The returned data distinguishes project inventory from weighted dependency edges.
- A targeted validation proves the path can answer the Scenario 1 question without shelling into raw project files.

## Proof Required

- Build or test proof in `C:\repositories\CanDoItAll.CodeAnalsis`
- Build proof in `C:\repositories\CanDoItAll`
- One targeted query validation against a fresh snapshot

## Browser Validation Logging

- N/A

## Progression Gate

- New project-navigation tools build successfully and produce a trustworthy direct-reference answer on real data.

## Suggested Agent Prompt

```text
Implement the project and solution navigation parity subbundle only. Keep the host MCP thin, and prove the new path answers direct project-reference questions cleanly.
```
