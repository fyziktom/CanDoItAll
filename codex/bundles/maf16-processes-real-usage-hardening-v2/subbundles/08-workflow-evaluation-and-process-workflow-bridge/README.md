# SB08: 08-workflow-evaluation-and-process-workflow-bridge

## Goal

Use MAF 1.6 workflow evaluation expected outputs for deterministic process workflow tests.

## Required work

- Add workflow-backed process step tests using expected output / ground truth where available.
- Verify workflow artifacts map to required process artifacts with explicit mapping fields.
- Ensure Workflows remain executors under Processes, not parallel process state machines.
- Add tests for workflow output mismatch causing process-owned artifact validation failure.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB08` are updated and downstream subbundles can rely on the behavior.
