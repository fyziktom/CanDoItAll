# ACR-012 — Party responsibility truth is duplicated across node metadata, assignment tables, and module-local fields

- Severity: **Critical**
- Skill source: `canonical-model-review`
- Category: Source-of-truth drift
- Phase: **Phase 1**
- Timing: **Now**
- Dependencies: Depends on ACR-003 and ACR-011. Enables ACR-013 and ACR-015. Strongly coupled with ACR-014.

## Problem statement

CRM/HR responsibility is now stored in more than one editable place. Participant, meeting, and work-item flows write both node metadata and project-party assignments, while Resources, Validation, and TestLab also store module-local responsible-party fields.

## Why this matters now

People, agents, and partners are now first-class project actors. Duplicated writable responsibility truth is a direct threat to agentic coordination and auditability.

## Deliverables

- Canonical actor-assignment owner with explicit scope model
- Decision on which existing fields become derived, cached, or deprecated
- Migration/backfill plan for node metadata and module-local responsibility mirrors

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Validation/ValidationModels.cs`
- `src/CanDoItAll.Modules.TestLab/TestLabModels.cs`
- `tests/CanDoItAll.Tests.Integration/*`
