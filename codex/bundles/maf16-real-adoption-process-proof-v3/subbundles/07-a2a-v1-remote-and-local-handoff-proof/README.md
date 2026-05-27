# SB07: 07-a2a-v1-remote-and-local-handoff-proof

## Goal

Strengthen A2A v1/handoff proof.

## Required work

- Keep deterministic local handoff smoke.
- Add remote/hosted A2A capability proof if feasible; otherwise guard the path with explicit readiness diagnostics.
- Verify handoff roles/messages are not mutated unexpectedly.
- Verify human-in-the-loop/A2A input-request content behavior if used.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB07` are updated and downstream subbundles can rely on it.
