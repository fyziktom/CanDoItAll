# SB06 Instance Builder And Immutable Plan Compiler

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Implement the builder as a compiler from template/definition/run context to immutable `ProcessInstancePlan`, including resolved drivers, strategy bindings, artifact plan, branch route table, subprocess plans, manager/recovery/monitoring/security plans, and plan hash.

## Why This Bundle Exists

Runtime reliability depends on explicit composition. If the builder does not produce a complete plan, runtime and dispatcher will rediscover behavior and recreate old coupling.

## Covered Inputs

- REQ-010 through REQ-014.
- REQ-011 full composition list.
- v3 branch and driver details.

## Context Reset: Read These First

- SB04 and SB05 execution reports.
- `architecture/04-builder-and-instance-composition.md`
- `architecture/11-project-boundary-and-dependency-map.md`
- `architecture/13-branch-switch-and-loop-contract.md`
- `architecture/12-runtime-persistence-event-store-and-outbox.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/04-builder-and-instance-composition.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/11-project-boundary-and-dependency-map.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/13-branch-switch-and-loop-contract.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs`

## Source Evidence To Use

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs`
- SB01 archive for run start/subprocess validation concepts.

## Prerequisites

- SB04 complete.
- SB05 complete.
- Template and driver contracts available.

## In Scope

- Builder pipeline.
- Schema migration invocation.
- Global component resolution.
- Local override conflict handling.
- Graph/artifact/branch validation.
- Driver stack selection.
- Strategy binding snapshots.
- Subprocess recursive plan creation.
- Artifact slots and initial ledger plan.
- Manager/recovery/monitoring/security plan sections.
- Plan hash and diagnostics.
- Persisted plan contract through ports/application boundary.

## Out Of Scope

- No runtime state execution.
- No concrete strategy invocation.
- No EF implementation.
- No UI.

## Target Projects / Files

- `src/CanDoItAll.Processes.Builder`
- related tests.

## Deliverables

- Builder/compiler implementation.
- Immutable plan model.
- Golden plan tests.
- Failure diagnostic tests.

## Expected Deliverables

- Runtime cannot start without a persisted plan.
- Every executable step has a strategy binding.
- Subprocesses are planned recursively.
- Backward branches have loop budgets.

## Dependency Impact

- SB07 runtime depends on plan model and persisted plan contract.
- SB09 manager depends on manager/recovery/branch plan sections.

## Validation Depth

- Validate with golden plan tests, missing binding failures, driver conflict tests, subprocess recursion tests, branch budget tests, plan hash tests, and dependency scans.

## Architecture Invariants That Must Hold

- Builder uses driver catalog contracts, not concrete drivers.
- Builder does not execute strategies.
- Plan is immutable after persistence.
- Missing strategy is a build failure.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Implement compile request and result models.
2. Implement pipeline stages.
3. Implement driver stack and strategy binding.
4. Implement artifact/branch/subprocess plan builders.
5. Implement plan hash.
6. Implement persistence handoff contract.
7. Add golden and negative tests.

## Refactoring Review Checkpoint

- Split pipeline stages by responsibility.
- Keep graph/artifact/branch validators pure.
- Keep diagnostics structured and testable.

## Required Tests / Proof

- Golden plan tests.
- Missing strategy negative tests.
- Driver conflict tests.
- Subprocess depth/cycle tests.
- Branch backward-route budget tests.
- Plan hash stability tests.

## Search Proof

- Search Builder for UI references.
- Search Builder for concrete driver references.
- Search Runtime for composition logic after SB07 begins.

## Stop And Report Conditions

- Stop if runtime must compose missing plan details.
- Stop if concrete drivers are needed in Builder.
- Stop if subprocess planning cannot be represented recursively.

## Do Not Do

- Do not execute strategies in Builder.
- Do not persist mutable runtime state as part of plan semantics.
- Do not allow runtime fallback strategy selection.

## Acceptance Checklist

- [ ] Builder pipeline implemented.
- [ ] Strategy bindings persisted in plan.
- [ ] Artifact, branch, subprocess, manager, monitoring, and security plan sections exist.
- [ ] Golden and negative tests pass.

## Proof Required

- Test output.
- Plan snapshot examples.
- Dependency scan.

## Browser Validation Logging

- Browser validation is not required because no UI behavior is implemented.

## Progression Gate

- SB07 may start after immutable plan tests and missing-binding failure tests pass.

## Suggested Agent Prompt

Execute SB06 from `codex/bundles/process-module-architecture-v3/subbundles/06-instance-builder-and-plan-compiler`. Implement the builder as compiler and persist complete immutable plans. Do not let runtime rediscover semantics.

## Handoff Notes For Next Bundle

Record plan schema, plan hash behavior, failure diagnostics, and fake plan fixtures for SB07.
