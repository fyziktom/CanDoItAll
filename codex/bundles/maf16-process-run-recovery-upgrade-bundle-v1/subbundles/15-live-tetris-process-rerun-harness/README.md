# SB15: 15-live-tetris-process-rerun-harness

## Goal

Rerun the live Tetris/Blazor process after fixes.

## Required work

- Use the live-run profile, not seeded baseline transitions/artifacts.
- Start the process and verify step 0 completes with a valid current-run delivery contract artifact.
- Verify implementation step is the first step allowed to mutate product files.
- Verify validation step cannot mutate product files and captures runtime/browser evidence.
- Capture run detail, step details, artifacts, execution logs, tool receipts, and diagnostics as proof.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB15` are updated and the next subbundle can safely depend on it.
