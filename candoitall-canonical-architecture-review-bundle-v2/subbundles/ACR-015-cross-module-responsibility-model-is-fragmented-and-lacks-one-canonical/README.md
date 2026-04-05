# ACR-015 — Cross-module responsibility model is fragmented and lacks one canonical actor-assignment owner

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Boundary drift
- Phase: **Phase 3**
- Timing: **Before next feature wave**
- Dependencies: Depends on ACR-012 and ACR-013. Influenced by ACR-003 and ACR-014.

## Problem statement

Resources, Validation, TestLab, project-party assignments, and workbench node metadata each represent responsibility in their own way. There is no explicit ownership matrix for who owns project-level, node-level, and module-level actor assignments.

## Why this matters now

As soon as people, agents, delivery units, and partners become reusable actors across modules, responsibility modeling becomes an architectural spine, not just CRM data.

## Deliverables

- Actor-assignment scope model and ownership matrix
- Migration/backfill plan for module-local mirrors
- Explicit documentation for canonical vs mirrored responsibility fields

## Likely files touched

- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Validation/ValidationModels.cs`
- `src/CanDoItAll.Modules.TestLab/TestLabModels.cs`
- `tests/CanDoItAll.Tests.Integration/CrmHrCrossModuleIntegrationTests.cs`
