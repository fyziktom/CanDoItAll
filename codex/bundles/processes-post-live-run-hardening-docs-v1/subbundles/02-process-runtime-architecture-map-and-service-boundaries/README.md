# SB02: 02-process-runtime-architecture-map-and-service-boundaries

## Goal

Create a current architecture map for Processes runtime.

## Required work

- Map process definition, template import, run start, dispatch, finalizer, artifacts, read model, recovery, manager chat, project-structure projection, and API surfaces.
- Identify oversized partial classes and responsibilities that should become services.
- Propose service boundaries without breaking generic process behavior.
- Update `src/CanDoItAll.Modules.Processes/README.md` with the architecture map draft.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB02` are updated and the next dependent workstream can rely on it.
