# SB05: 05-agent-session-and-stream-error-persistence

## Goal

Prove session restore and stream-error persistence behavior.

## Required work

- Simulate stream/tool error after input was sent.
- Verify serialized session or transcript fallback can continue safely.
- Verify pending approvals still require serialized session or produce clear error.
- Document how this maps to MAF 1.6 stream-error input persistence improvements.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB05` are filled and the downstream dependency is safe.
