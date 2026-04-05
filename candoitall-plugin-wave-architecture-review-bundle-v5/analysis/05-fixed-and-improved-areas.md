# Fixed and Improved Areas

## S-001

Canonical project-party assignments are no longer primarily stored in Workbench metadata. The CRM/HR ownership direction is much healthier.

- Evidence: `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:121-198; src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:145-353`

## S-002

Typed ProjectNodeReference exists at the cross-module boundary, which is a real improvement over raw string-only bridge contracts.

- Evidence: `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:121-149; src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs:8-68`

## S-003

Delete and move compensation paths are covered by integration tests. That materially lowers current risk even though the seam is still non-atomic.

- Evidence: `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`

## S-004

Hierarchy cycle protection now exists in ProjectStructureInvariantService.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:7-54`

## S-005

Pure view-state persistence is already separated from node persistence; keep that distinction and do not regress it.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:113-131`

## S-006

Semantic coordinates and marker sets are persisted today. These should stay canonical because they carry project meaning, not just rendering cosmetics.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:46-57; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:189-200`
