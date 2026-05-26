# SB09: 09-process-step-work-briefs-for-agent-limitations

## Goal

Make each step's limits explicit in the work brief.

## Work items

- Generate or update work brief text so each agent sees only the current step goal, forbidden work, required artifacts, and handoff.
- For the first step explicitly say: do not implement, do not create project files, do not run product mutation tools.
- For QA explicitly say: do not repair code; branch to repair-required and record evidence.
- Add tests/source assertions that generated work briefs include current-step boundaries.

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
