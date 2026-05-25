# SB01 - Explicit step operation contract and classifier hardening

## Status

Ready.

## Objective

Add an explicit generic step operation contract and harden the classifier so artifact production is not confused with product mutation.

## Covered Inputs

RQ01, RQ02, N005, N006, N007

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Create or extend a generic `ProcessStepOperationContract` model in the process runtime layer.
2. Define generic operations such as ReadProcessContext, WriteManagedProcessArtifacts, MutateProductTarget, RunValidation, CaptureRuntimeProof, ExecuteExternalAction, RecoverArtifactsOnly.
3. Make explicit contract fields override heuristic inference.
4. Change `ResolveProcessStepExecutionBoundary` so broad verbs like create/write/generate do not imply product mutation unless target scope or artifact kind says product mutation.
5. Add tests where a Work step creates an architecture record, business report, legal decision, and research summary without receiving product mutation permission.
6. Add tests where an implementation step still receives product mutation permission.
7. Update linter to warn when classifier confidence is low or when explicit contract is missing for ambiguous steps.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Architecture/report/decision Work steps that create artifacts are not ProductMutation.
- [ ] Implementation/scaffold/repair steps still become ProductMutation when explicitly modeled.
- [ ] Explicit operation contract beats text heuristic.
- [ ] No Blazor/.NET-specific assumptions are needed for the classifier.

## Proof Required

Create/update:

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- `proof/SB01/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB01/transcripts/passing.txt`
- `proof/SB01/transcripts/source-assertions.txt`
- `proof/SB01/transcripts/anti-stub-audit.txt`
- `proof/SB01/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB01 - Explicit step operation contract and classifier hardening` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
