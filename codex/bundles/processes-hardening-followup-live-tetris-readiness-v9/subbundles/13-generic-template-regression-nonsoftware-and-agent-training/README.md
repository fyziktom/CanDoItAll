# SB13: 13-generic-template-regression-nonsoftware-and-agent-training

## Goal

Keep process core and templates generic, not software-only.

## Work items

- Run typed operation contract audit for all templates in manifest.
- Add or update at least one non-software template example using the same typed governance model.
- Add or draft an agent-improvement/training process template pattern: intake, evaluation, training/rework, validation, approval, deployment/lesson capture.
- Ensure no software-specific runtime logic leaks into generic process services.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- A note explaining how this improves readiness for the real UI-driven Blazor WASM PWA Tetris test.
- A note explaining how generic process behavior remains protected.

## Closure criteria

This subbundle is complete only when its proof manifest is updated and the next subbundle can rely on the result.
