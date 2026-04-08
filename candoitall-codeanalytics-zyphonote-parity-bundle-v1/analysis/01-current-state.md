# Current State

## Host MCP state

- `src/CanDoItAll.Mcp.CodeAnalytics/CodeAnalyticsTools.cs` already exposes snapshot build, dependencies, services, persistence, exports, type search, symbol search, symbol definition, symbol members, symbol implementations, symbol references, and focused context.
- `src/CanDoItAll.Mcp.CodeAnalytics/CodeAnalyticsCoordinator.cs` is a thin wrapper over `ICodeAnalyticsApplicationService` and currently has no dedicated project-reference or document/source-reading surface.
- `tools/Reinstall-CanDoItAllMcps.ps1` already publishes and registers `CanDoItAll.Mcp.CodeAnalytics`, so host integration is present and only needs extension.

## Sibling library state

- `src/CanDoItAll.CodeAnalytics.Abstractions/ICodeAnalyticsApplicationService.cs` currently exposes snapshot, dependency, service, persistence, symbol, and focused-context queries.
- `src/CanDoItAll.CodeAnalytics.Domain/Facts/ProjectFact.cs` already stores direct `ProjectReferences`, `PackageReferences`, target frameworks, and document counts.
- `src/CanDoItAll.CodeAnalytics.Workspace/Inventory/ProjectFileInventoryReader.cs` already knows how to read direct `ProjectReference` edges from project files.
- `src/CanDoItAll.CodeAnalytics.Application/Services/CodeAnalyticsApplicationService.Symbols.Source.cs` already reads source excerpts from disk for symbol definitions and references, so raw source access can stay in the same architectural direction.

## Prior parity work

- `C:\repositories\CanDoItAll.CodeAnalsis\CanDoItAll.CodeAnalsis.SymbolToolsBundle\inventories\01-missing-sharptools-surface.md` shows that the first parity pass already closed the major symbol-navigation gaps: search definitions, view definition, get members, list implementations, and find references.
- That older parity bundle did not target direct project-reference answers, project and solution inventory, or document-level inspection.

## Current benchmark gaps

- Zyphonote Finding 1 shows that `code_analytics_dependencies_get` returns usage-weighted dependency data instead of a clean direct project-reference answer.
- Zyphonote Finding 2 shows that the member-focused summary path failed for `NotationEditor.ApplyExternalScoreAsync()`.
- Scenario 4 is currently answerable only by dropping to lower-level symbol definition reads, which is weaker than the intended ergonomic path.
