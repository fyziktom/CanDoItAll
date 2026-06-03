# SB01 Fail-closed process operation contracts

## Status

Completed.
Critical foundation: **Yes**

## Objective

Make process operation contracts explicit, strict, and fail-closed for governed live runs. Missing or invalid contracts must block execution before any mutation, validation, launch, browser interaction, or external action tool can run.

## Covered Inputs

R01, R02; source evidence E02, E03; user request to find skipped hardening.

## Prerequisites

Prepared bundle validation passed. Read `ProcessToolOperationAuthorizer`, `ProcessStepOperationContractState`, process template definitions, and run-start/dispatch code.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessToolOperationAuthorizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`

## Deliverables

- `IProcessStepOperationContractResolver` or equivalent cohesive resolver.
- Strict contract result state: Resolved, Missing, Invalid, LegacyImplicit, MigrationRequired.
- GovernedLive runtime block for Missing/Invalid/MigrationRequired.
- Template lint/migration report for all shipped process definitions.
- UI/API diagnostics for missing contracts.
- Regression tests proving missing contracts deny mutation, validation, launch, browser interaction, and external actions.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Inventory all process definitions and step kinds that currently rely on default/missing operation contracts.
2. Add a resolver with explicit status and diagnostic code; do not hide missing contract inside normalization.
3. Update process publish/import/start to lint strict contracts and produce actionable errors.
4. Change runtime policy context so a governed process step with missing operations cannot proceed as if unrestricted.
5. Add compatibility migration for old drafts, but keep `GovernedLive` strict by default.
6. Update templates with explicit `AllowedOperations` and `OperationTargetScope` for every automation-relevant step.
7. Add failing-first and passing tests.

## Scope Exceptions

None. Existing compatibility-mode behavior remains visible at publish time, but `GovernedLive` now forces strict lint before execution.

## Do Not Do

Do not silently infer broad operations for governed live runs. Do not mark old templates valid by adding catch-all `ExecuteExternalAction` unless the step truly owns external actions.

## Acceptance Checklist

- [x] Source references were reopened before editing.
- [x] Implementation is the smallest correct change set for this subbundle.
- [x] Failing-first proof was captured for behavior-changing critical work.
- [x] Passing proof was captured after implementation.
- [x] Anti-stub audit was run.
- [x] Raw notes owned by this subbundle were closed or explicitly blocked.
- [x] Downstream dependency impact was reviewed before moving on.

## Proof Required

Unit and integration tests: missing operations block all non-read process tools; invalid operation/scope combinations block publish or start; all shipped templates pass strict lint; non-process legacy runs remain unaffected.

## Browser Validation Logging

N/A for core behavior. If UI diagnostics are touched, add `/processes` or run-detail screenshots under SB08.

## Progression Gate

No downstream SB04 process E2E may start until strict contract tests and template lint pass.

## Suggested Agent Prompt

You are implementing `SB01 Fail-closed process operation contracts` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
