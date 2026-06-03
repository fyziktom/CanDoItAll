# SB01 Semantic Invariants

## SB01_INV_001 Missing Governed Operation Contract Fails Closed

Invariant: a governed process step with no declared allowed operations must not execute any tool that has a required non-read process operation.

Implementation points:

- `ProcessToolOperationAuthorizer.Evaluate` now denies missing-operation contexts when the tool requirement list contains at least one required operation.
- The denial reason includes the missing contract state and required operation name so UI/API diagnostics remain actionable.

Proof:

- Failing-first: `bundle://proof/SB01/transcripts/failing-first-authorizer-missing-contract.txt`
- Passing: `bundle://proof/SB01/transcripts/passing-authorizer-missing-contract.txt`
- Source assertion: `bundle://proof/SB01/source-assertions.txt`

Covered tool operation classes:

- `MutateProductTarget`
- `RunValidation`
- `LaunchRuntime`
- `CaptureRuntimeProof`
- `ExecuteExternalAction`

## SB01_INV_002 GovernedLive Run Start Uses Strict Contract Lint

Invariant: a `GovernedLive` process run must be blocked at run start when a risky process step lacks a typed operation contract, even if the published definition is in compatibility contract mode.

Implementation points:

- `ProcessesService.Runtime.RunStart` passes the resolved operating mode into `ResolveEffectiveLintMode`.
- `ResolveEffectiveLintMode` treats `ProcessOperatingMode.GovernedLive` as a strict-lint trigger.

Proof:

- Failing-first: `bundle://proof/SB01/transcripts/failing-first-governed-live-run-start.txt`
- Passing: `bundle://proof/SB01/transcripts/passing-governed-live-run-start.txt`
- Source assertion: `bundle://proof/SB01/source-assertions.txt`

## SB01_INV_003 Contract Lint Remains Typed And Template-Aligned

Invariant: the system must reject missing, partial, inferred, and invalid risky operation contracts under strict lint, preserve compatibility-mode visibility for legacy drafts, and keep shipped templates typed.

Proof:

- Linter contract suite: `bundle://proof/SB01/transcripts/passing-process-definition-linter-contracts.txt`
- Publish contract-mode gates: `bundle://proof/SB01/transcripts/passing-publish-contract-mode-gates.txt`
- Template contract governance: `bundle://proof/SB01/transcripts/passing-template-operation-contracts.txt`

## Shallow-Pass Trap

A shallow fix that only blocks publish-time strict mode would still allow compatibility definitions to start in `GovernedLive`. `StartRunAsync_SB01_INV_001_applies_strict_lint_for_governed_live_runtime` prevents that by publishing the missing-contract definition successfully in compatibility mode, then requiring run-start failure only when `OperatingMode = GovernedLive`.

A shallow fix that only changes run-start lint would still allow direct policy invocations with an empty operation list to pass. `ProcessToolOperationAuthorizer_SB01_INV_001_denies_governed_step_with_missing_operation_contract` prevents that by exercising the policy branch directly across the non-read operation classes.

## Anti-Stub And Artifact Evidence

- Anti-stub audit: `bundle://proof/SB01/anti-stub-audit.txt`
- Changed file hashes: `bundle://proof/SB01/changed-file-hashes.txt`
