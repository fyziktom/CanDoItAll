# SB02 Active Removal, Quarantine, Skeleton Projects, And Boundary Tests

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Remove active old Process implementation after SB01 archive proof, quarantine old tests, create target skeleton projects in corrected dependency order, restore solution health, and add architecture boundary tests before behavior rebuild starts.

## Why This Bundle Exists

This bundle prevents Codex from building a new architecture on top of old runtime/dispatcher coupling. It creates a clean foundation and makes dependency/domain leaks fail early.

## Covered Inputs

- REQ-047: future implementation starts on a new branch.
- REQ-049: old projects/tests removed before rebuild.
- v3 dependency map and hardening gates.

## Context Reset: Read These First

- SB01 execution report and manifest.
- `architecture/11-project-boundary-and-dependency-map.md`
- `plan/02-phase-0-reference-archive-and-removal.md`
- `plan/03-project-by-project-rebuild-plan.md`
- `plan/05-review-checkpoints-and-hardening-gates.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/11-project-boundary-and-dependency-map.md`
- `repo://codex/bundles/process-module-architecture-v3/plan/02-phase-0-reference-archive-and-removal.md`
- `repo://codex/bundles/process-module-architecture-v3/plan/03-project-by-project-rebuild-plan.md`
- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://tests`

## Source Evidence To Use

- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://tests`
- `repo://Templates/Processes`

## Prerequisites

- SB01 complete.
- Archive manifest and hash proof accepted.
- Clean working tree after SB01 commit.

## In Scope

- Remove old active Process projects from solution/source.
- Quarantine or remove old tests that compile against old contracts.
- Clean DI, routes, navigation, EF configuration, scheduler/workflow, workbench, and project-structure references.
- Create skeleton target projects in corrected order.
- Add architecture dependency tests.
- Add vocabulary leak tests.
- Add old-symbol leak tests.

## Out Of Scope

- Do not implement runtime behavior.
- Do not port old dispatcher behavior.
- Do not migrate templates.
- Do not rebuild UI behavior.

## Target Projects / Files

- `CanDoItAll.slnx`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Abstractions`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Drivers.Abstractions`
- `src/CanDoItAll.Processes.Projections`
- `src/CanDoItAll.Git`
- `src/CanDoItAll.Processes.Templates`
- `src/CanDoItAll.Processes.Builder`
- `src/CanDoItAll.Processes.Runtime`
- `src/CanDoItAll.Processes.Persistence`
- `src/CanDoItAll.Processes.Application`
- architecture test files under `tests`

## Deliverables

- Old active Process code removed or quarantined.
- New skeleton projects compile.
- Boundary tests exist.
- Old-symbol search proof exists.

## Expected Deliverables

- Build restored without old dispatcher.
- Dependency tests fail if core/runtime/UI boundaries are violated.
- Vocabulary leak tests fail if domain terms enter generic contracts.

## Dependency Impact

- Every later subbundle depends on this clean boundary.
- Build may be restored in the same commit as removal if repository policy requires every commit to build.

## Validation Depth

- Validate with build, architecture dependency tests, vocabulary leak tests, old-symbol searches, and explicit review of removed/quarantined surfaces.

## Architecture Invariants That Must Hold

- Core has no EF/Razor/UI/concrete driver references.
- Runtime has no UI or concrete driver references.
- UI skeleton does not query EF runtime entities.
- Old dispatcher symbols remain only in archive/migration input.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Confirm SB01 archive proof.
2. Remove old active projects and solution references.
3. Quarantine or remove old tests.
4. Clean registrations and integration references.
5. Add skeleton projects in corrected dependency order.
6. Add architecture tests.
7. Restore build.
8. Run old-symbol and domain-leak searches.

## Refactoring Review Checkpoint

- Verify no compatibility service reintroduces old runtime semantics.
- Verify skeletons are minimal and do not fake behavior.
- Verify tests protect boundaries before behavior implementation.

## Required Tests / Proof

- Solution build or explicit build-scope proof.
- Architecture dependency tests.
- Domain vocabulary leak tests.
- Old-symbol search proof.

## Search Proof

Search for old dispatcher/runtime symbols listed in `plan/05-review-checkpoints-and-hardening-gates.md`.

## Stop And Report Conditions

- Stop if build can only be restored by keeping old dispatcher/runtime semantics.
- Stop if old tests require old contracts rather than quarantine/rewrite.
- Stop if removal reveals hidden dependencies too broad to handle safely.

## Do Not Do

- Do not wrap `ProcessRunAutomationDispatchService`.
- Do not recreate a giant partial dispatcher under a new name.
- Do not keep old runtime entities as target contracts.
- Do not delete `Templates/Processes`.

## Acceptance Checklist

- [ ] Old active Process source removed/quarantined.
- [ ] Old tests removed/quarantined or explicitly marked legacy reference.
- [ ] Skeleton projects created.
- [ ] Build restored through skeleton boundaries.
- [ ] Boundary, vocabulary, and old-symbol tests exist.

## Proof Required

- Build output.
- Test output.
- Search proof.
- Refactoring review result.

## Browser Validation Logging

- Browser validation is not required because UI behavior is not rebuilt in this bundle.

## Progression Gate

- SB03 may start only when old active implementation symbols are absent outside allowed reference/migration locations.

## Suggested Agent Prompt

Execute SB02 from `codex/bundles/process-module-architecture-v3/subbundles/02-active-removal-quarantine-skeleton-boundaries`. Remove old active Process code after SB01 proof, create skeleton boundaries, and add boundary tests. Do not restore build by reviving old semantics.

## Handoff Notes For Next Bundle

Record skeleton project paths, test names, remaining approved legacy references, and any blocked integration references for SB03.
