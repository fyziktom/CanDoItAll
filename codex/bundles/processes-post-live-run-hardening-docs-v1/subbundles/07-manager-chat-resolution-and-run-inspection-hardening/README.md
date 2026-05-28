# SB07: 07-manager-chat-resolution-and-run-inspection-hardening

## Goal

Harden manager chat selected-run resolution and inspection context.

## Required work

- Refactor manager resolution into a shared resolver with reason codes and confidence.
- Prefer configured manager and selected-run assignment before fallback.
- Make fallback capability/tag based where possible; keep text scoring as last-resort and explain ambiguity.
- Add manager chat context summary: run health, artifacts, diagnostics, output roots, manager directives, pending approvals.
- Add tests for ambiguity, assigned manager, missing technical agent, completed run, failed run, and pending approval run.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB07` are updated and the next dependent workstream can rely on it.
