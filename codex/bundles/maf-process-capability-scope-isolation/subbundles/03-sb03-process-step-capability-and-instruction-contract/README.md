# SB03 Process Step Capability And Instruction Contract

## Status

- Status: `Completed`
- Criticality: `Critical process contract foundation`
- Depends on: SB02

## Objective

Add process-neutral per-step contracts for capability directives and scoped instruction fragments, then persist the effective scope on runtime assignments.

## Covered Inputs

- Processes must add specific instructions through process steps.
- Processes must suppress or require tools, skills, MCPs, and runtime providers without editing agent defaults.
- REQ-MAF-006, REQ-MAF-007, REQ-MAF-009, REQ-MAF-012.
- NFR-001, NFR-002, NFR-003.

## Prerequisites

- SB02 enforcement semantics available.
- Read `bundle://architecture/01-csharp-boundary-map.md`.
- Decide exact home for process-neutral contracts before editing persistence.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs`
- `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs`

| Source | Required attention |
| --- | --- |
| `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs` | Template step document fields and authoring summaries. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | `BuildAssignments` and effective launch/step scope construction. |
| `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs` | Add effective runtime scope to assignments. |
| `repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs` | Persist assignment scope. |
| `repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs` | Map assignment scope. |
| `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs` | Save/load scope. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` | Generic process brief should expose scope-aware instruction inputs. |

## Scope

- Define process-neutral scope DTOs with typed directive effects, target kinds, and identifiers.
- Add template document fields for per-step capability scope and scoped instruction fragments.
- Validate unknown effects, unknown target kinds, empty identifiers, conflicting directives, and instruction fragments without valid prerequisites.
- Add effective scope to `ProcessRuntimeStepAssignment`.
- Persist scope through EF entity/config/store.
- Update assignment repair/projection if they expose or rebuild assignment state.

## C# Architecture Impact

This phase touches process template/application/runtime/persistence contracts. Keep the contract independent from MAF wrapper implementation.

## Boundary Ownership

- Process contracts describe intent: deny/require/allow-only by process-neutral target.
- AgentFramework integration later maps that intent to MAF capability selectors.
- Process core should not import `CanDoItAll.AgentFramework.Maf`.

## Dependency Direction

Prefer process-neutral records in `Processes.Contracts` or `Processes.Runtime` depending on existing ownership. If authoring templates and runtime assignments both need them, choose the lowest stable process project already referenced by both without creating cycles.

## Dependency Impact

- Expected impact spans process templates, application launch, runtime assignments, persistence, and tests.
- Downstream SB04 depends on persisted effective scope, not raw template data.

## Pattern Decision

Use typed records and validation services. Avoid launch-variable maps as the primary scope transport.

## Testability Contract

- Template parsing tests for valid and invalid scope.
- Assignment build tests proving effective scope is copied from template to assignment.
- Persistence tests proving scope round-trips.
- Prompt brief tests proving scoped instruction fragments are available but not blindly appended.

## Validation Depth

- Unit tests are mandatory for validation and assignment construction.
- Persistence tests are mandatory if a column or JSON field is added.
- Architecture reference scan is mandatory after project reference edits.

## Partial Class Policy

Do not add more partial files to hide process launch complexity unless there is already an established partial cluster in the target file and the new type remains focused. Prefer top-level contract and validator types.

## Implementation Steps

1. Add typed process runtime scope contracts.
2. Extend template document classes and authoring summaries.
3. Add validation for scope fields.
4. Add effective scope to runtime assignments.
5. Update assignment persistence.
6. Update tests.
7. Capture proof in `proof/SB03/`.

## Do Not Do

- Do not store scope only as raw JSON launch variables.
- Do not reference MAF wrapper implementation from process contracts.
- Do not let process notes double as policy.
- Do not silently ignore unknown directives.

## Acceptance Checklist

- Process template scope is strongly typed and validated.
- Runtime assignments carry effective scope.
- Persistence round-trips scope.
- No forbidden project references are added.
- Scoped instructions are tied to scope validation.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- Production Behavior Artifact Matrix for new template fields, runtime assignment fields, persistence columns, and projection/repair changes.
- Test output.

## Browser Validation Logging

- N/A unless process authoring UI is changed.

## Progression Gate

- SB04 may start only when effective process scope is available on `ProcessRuntimeStepAssignment`.

## Suggested Agent Prompt

```text
Execute SB03 only. Add process-neutral typed step scope and scoped instruction contracts, persist effective assignment scope, and add validation/tests. Do not wire MAF metadata yet except where needed for compile references.
```
