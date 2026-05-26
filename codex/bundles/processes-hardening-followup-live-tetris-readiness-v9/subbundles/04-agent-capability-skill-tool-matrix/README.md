# SB04: 04-agent-capability-skill-tool-matrix

## Goal

Define exactly which skills and tools each process role needs for the live test.

## Work items

- Create a matrix for delivery manager, architect, implementation engineer, QA/browser proof lead, repair engineer, and writeback manager.
- For each role list required skills, required tools, forbidden tools, operation contract, target scope, and expected artifacts.
- Ensure the matrix is generic enough to reuse for other software templates.
- Add runtime/tool-profile assertions that assigned agents have the needed tool capabilities before dispatch starts.
- Block with typed cause when a required tool or skill is missing instead of letting agents improvise.

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
