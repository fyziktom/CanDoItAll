# SB07: 07-a2a-handoff-and-workflow-capability-proof

## Goal

Strengthen A2A/handoff/workflow proof.

## Required work

- Run deterministic local handoff smoke.
- Add remote/hosted A2A readiness diagnostic if no remote test environment is configured.
- Verify workflow-backed process step mapping still uses process-owned finalizer.
- Prove A2A v1 package path does not bypass process tool policy.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB07` are filled and the downstream dependency is safe.
