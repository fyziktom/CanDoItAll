# SB08: 08-maf16-agent-tool-skill-runtime-regression-closure

## Goal

Close MAF/tool/skill proof debt from prior blockers.

## Required work

- Split timed-out broad integration into smaller named tests.
- Re-run/prove tool-loop, context provider, finalizer, session/stream error, approval, MCP, A2A/handoff, workflow mapping, and trace correlation slices.
- Record which MAF 1.6 features are adopted, fallbacked, or deferred.
- Update Agent Framework docs/skills with current decisions.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB08` are updated and the next dependent workstream can rely on it.
