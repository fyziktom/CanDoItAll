# Current State

## What improved compared with the earlier state

- The cross-module public boundary now has `ProjectNodeReference`, which is healthier than raw string-only bridge contracts.
- CRM/HR node-scoped assignment ownership direction is better than before.
- Hierarchy cycle protection and the explicit ban on user-authored hierarchy-like generic links now exist.
- Delete/move compensation paths are covered by integration tests.
- ADR guardrails were added to the repo.

## What is still materially wrong

The deepest blockers for the external plugin wave are **still open**:

- Workbench still persists a synchronized foreign-module projection graph as system-managed canonical rows.
- The node carrier is still too broad.
- Kind semantics are still scattered.
- Reclassification still lacks durable lifecycle history.
- Assignment capability rules are still too weak for the next wave.
- Provider/resource extensibility is still enum/switch driven.

## Static inventory snapshot

- Projects (`*.csproj`): `42`
- C# files: `603`
- Razor files: `338`

## Architectural hotspots

- `5001` LOC — `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `3227` LOC — `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `1951` LOC — `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `1292` LOC — `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`

## Bottom line

Phase 5 was valuable, but it did **not** finish the canonical-model stabilization work that is needed before the plugin wave.
