# SB05: Manager/operator runtime-host readback for real runs

## Status
Prepared.

## Objective
Manager/operator runtime-host readback for real runs.

## Covered inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Exact source references
See body below. Add exact file paths during implementation if the inventory discovers renamed or moved sources.

## Scope

Connect runtime-host verification/dry-run readback to actual process run/step contexts.

Deliverables:
- manager/API readback DTO includes verification host status, dry-run plan readback, audit id/hash, denials, evidence counts, no-mutation flags;
- add large-screen UI proof if route/component changes are made;
- otherwise add API/readback proof against real run ids from SB03/SB04.


## Dependency impact
This subbundle gates the next subbundle. If validation fails, downstream work is not trustworthy.

## Validation depth
Critical. Requires focused tests and source assertions. Browser proof is required only for UI-visible changes or route proof.

## Do Not Do
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, driver self-registration, or generic effectful runtime host.
- Do not mutate process state through drivers.
- Do not add domain-specific concepts into Process Core.
- Do not create large proof scaffolding or repeated boilerplate during execution.

## Acceptance checklist
- Real source/test code changed unless this is an explicit inventory blocker.
- No effectful driver execution added.
- Process Core remains generic.
- Focused tests prove behavior.
- Source scans pass.
- Code-first ratio is not weakened.

## Proof required
- Focused test transcript.
- Source scan transcript.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser validation logging
N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression gate
Proceed only after acceptance checklist passes. Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested agent prompt
Implement SB05 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
