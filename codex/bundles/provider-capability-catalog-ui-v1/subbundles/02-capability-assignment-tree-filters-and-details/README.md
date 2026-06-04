# Capability assignment tree filters and details

## Status

- `Completed`

## Objective

- Rework capability assignment into a tree-selected agent workspace with durable capability tags, filter toolbar, compact card grid, and detail edit dialogs.

## Covered Inputs

- N04, N05, N06, N09, N10, N11.

## Prerequisites

- SB01 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Capabilities/CapabilityModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs`

## Deliverables

- Capability tag model/editor persistence.
- Agent tree and capability filters.
- Desktop card grid.
- Capability details dialog with guarded built-in tool edits and typed MCP config editor.

## Dependency Impact

- SB03 wizard saves into the same capability editor path and depends on these tag/config contracts.

## Validation Depth

- Critical UI/data foundation.

## Implementation Steps

1. Add capability tags to models/editors/save path and normalizer.
2. Replace flat agent rail with `TreeView`.
3. Add filter state and filter toolbar.
4. Convert vertical capability list into desktop grid cards.
5. Add details dialog and MCP config helpers.
6. Add component tests for filters/dialog saves.

## Scope Exceptions

- Built-in tool key/path/config edits may remain read-only, but tags must remain editable.

## Do Not Do

- Do not implement chat-time `/skills-tag:*` expansion.
- Do not auto-run MCP servers from the editor.

## Acceptance Checklist

- Filters narrow capability cards correctly.
- Details dialog persists tags for built-in tools.
- MCP parameter edits round-trip into configuration JSON.
- Large desktop grid has multiple cards per row.

## Proof Required

- Targeted component tests.
- Browser screenshot of capability grid and details dialog open state.
- Source hash/proof manifest entries.
- Closure manifest: `bundle://proof/SB02/manifest.md`.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/agents?tab=capabilities`.
- Viewport: large desktop first.
- Actions: select agent tree node, apply search/tag/type/assignment filters, open details for MCP/Skill/Tool.
- Screenshot review: multiple card columns, no text overlap, dialogs readable, filters fit toolbar.

## Progression Gate

- Capability save/edit/tag behavior must pass before SB03 wizard creation uses the same path.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
