# Verdict and scope

## Verdict

The Process module is **not yet architecturally closed**.

Codex did real work and several important areas are now materially better:

- optimistic concurrency exists for definitions, versions, runs, and step runs;
- save/publish/start-run/transition now use explicit transactions;
- differential child-graph persistence replaced the original delete-and-recreate save pattern;
- publish lifecycle, clone logic, runtime guard/planner logic, and read queries were meaningfully decomposed;
- cross-module helper duplication was reduced by extracting shared utilities.

That said, the remaining gaps are still structural enough that I would not accept a blanket “everything is now in order” claim.

## Why the previous closure claim is not sufficient

The repository itself still contains evidence that the work is not fully finished:

1. The checked-in execution report still lists residual risks:
   - `architecture_hardening_bundle/reviews/01-execution-report.md:197-200`
2. The code still keeps legacy dependency scalar mirrors alive inside core entity/editor/runtime models:
   - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs:168-170`
   - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs:160-162`
   - `src/CanDoItAll.Modules.Processes/ProcessRuntimeViewModels.cs:41-42`
3. The schema still lacks most definition-child and runtime foreign keys:
   - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:125-191`
   - `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs:6-175`
4. The checked-in integration `.trx` proves only three import-metadata tests, not the broader Process integration matrix:
   - `.codex-test-results/integration/integration.trx`
   - `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Scope of this follow-up

This bundle is intentionally narrower than the first initiative. It focuses only on the remaining red architectural gaps:

- true canonical dependency closure;
- database referential integrity and invariant enforcement;
- lifecycle hardening for draft/published versioning;
- durable side-effect dispatch;
- proof reconciliation;
- final structural follow-up only after the invariants are safe.


# Open findings

## F001 — Dependency modeling is still not truly canonical

### Why this is still red

The module no longer has distributed fallback logic everywhere, but core types still carry **two meanings** for the same concept:

- legacy scalar primary dependency fields;
- canonical dependency collection/rows.

That is an improvement over the original state, but it is not the same as one source of truth.

### Evidence

- Persisted step entity still has scalar mirror fields:
  - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs:168-170`
- Editor step model still has scalar mirror fields in addition to `Dependencies`:
  - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs:160-176`
- Runtime step view model still exposes single-dependency shortcut fields:
  - `src/CanDoItAll.Modules.Processes/ProcessRuntimeViewModels.cs:38-64`
- Compatibility bridge still mutates models and syncs legacy mirrors back from canonical collections:
  - `src/CanDoItAll.Modules.Processes/ProcessDependencyCompatibilityBridge.cs:5-173`
- Runtime read path still populates first-dependency shortcut fields from `Dependencies.FirstOrDefault()`:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs:189-223`
- Import normalization still remaps both legacy scalar fields and canonical collections:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.ImportNormalization.cs:81-91`

### Architectural impact

This means the codebase is still relying on “canonical + mirrored legacy compatibility” instead of “canonical only + compatibility at the boundary”.

---

## F002 — Most Process child and runtime tables still lack database-enforced foreign keys

### Why this is still red

The code now has some aggregate-boundary foreign keys, but large parts of the Process schema are still protected only by application logic and ordered delete code.

### Evidence

The only `HasForeignKey(...)` calls in the Process entity configurations are:

- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:48-51`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:71-74`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:86-89`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:114-121`

There are **no** foreign-key mappings in:

- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs:6-175`

The current FK migration adds only that small subset:

- `src/CanDoItAll.Migrations.Sqlite/Migrations/20260413144750_AddProcessDefinitionForeignKeys.cs:13-51`

### Still-unprotected tables

Definition-side examples:
- `ProcessStepDependencyDefinition`
- `ProcessStepBranchOutcomeDefinition`
- `ProcessStepRoleAssignmentRequirement`
- `ProcessArtifactExpectation`
- `ProcessStepArtifactInputDefinition`

Runtime-side examples:
- `ProcessRun`
- `ProcessStepRun`
- `ProcessRunAssignment`
- `ProcessWorkBrief`
- `ProcessDecisionRecord`
- `ProcessArtifactRecord`
- `ProcessJournalEntry`
- `ProcessConformanceObservation`
- `ProcessImprovementCandidate`

### Architectural impact

The DB still allows orphan rows and invalid references if a bug, import defect, or direct DB write bypasses the intended service flow.

---

## F003 — Step-dependency uniqueness is still broken for the common `NULL` branch-outcome case

### Why this is still red

The current unique index is:

- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:131-135`

