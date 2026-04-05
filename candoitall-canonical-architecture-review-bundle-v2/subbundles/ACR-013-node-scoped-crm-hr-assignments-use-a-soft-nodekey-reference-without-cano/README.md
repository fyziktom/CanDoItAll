# ACR-013 — Node-scoped CRM/HR assignments use a soft NodeKey reference without canonical integrity checks

- Severity: **High**
- Skill source: `feature-block-architecture-review`
- Category: Invariant drift
- Phase: **Phase 0**
- Timing: **Now**
- Dependencies: Depends on ACR-011. Full role validation depends on ACR-003; minimal existence/project validation can start immediately.

## Problem statement

ProjectPartyAssignment stores NodeKey as a plain string and SaveAssignmentAsync validates only project and party existence. There is no visible check that the referenced node exists, belongs to the same project, or allows the requested role.

## Why this matters now

This is the most direct CRM/HR integrity hole introduced by the new module.

## Deliverables

- Node-scoped assignment validator
- Guardrail tests for orphan/mismatched/disallowed assignments
- Explicit policy for node-scoped versus project-scoped assignments

## Likely files touched

- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs`
