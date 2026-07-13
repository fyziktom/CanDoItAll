# Artifact Lineage And Connected Input Contract

## Status

- `Completed`

## Objective

Represent connected process artifacts as concrete input packages and lineage facts, not only available slot ids, so downstream steps can consume artifacts from any connected prior step and recovery can route missing artifacts to the responsible owner.

## Covered Inputs

- R02, R03, R06, R08, R11, R14
- US02, US05, US07
- EX01, EX02, EX03, EX04, EX10, EX15
- Architect notes about connected artifacts across canvas edges, non-direct prior steps, lost artifacts, and retry returning to the previous producing step.

## Prerequisites

- SB01 progression gate passed.
- Current artifact slot and scheduler behavior is characterized.
- No downstream finalization/recovery code depends on a placeholder contract.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Core/ProcessArtifactModels.cs`
- `repo://src/Processes/CanDoItAll.Processes.Core/ProcessGraphKernel.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessTemplateKernelBuilder.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessInstancePlan.cs`
- `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessInstancePlanCompiler.Builders.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceMappers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessArtifactLedgerStore.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs`

## Deliverables

- Strongly typed connected artifact lineage model.
- Durable per-step required input package or equivalent runtime facts.
- Launch/plan validation for invalid artifact connections.
- Scheduler/readiness changes that require concrete satisfiable connected artifacts, not only slot availability.
- Persistence/projection updates for new lineage facts.
- Tests for direct, non-direct, branch, missing, unreadable, and invalid connected artifacts.

## Dependency Impact

- SB04 finalization depends on this contract.
- SB05 recovery routing depends on producer/consumer lineage.
- SB06 driver policy depends on the generic artifact boundary.
- SB07 context packaging depends on concrete artifact refs and retrieval handles.
- Weak lineage proof invalidates later gates.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Define the minimal generic lineage facts required by finalization, recovery, and context packaging.
2. Place pure definitions in Core only when they are not runtime-state facts; place runtime facts in Runtime.
3. Preserve artifact connections from template/kernel/plan into runtime state at launch.
4. Add validation so invalid declared connections fail before run or block launch with explicit diagnostic.
5. Update scheduler/readiness to require concrete connected artifact satisfiability.
6. Update persistence/projections minimally for durable facts.
7. Add failing-first tests, then passing implementation.
8. Update proof manifest and execution report.

## Scope Exceptions

- Does not implement finalizer logic; SB04 consumes lineage.
- Does not implement recovery router; SB05 consumes lineage.
- Does not implement context budget policy; SB07 consumes lineage.

## Do Not Do

- Do not model lineage as strings or untyped dictionaries.
- Do not assume a required artifact comes only from the direct previous step.
- Do not mark a slot available as sufficient proof when concrete artifact ref/readback is absent.
- Do not introduce domain-specific artifact categories into Runtime.

## Acceptance Checklist

- A consumer step can resolve concrete artifacts by required slot and connected producer.
- A consumer step can consume an artifact from an earlier non-direct step.
- Missing connected artifact does not make the consumer ready.
- Invalid template/plan connection fails predictably.
- Persistence round-trips the lineage facts required by finalization and recovery.

## Proof Required

- `bundle://proof/SB02/manifest.md` with changed-file hashes, commands, and artifact paths.
- `bundle://proof/SB02/semantic-invariants.md` describing lineage invariants.
- Failing-first test for missing connected input where consumer is not retried.
- Passing test for A produces, B unrelated, C consumes A.
- Passing test for invalid artifact connection rejection.
- Source assertions that Runtime remains domain-neutral.

## Browser Validation Logging

- Route: `N/A unless lineage is surfaced in UI/projections`
- Viewports: if UI touched, large desktop plus affected responsive width
- Playwright evidence: required only if UI/projection touched
- Screenshots: record concrete paths if UI/projection touched
- Review questions: can an operator see the producer/consumer artifact relationship without exposing sensitive content?

## Progression Gate

- SB04, SB05, and SB07 may proceed only when connected artifact lineage is durable.
- Production paths must populate lineage.
- Tests must prove non-direct prior-step artifact consumption.

## C# Architecture Impact

Introduces foundational contracts. This is high-impact and must keep generic process vocabulary clean.

## Boundary Ownership

Core owns pure artifact identifiers/definitions. Runtime owns runtime lineage state and readiness decisions. Application owns launch orchestration. Persistence owns storage mapping. Projections own read models.

## Dependency Direction

No Runtime dependency on Application, Persistence, Projections, Modules, or AgentFramework. No cycles.

## Pattern Decision

Use a typed ledger/state model, not a string-key lookup or prompt-derived artifact map.

## Testability Contract

Runtime lineage behavior must be unit-testable without Module integration. Launch/persistence integration tests must prove production population.

## Partial Class Policy

Do not add new runtime partial files as the final structure. If `ProcessRuntimeEngine` is touched, move lineage behavior toward a cohesive service or helper with focused tests.

## Architecture Proof Required

- Dependency graph or source assertion proof.
- Source assertion that new Runtime contracts do not mention AgentFramework, MAF, .NET delivery, browser, or project-structure concepts.
- Anti-stub audit showing tests use launch/runtime population where critical.

## Suggested Agent Prompt

```text
Implement SB02 only. Add generic connected artifact lineage and concrete input package facts through production plan/launch/runtime paths. Prove non-direct connected artifact consumption and missing-input blocking. Do not implement finalization or recovery routing beyond the minimum needed to expose lineage.
```
