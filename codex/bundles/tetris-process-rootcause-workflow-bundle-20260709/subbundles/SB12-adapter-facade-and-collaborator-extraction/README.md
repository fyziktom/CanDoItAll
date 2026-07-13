# SB12 Adapter Facade And Collaborator Extraction

## Status

- `Completed`

## Objective

Replace the `AgentFrameworkProcessExecutionAdapter` partial cluster with one thin non-partial boundary and cohesive top-level collaborators for orchestration, subprocess coordination, runtime-owned execution, completion coordination, managed artifacts, completion policy, recovery classification, and result conversion.

## Covered Inputs

- `bundle://inputs/03-architecture-refactor-request.md`
- Reopened SB01 architecture claim.

## Prerequisites

- Fresh CodeAnalytics snapshot `snap-20260709195146-c1b7a73e` is healthy enough for corrective orientation.
- Existing adapter behavior tests establish characterization coverage.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateFactory.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeArchitectureBaselineTests.cs`

## Deliverables

- Exactly one non-partial adapter type containing boundary delegation only.
- Top-level collaborators with one reason to change and explicit constructor dependencies.
- No nested provider/strategy/service types used as architecture boundaries.
- Existing behavior preserved.

## Dependency Impact

- No project-reference change is planned.
- SB13 depends on the new completion/policy seams.

## Validation Depth

- Critical foundation with failing-first source assertion, focused characterization tests, direct collaborator tests, DI composition smoke, module build, and refreshed CodeAnalytics proof.

## Implementation Steps

1. Record before member/file/dependency inventory.
2. Extract top-level models and pure policies first.
3. Extract stateful artifact, subprocess, completion, and agent-execution services.
4. Reduce the adapter to interface descriptors and delegation.
5. Remove every adapter partial file and duplicate behavior.
6. Register collaborators explicitly.

## C# Architecture Impact

This is a local extraction in `CanDoItAll.Modules.Processes`; external MAF SDK and module composition stay at the outer boundary.

## Boundary Ownership

- Adapter: process-driver boundary only.
- Agent execution service: MAF invocation orchestration.
- Completion coordinator: completion workflow only.
- Artifact service: workspace managed-artifact lifecycle only.
- Subprocess coordinator: subprocess launch/bridge only.
- Pure policies: parsing, receipt evaluation, branch inference, and diagnostic classification.

## Dependency Direction

Collaborators may depend on MAF/process abstractions already referenced by the module. Generic process projects must not reference the module or Workbench.

## Pattern Decision

Thin facade plus cohesive extracted services. Rejected: partial files, nested helpers, a replacement manager class, or service location.

## Testability Contract

Direct tests must instantiate extracted collaborators without constructing `AgentFrameworkProcessExecutionAdapter` or a full host. A composition smoke proves the adapter uses the extracted executor.

## Partial Class Policy

Zero permanent adapter partial declarations are allowed. No migration partial may remain at closure.

## Architecture Proof Required

- Before/after member and line-count inventory.
- Source assertion for exactly one non-partial adapter declaration and no `AgentFrameworkProcessExecutionAdapter.*.cs` files.
- Direct collaborator tests and DI smoke.
- Refreshed snapshot/member/dependency evidence.

## Do Not Do

- Do not move all behavior into one renamed executor or manager.
- Do not alter process semantics unless a characterization test exposes an existing defect.
- Do not add new project references in this phase.

## Acceptance Checklist

- Adapter is thin and non-partial.
- Each extracted type has one clear reason to change.
- Existing adapter regressions pass.
- Direct collaborator and negative architecture tests pass.

## Proof Required

- `bundle://proof/SB12/manifest.md`
- `bundle://proof/SB12/semantic-invariants.md`
- Failing-first and passing transcripts, changed-file hashes, source assertions, anti-stub audit, build/test transcripts, and refreshed CodeAnalytics transcript.

## Browser Validation Logging

- N/A; backend architecture phase.

## Progression Gate

- SB13 may start only after the architecture review gate passes with no remaining adapter partial and no replacement monolith.

## Suggested Agent Prompt

Remove the adapter partial-class architecture completely, preserve behavior, and prove the extracted collaborators independently.
