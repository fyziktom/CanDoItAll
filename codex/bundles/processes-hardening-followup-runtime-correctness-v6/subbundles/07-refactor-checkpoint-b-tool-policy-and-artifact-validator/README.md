# SB07: Refactor policy and artifact validation after SB05-SB06.

## Objective

Refactor policy and artifact validation after SB05-SB06.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract `ProcessToolOperationAuthorizer`.
- Extract `ProcessScriptSideEffectAnalyzer`.
- Extract `ProcessCompletionArtifactValidator`.
- Extract `ProcessArtifactIdentityService`.
- Ensure unit tests cover services without full MAF runtime.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN02 complete with weak/manual artifact validation.
- RN04 allow script-based side effects through imperfect regex inspection.
- RN09 add refactoring checkpoints every few subbundles.
- RQ03, RQ05, RQ11.

## Prerequisites

- SB05 and SB06 closure gates pass.
- SB03 shared validator remains trusted.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Deliverables

- Extracted policy authorizer, script side-effect analyzer, completion artifact validator, and artifact identity service where the codebase shape supports it.
- Unit tests that target extracted policy/validator services without full runtime orchestration.
- Architecture update for checkpoint B.

## Dependency Impact

- SB08 depends on a clean artifact validator boundary.
- SB10 and SB11 depend on policy decisions being testable without full agent runtime.

## Validation Depth

- Focused tests for extracted services.
- Source assertion for production call paths using extracted services.
- Anti-stub audit for unused extracted classes or duplicate logic.

## Implementation Steps

- Extract cohesive policy and artifact-validation logic without widening behavior.
- Redirect production runtime paths to extracted services.
- Add direct tests for extracted services where practical.
- Update architecture/refactoring checkpoint notes.
- Record proof under `bundle://proof/SB07/`.

## Do Not Do

- Do not introduce parallel implementations that drift from production behavior.
- Do not extract interfaces that only obscure simple static/domain logic.
- Do not change SB05/SB06 semantics during checkpoint cleanup without reopening their proof.

## Acceptance Checklist

- Policy and artifact validation code is more modular and production-callable.
- Focused tests pass after extraction.
- Architecture notes describe the new boundaries.
- Downstream SB08 can depend on the validator boundary.

## Proof Required

- `bundle://proof/SB07/manifest.md`
- `bundle://proof/SB07/semantic-invariants.md`
- Passing focused test transcript.
- Source assertion transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB07 is a non-UI refactoring checkpoint.

## Progression Gate

- Passed. SB08 may start after checkpoint B proved policy and artifact-validation extraction did not weaken SB03/SB05/SB06 behavior.

## Completion Notes

- Extracted `ProcessToolOperationAuthorizer`, `ProcessScriptSideEffectAnalyzer`, `ProcessCompletionArtifactValidator`, and `ProcessArtifactIdentityService`.
- Redirected production policy, artifact validation, and artifact identity call paths to the extracted services.
- Added direct tests for the extracted policy/analyzer/validator/identity services without full MAF runtime orchestration.
- Included a generic/non-software artifact validator case with a board-meeting decision memo.
- Focused unit and integration tests passed; no SQLite runtime or migration dependency was introduced.

## Suggested Agent Prompt

- Execute checkpoint B with minimal extraction, update architecture proof, rerun focused tests, and record SB07 gate closure.
