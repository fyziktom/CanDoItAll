# SB07: 07-a2a-v1-and-handoff-regression

## Goal

Prove or explicitly guard A2A v1 and handoff behavior.

## Required work

- Compile and test A2A v1 package usage.
- Verify handoff message role mutation fix does not break existing workarounds.
- Add smoke tests for local handoff workflow and any remote A2A bridge if configured.
- If A2A runtime cannot be fully tested locally, gate it behind clear feature readiness diagnostics.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB07` are updated and downstream subbundles can rely on the behavior.
