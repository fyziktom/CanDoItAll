# SB08: 08-agent-assignment-and-tool-profile-validation

## Goal

Validate agents before execution starts.

## Work items

- Add launch-plan or run-start validation that checks required role assignments and required tool/skill availability.
- Ensure missing Blazor/PWA/browser/project-structure/process tools results in a typed block or launch-plan not-ready state.
- Ensure roles do not receive unsafe mutation tools when their operation contract is read-only.
- Add tests for missing QA browser tools and missing implementation workspace mutation tools.

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