That index is defined on:

- `(StepDefinitionId, DependsOnStepId, DependsOnBranchOutcomeId)`

Because `DependsOnBranchOutcomeId` is nullable, both SQLite and PostgreSQL allow duplicate rows where the nullable column is `NULL`.

### Architectural impact

The schema does **not** actually enforce uniqueness for unconditional dependencies, which are the common case.

### Required correction

Use either:
- split filtered unique indexes for `IS NULL` and `IS NOT NULL`; or
- a normalized non-null route key / route-id strategy.

Do not keep the current nullable composite unique index as the only protection.

---

## F004 — Lifecycle invariants are still assumed in code but not fully enforced in the schema

### Why this is still red

The services assume singular lifecycle facts that the schema does not fully protect.

### Evidence

- `GetWorkingVersionAsync` chooses one version by ordering, not by invariant:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:618-627`
- `GetNextVersionNumberAsync` still uses `MAX + 1`:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:629-635`
- Slug allocation is still pre-check based and race-prone:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:641-677`
- Publish picks the latest draft with `FirstOrDefaultAsync`:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs:226-229`
- Next draft creation still depends on the existing version-number allocator:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs:330-349`
- The version configuration has only a non-unique index on `{ ProcessDefinitionId, Status }`:
  - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs:46-48`
- `ProcessDefinition.ActivePublishedVersionId` exists as a property but is not FK-bound in the model:
  - `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs:34`
  - `src/CanDoItAll.Migrations.Sqlite/Migrations/AppDbContextModelSnapshot.cs:5162-5200`
- `StartRunAsync` trusts `ActivePublishedVersionId` directly:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:21-28`

### Architectural impact

Current code assumes:
- one working draft;
- one current published version;
- a valid active published version pointer;
- conflict-safe version allocation.

Those assumptions are still not fully enforced where they should be strongest: in the schema and transaction model.

---

## F005 — Search/activity side effects still run after the transaction commits, without an outbox boundary

### Why this is still red

The DB commit and the external side effects are not coordinated. If a side effect fails after commit, the mutation may already be durable in the DB while the command reports failure or leaves stale external projections.

### Evidence

- Save commits, then writes search index + activity stream:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:102-141`
- Publish commits, then writes activity stream:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs:66-93`
- Delete commits, then deletes from search index:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs:207-208`
- StartRun commits, then writes activity stream:
  - `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs:192-216`

### Architectural impact

This is an atomicity gap. The module is transaction-safe inside the DB, but not across the system boundary.

---

## F006 — The checked-in proof artifacts do not yet prove the claimed full Process integration matrix

### Why this is still red

The repository contains a broad Process integration test file, but the checked-in integration `.trx` currently proves only three import-metadata tests.

### Evidence

- Checked-in TRX:
  - `.codex-test-results/integration/integration.trx`
- Current Process integration suite file:
  - `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

At review time:
- the `.trx` showed **3 executed tests**;
- `ProcessesServiceIntegrationTests.cs` contains a much larger Process surface.

### Architectural impact

The code may be better than before, but the shipped proof is still weaker than the closure claim.

---

## F007 — Structural concentration is improved, but still heavy

### Why this is still amber

This is no longer the top blocker, but the orchestration surface is still large:

- `ProcessesService*` total: about **3220** lines
- `ProcessWorkspace*` total: about **4913** lines

The new decomposition is real, but it is still mostly a “large façade + partial classes + nested helpers” design.

### Architectural impact

This is acceptable only after the invariant work is safe. It is not the first thing to reopen, but it is still a good late follow-up target.

---

## Summary

The current repo is **better**, but the remaining gaps are still big enough that I do not consider the Process architecture fully signed off.

