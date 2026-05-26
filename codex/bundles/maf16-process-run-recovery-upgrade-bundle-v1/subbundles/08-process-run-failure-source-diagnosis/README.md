# SB08: 08-process-run-failure-source-diagnosis

## Goal

Diagnose the `StaleOrWrongRun` source cause.

## Required work

- Trace finalizer validation for artifact record `aa9a3e75-8d3e-4757-bafa-be00e8678b8d` through source.
- Identify exactly which condition produced `StaleOrWrongRun`.
- Compare required artifact satisfaction projection versus finalizer-grade validation.
- Inspect path normalization, execution-run binding, workflow-run binding, projected execution run id, recovery execution id, and content hash handling.
- Produce a diagnosis table and failing-first test before fixing.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB08` are updated and the next subbundle can safely depend on it.
