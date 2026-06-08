# SB037 — Adversarial transcript corpus

## Status
Prepared for implementation.

## Objective
Add tests for huge lines, binary-looking text, repeated secrets, embedded URIs, malicious ANSI/control sequences and log injection.

## Covered Inputs
- Raw request: inspect real code after Codex crash, preserve quality, and prepare broader coherent phases toward stable Core/domain drivers.
- Phase: P13 — Security, Privacy, And Abuse-Resistance Hardening

## Prerequisites
- Previous phase gate must be closed unless this is part of P01.
- Current branch must be `maf-processes-refactor`.
- Do not continue if source or proof contradicts the current-state review.

## Exact Source References
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/reviews/01-execution-report.md`

## Deliverables
- Production/source/test/docs changes required by this subbundle.
- Updated proof material under `proof/SB037/`.
- Updated gate row in `reviews/01-execution-report.md`.

## Dependency Impact
This subbundle affects downstream phases because make verification drivers robust against secret leakage and malicious transcripts. If this proof fails, reopen dependent phases before continuing.

## Validation Depth
Standard. Must include targeted tests or source assertions and must not weaken critical gate assumptions.

## Implementation Steps
1. Re-read exact source references before editing.
2. Implement only the scoped changes.
3. Update or add tests before relying on broad smoke tests.
4. Run targeted validation.
5. Record proof paths and update execution report.

## Scope Exceptions
- Do not implement runtime driver host/registry/selector/DI/manager command.
- Do not move side-effectful process orchestration into Core.
- Do not create UI/browser/mobile proof unless UI files unexpectedly changed; that should fail this subbundle.

## Do Not Do
- Do not use shell execution, package restore, Office/Graph calls, workspace/storage writes, process mutation, claim/transition/finalizer/retry mutation.
- Do not hide runtime behavior behind "verification" names.
- Do not close the subbundle with status-only proof.

## Acceptance Checklist
- [ ] Scoped production/test/docs changes completed.
- [ ] Existing behavior preserved.
- [ ] No forbidden dependency or runtime token drift.
- [ ] Tests/source scans captured.
- [ ] Execution report row updated.

## Proof Required
- Build/test transcript or explicit no-production-change justification.
- Source assertion transcript.
- Anti-stub scan.
- Proof may be shared with the nearest critical gate if no production behavior changed.

## Browser Validation Logging
N/A backend/Core/driver contract work unless UI files unexpectedly change; if they do, fail this subbundle instead of adding mobile/small/medium proof.

## Progression Gate
May proceed only if no critical assumptions are weakened.

## Suggested Agent Prompt
Implement SB037 for `process-driver-runtime-evidence-verifier-integration-hardening-v1`. Keep changes within scope, preserve all previous behavior, and record artifact-backed proof. Stop if any forbidden runtime/Core/driver/UI boundary is crossed.
