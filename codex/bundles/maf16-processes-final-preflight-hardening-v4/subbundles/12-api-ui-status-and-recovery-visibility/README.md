# SB12: 12-api-ui-status-and-recovery-visibility

## Goal

Ensure operator/API/UI can see invalid recorded artifact states.

## Required work

- Expose status, diagnostic, attempted path, artifact record id, suggested action, and failure ownership.
- Map danger/warning tones for invalid artifact statuses.
- Update process skills/API docs with these states.
- Add component/API tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB12` are filled and the downstream dependency is safe.
