# SB033 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by exposing raw driver responses to a manager, by persisting conformance rows, or by adding a manager command that invokes drivers. The proof must show a manager-visible projection consumes already-produced read-only observations, requires explicit attachment mode, and cannot mutate process state, transitions, finalizers, workspace, storage, or external systems.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- manager projection invokes a driver or creates a runtime host;
- manager projection registers drivers in DI or adds hosted services;
- manager projection writes conformance observations, artifacts, workspace files, storage records, transitions, or finalizer state;
- `None` mode attaches diagnostics or an evidence envelope;
- diagnostics or evidence-envelope mode attaches without a requesting manager identity;
- diagnostics or evidence envelope report process/transition/finalizer mutation as allowed;
- BusinessAnalysis/Office read-only verification accepts external calls or business-record mutation;
- aggregate read-only observations stop proving all responses are mutation-free.

## Semantic Positive Proof

`bundle://proof/SB033/transcripts/focused-manager-readonly-verification-tests.txt` proves the focused P11 matrix passes against current integration-test binaries.

## Anti-Stub Proof

`bundle://proof/SB033/transcripts/anti-stub-manager-readonly-negative-proof.txt` proves attached projection requests reject missing manager identity and the read-only evidence lanes still reject mutation/external-call attempts.

## Raw-Note Closure

- RN-006 remains partially solved: SB033 adds manager-visible read-only verification projection without making drivers execution-capable or mutating process state. Broader orchestration hardening remains planned by SB034-SB036 and SB040-SB045.
- RN-007 remains partially solved: SB033 explicitly does not add a runtime host, registry, selector, DI registration, manager command, scheduler hook, or workflow hook.

## Production Behavior Artifact Matrix

New production signal: `ProcessManagerReadOnlyVerificationProjection` and related internal records/enums. It is projection-only, consumes `ProcessReadOnlyVerificationBatchObservation`, and is covered by focused diagnostics, evidence-envelope, and negative manager-request tests. No UI or public API surface was changed.
