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

## S-007

Persisted Workbench parallel truth is removed. User-authored nodes remain canonical while system-managed structure/calendar projections are assembled in memory and spatial overrides are stored separately.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:112-170; src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:186-221`

## S-008

Carrier/binding ownership is now explicit. Route, artifact/media/storage payload, and foreign references live outside the canonical carrier row without regressing the DTO surface.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs:103-170; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:641-652`

## S-009

Node semantics are centralized behind `ProjectNodeKindRegistry`, and reclassification is now auditable through lifecycle history persistence.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectNodeKindRegistry.cs; src/CanDoItAll.Modules.Workbench/ProjectNodeLifecycleHistory.cs; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

## S-010

Workspace providers and project resources now share a real connector manifest platform, which lets Workbench consume plugin hooks without editing more enums or switch blocks.

- Evidence: `src/CanDoItAll.Modules.Workspace/ConnectorPluginPlatform.cs; src/CanDoItAll.Modules.Resources/ResourceConnectorPlugins.cs; tests/CanDoItAll.Tests.Unit/ConnectorPluginRegistryTests.cs; tests/CanDoItAll.Tests.Integration/ConnectorPluginIntegrationTests.cs`

## S-011

The Workbench hotspot was materially reduced instead of merely relocated: `ProjectWorkbenchModels.cs` dropped from `1758` to `1158` lines, and `ProjectWorkbenchService` dropped from `79` to `53` members with architecture guardrail tests protecting the new split.

- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchRelationService.cs; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchLifecycleService.cs; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCommandService.cs; tests/CanDoItAll.Tests.Unit/ProjectWorkbenchServiceArchitectureTests.cs`
