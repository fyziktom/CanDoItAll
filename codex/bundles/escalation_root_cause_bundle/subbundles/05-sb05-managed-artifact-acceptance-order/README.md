# SB05 - Managed Artifact Acceptance Order

## Status

- `Completed`
- Critical foundation: yes

## Objective

Separate structured finalizer validity, staged artifact materialization, completion-gate acceptance, and produced-slot promotion. A managed artifact must not be advertised as runtime-accepted or promoted to a parent/consumer context until completion gates pass.

## Covered Inputs

- GPTPro managed artifact acceptance order finding.
- REQ-009, REQ-010, REQ-017, REQ-018, REQ-020.
- User concern that artifact templates can have the same false-completion class.

## Prerequisites

- SB02 aggregate gate result available.
- Current managed artifact and MAF source references refreshed.
- Artifact template inventory reviewed.

## Exact Source References

- `bundle://codex/06-managed-artifact-acceptance-order.md`
- `bundle://inventories/03-artifact-template-inventory.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/business-plan.json`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`

## Deliverables

- Explicit lifecycle states or equivalent records for structured finalizer valid, artifact staged, completion gates accepted, and produced slot promoted.
- Runtime wording changed from premature "Runtime Validated Structured Outcome" acceptance to neutral staged/finalizer wording before gates pass.
- Produced artifact slot promotion occurs only after aggregate completion gates pass.
- Rejected staged artifacts remain inspectable as evidence but are not accepted outputs.
- Tests for successful acceptance, rejected gate, and parent/consumer visibility.

## Dependency Impact

- SB06 relies on accepted slots for subprocess bridging.
- SB09 relies on this distinction when auditing artifact templates.
- SB12 cannot close if artifacts are still accepted by structure or file existence alone.

## Validation Depth

- Critical foundation with unit/integration tests and artifact acceptance negative proof.
- Semantic proof must show runtime wording and produced slots reflect the same acceptance state.

## Implementation Steps

1. Trace current artifact materialization and wording order in adapter and MAF finalizer code.
2. Identify the exact point where finalizer parse success becomes staged evidence.
3. Add or reuse typed lifecycle state to represent staged versus accepted versus promoted.
4. Move produced-slot promotion after completion gate success.
5. Preserve rejected staged artifact evidence for diagnostics without treating it as accepted output.
6. Update runtime/projection text to avoid claiming acceptance before gates pass.
7. Add tests where finalizer schema is valid but product completion gate fails.
8. Add tests where gates pass and artifact is promoted.
9. Add tests where parent/subprocess bridge cannot consume rejected staged output.
10. Update artifact template audit criteria to reference the new lifecycle.

## Do Not Do

- Do not delete staged artifacts needed for diagnostics.
- Do not rename wording only while leaving slot promotion behavior unchanged.
- Do not treat finalizer schema validity as process semantic completion.
- Do not accept physical file existence as a produced artifact slot.

## Acceptance Checklist

- [x] Valid finalizer output can be staged without being accepted.
- [x] Completion gate failure prevents produced-slot promotion.
- [x] Completion gate success promotes the produced slot.
- [x] Runtime/projection wording distinguishes staged from accepted.
- [x] Rejected artifact evidence remains visible for debugging.
- [x] Tests cover artifact template false-completion risk.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- Failing-first artifact accepted-before-gates test.
- Passing staged/accepted/promoted tests.
- Source assertions for lifecycle state and promotion location.
- Production Behavior Artifact Matrix if new lifecycle records/states are introduced.

## Browser Validation Logging

- `N/A` unless UI wording is changed; if UI changes, capture the affected process detail route.

## Progression Gate

- SB06 and SB09 may proceed only after accepted artifact truth is distinct from staged/finalizer-valid output.

## C# Architecture Impact

Clarifies artifact lifecycle and prevents adapter/MAF finalizer structure from owning process semantic completion.

## Boundary Ownership

MAF owns finalizer schema validity. Process runtime/adapter owns completion gate acceptance and produced-slot promotion.

## Dependency Direction

MAF must not depend on process runtime acceptance semantics.

## Pattern Decision

Use explicit lifecycle records/states; no new pattern unless current code already has a lifecycle abstraction to extend.

## Testability Contract

Tests must assert produced-slot visibility, not just text.

## Partial Class Policy

Avoid expanding managed artifact partials beyond thin plumbing; extract lifecycle decisions where possible.

## Architecture Proof Required

- Show where lifecycle state is set and consumed.
- Show a negative test for finalizer-valid but gate-rejected output.

## Suggested Agent Prompt

```text
Execute SB05 only. Separate finalizer validity, staged artifact evidence, completion gate acceptance, and produced-slot promotion. Prove rejected artifacts are not accepted outputs and update wording accordingly.
```
