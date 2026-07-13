# 03-process-template-and-branch-signal-drift

## Status

- `Completed`

## Objective

Repair process-template and branch-signal test failures while preserving the actual process-runtime semantics.

## Covered Inputs

- RH-005: brittle or obsolete process-template assertion around repair validation wording.
- RH-006: completed process outputs that declare a branch outcome no longer emit expected manager branch signals in three tests.

## Prerequisites

- Evidence exists: `bundle://evidence/targeted-failing-tests.txt`.
- No dependency on SB01/SB02 for local edits, but SB05 depends on this proof.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/steps/feature-repair.md`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/steps/targeted-validation.md`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/steps/targeted-recheck.md`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`

## Deliverables

- Process-template tests assert durable invariants instead of stale wording, or template text is restored if the invariant is actually missing.
- Branch outcome recovery emits `ProcessBranchSignalCodes.Outcome(...)` for declared, unambiguous branch outcomes in completed process output.
- Ambiguous/invalid branch outcome text remains rejected.

## Dependency Impact

- Process automation and dispatch depend on branch signals. Weak proof here can make completed subprocesses fail to route follow-up work.

## Validation Depth

- Process-critical semantic foundation.

## Implementation Steps

1. Run the failing process-template and process-runtime tests as a focused failing-first transcript.
2. For the template assertion, identify the invariant behind the missing phrase and decide whether the test or template is stale.
3. For branch-signal tests, trace why `ManagerSignals` is empty despite output text containing valid branch keys.
4. Repair production recovery logic or test setup according to the traced cause.
5. Add or confirm negative proof for multiple/different branch keys and undeclared keys.

## Scope Exceptions

- Do not redesign process templates outside the named dotnet feature implementation template.
- Do not change process dispatch behavior beyond branch-signal recovery unless a failing test proves it.

## Do Not Do

- Do not remove branch-signal tests just because the parser is strict.
- Do not assert only exact prose when behavior-level assertions are available.

## Acceptance Checklist

- [x] `ProcessDefinitionCatalogProjectionTests.Dotnet_feature_code_change_keeps_browser_proof_out_of_atomic_targeted_validation_step` passes.
- [x] The three failing `ProcessRuntimeIntegrationAdapterTests` pass.
- [x] Ambiguous branch outcome text remains rejected.
- [x] Execution report records whether the root cause was stale test setup, template drift, or production recovery regression.

## Proof Required

- Failing-first transcript: `proof/SB03/failing-process-tests.txt`.
- Passing transcript: `proof/SB03/passing-process-tests.txt`.
- Semantic proof note: explicit line, heading-plus-next-line, and inferred-title positive cases plus at least one invalid/ambiguous negative case.
- Source assertion for changed template or parser files.

## Browser Validation Logging

- N/A. Backend/process-runtime tests only.

## Progression Gate

- SB05 cannot close while process branch-signal recovery tests fail.

## Suggested Agent Prompt

```text
Implement SB03 only. Preserve process branch-routing semantics; repair stale prose assertions only when behavior remains covered.
```
