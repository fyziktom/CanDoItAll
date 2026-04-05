# ACR-004 — Relation semantics are blurred and hierarchy is stored twice

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Boundary drift
- Phase: **Phase 1**
- Timing: **Now**
- Dependencies: Foundational for ACR-001, ACR-002, ACR-005, ACR-006, and ACR-014.

## Problem statement

ParentNodeKey and hierarchy link rows both describe parentage, while dependency analysis folds ancestry into prerequisites and inverse blocking semantics. As CRM/HR links and critical-path logic grow, blurred graph semantics become more dangerous.

## Why this matters now

Without explicit relation taxonomy, critical-path analysis, actor assignment scope, and graph assembly will continue to blur different meanings.

## Deliverables

- Canonical hierarchy owner (e.g. NodeCarrier.ParentNodeKey or a dedicated containment record)
- Explicit non-hierarchy NodeRelation model for DependsOn/Blocks/Uses/etc.
- Relation policy matrix by node kind

## Likely files touched

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureDependencyAnalysis.cs`
- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
