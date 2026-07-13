# SB03 Semantic Invariants

## Recovery Classification

- Safe/idempotent completion-gate diagnostics are classified from typed diagnostic codes and flags, not free-form summary text.
- The classifier recognizes product, produced-artifact, ungrounded-completion, required-tool-receipt, and unresolved-blocker completion-gate codes.
- Policy-denied and tool-denied diagnostics are never automatically retried, even if their source result shape is otherwise retryable.
- Unsafe, non-idempotent, and unknown non-completion failures preserve explicit manager escalation.

## Retry Bounds

- Current-step retry is bounded by per-step automatic retry count and same-diagnostic fingerprint count.
- Repeated fingerprint exhaustion routes to `ManagerRequired` with policy `process.current-step-safe-retry-budget-exhausted`.
- Recovery receipts persist diagnostic fingerprint, attempt count, and maximum attempt values so restart/reload behavior remains deterministic.

## Runtime Behavior

- A safe/idempotent completion-gate `NeedsManager` result records the original outcome while applying step status `Ready`.
- The dispatch service only sends manager recovery instructions for committed `Blocked` step state; safe retry does not produce a manager escalation side effect.
- Budget-exhausted completion-gate diagnostics retain manager escalation with root-cause diagnostic context.

## Architecture

- Recovery policy is owned by `CanDoItAll.Processes.Runtime`.
- The runtime classifier has no dependency on `Modules.Processes`, Workbench, MAF, templates, persistence, or file-system services.
- Application and persistence changes only preserve runtime receipts and respect committed runtime state.
- CodeAnalytics snapshot `snap-20260708183408-4375209f` reported no scoped dependency cycles.


## Completed Validator Contract

- Invariant ID: SB03-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB03/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB03/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/03-sb03-recovery-classifier-safe-rework/README.md and bundle://proof/SB03/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.


## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB03 semantic proof metadata | proof/SB03/semantic-invariants.md | proof/SB03/transcripts/00-validator-metadata.txt | final proof closure | proof/SB03/manifest.md rejects missing semantic proof |
