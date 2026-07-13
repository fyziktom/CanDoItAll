# 11 Project And Workbench Source Adapters

## Status

- `Completed`

## Objective

- Add Source Gateway adapters for project structures, project nodes, workbench project data, and explicit project ingestion actions.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R07
- R08

## Prerequisites

- SB10 gate passed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`
- `repo://src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions/MemorySourceSnapshotModels.cs`
- `repo://src/App/CanDoItAll.Composition/SchedulerPlannerWorkflowInputOptionProviders.cs`
- `bundle://inventories/01-current-memory-surface-inventory.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement Source Gateway adapters for project structures, project nodes, project metadata, workbench project data, and explicit project ingestion actions.
- Expose snapshots containing project id/name/tags/budget/domain, node ids/titles/types, relevant artifacts, provenance, sensitivity, and redaction metadata.
- Add an ingestion action entry point for project pages or APIs that creates a generic ingestion job for a selected provider.
- Avoid direct provider access to project EF entities; adapters own all module reads and snapshot mapping.
- Add tests for project-level, node-level, all-project-names, denied-scope, and missing-project snapshots.
- Migrate or adapt the existing Workbench project-structure source snapshot provider to the generic Source Gateway contract without losing current source identity, cursor/hash, redaction, or unavailable-source semantics.

## Dependency Impact

- Manual ingestion and provider source requests for projects depend on safe snapshots.

## Validation Depth

- `Source adapter foundation`

## Implementation Steps

1. Inventory current project/workbench services and choose adapter boundaries that do not expose module internals.
2. Implement project snapshot mappers using DTOs and source references, not EF entities.
3. Implement provider source request resolution for project-wide, node-specific, and metadata-only scopes.
4. Add manual `Ingest into memory` command path that uses provider selection and operation ledger.
5. Add tests covering redaction, provenance, sensitivity, selected provider, and missing project behavior.
6. Add compatibility proof that the current Workbench source snapshot provider has been reused, moved, or wrapped rather than replaced by an incompatible duplicate.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- A provider can request a project or node snapshot only through Source Gateway policy.
- Manual project ingestion creates a generic ingestion operation tied to provider id and source snapshot id.
- Snapshots preserve enough project context for memory recall without leaking full module entities.

## Proof Required

- Create `proof/SB11/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run Source Gateway adapter tests for project/workbench scenarios.
- Run source audit proving project adapters do not appear in provider driver projects.
- Run an audit proving Workbench project snapshots use the same generic source snapshot contract family as provider-initiated source requests.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB11 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB11 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
