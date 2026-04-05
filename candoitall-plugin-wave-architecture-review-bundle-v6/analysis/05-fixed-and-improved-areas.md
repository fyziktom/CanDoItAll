# Fixed and Improved Areas

## S-001 - Strength

Typed ProjectNodeReference now exists at the cross-module boundary, which is a real improvement over raw string-only public bridge contracts.

- Evidence: `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:121-149`

## S-002 - Strength

CRM/HR project-party ownership is healthier than in the earlier state; node-scoped assignment operations now flow through the explicit bridge instead of hiding primarily in Workbench metadata.

- Evidence: `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:151-198; src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`

## S-003 - Strength

Hierarchy cycle protection and a ban on user-authored Contains/BelongsTo links now exist in ProjectStructureInvariantService.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:7-74`

## S-004 - Strength

Delete and move compensation paths are covered by integration tests, which reduces immediate operational risk even though the seam is still non-atomic.

- Evidence: `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`

## S-005 - Strength

ADR guardrails were added inside the repo, which is useful because the architectural intent is now at least documented and reviewable.

- Evidence: `architecture/adrs/ADR-0001-canonical-project-party-assignment-ownership.md; ADR-0002-workbench-party-metadata-is-projection-only.md; ADR-0003-use-typed-project-node-references-across-module-boundaries.md; ADR-0004-workbench-node-extension-guardrails.md`

## S-006 - Strength

Pure view-state persistence is separated into its own Workbench view-state record instead of being mixed into the main node rows.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:113-130; 1749-1764; 2624-2645`
