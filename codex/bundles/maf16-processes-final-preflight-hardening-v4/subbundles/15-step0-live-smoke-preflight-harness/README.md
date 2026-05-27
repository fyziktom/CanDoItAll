# SB15: 15-step0-live-smoke-preflight-harness

## Goal

Run controlled step0 smoke, not full live process.

## Required work

- Use live-run profile with a disposable/project-safe target.
- Run only `Resolve Blazor delivery contract` through automation.
- Verify finalizer result, read model, diagnostics, artifact content, content hash, and tool receipts agree.
- Abort if any mismatch appears.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB15` are filled and the downstream dependency is safe.
