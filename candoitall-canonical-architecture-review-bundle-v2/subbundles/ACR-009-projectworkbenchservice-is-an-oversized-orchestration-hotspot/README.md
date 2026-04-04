# ACR-009 — ProjectWorkbenchService is an oversized orchestration hotspot

- Severity: **High**
- Skill source: `architecture-drift-audit`
- Category: Dependency drift
- Phase: **Phase 4**
- Timing: **Before next feature wave**
- Dependencies: Execute after the semantic seams from ACR-001/002/003/004/012/014 exist; avoid cosmetic extraction first.

## Problem statement

A 2900+ line service owns graph sync, CRUD, transfer, media save, view state, command translation, DTO mapping, and now indirectly participates in party integration flows, making safe change difficult.

## Why this matters now

This service is the main amplifier of all other architectural drift; it should be reduced after—not before—the canonical seams are real.

## Deliverables

- Thin façade for ProjectWorkbenchService or equivalent orchestration root
- Extracted graph assembly / invariant / projection / facet lifecycle collaborators

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/*`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
