# Structured Input

## Objectives

- Continue progressive process isolation on branch `maf-processes-refactor`.
- Remove ambiguous route source-payload, finalizer intent, hydration, subprocess, projection, artifact, wrapper, and driver-readiness boundaries.
- Preserve current runtime behavior while making a later narrow Process Core proposal easier to justify or reject.

## Hard Constraints

- Do not create `CanDoItAll.Processes.Core` or `CanDoItAll.Modules.Processes.Core`.
- Do not introduce production driver APIs, registries, DI hooks, manager tools, or helper-driver runtime services.
- Do not touch UI, Razor, CSS, JavaScript, TypeScript, media, screenshots, or mobile/browser proof surfaces.
- Do not simplify or remove route, finalizer, retry, recovery, provider, subprocess, projection, artifact, or claim behavior.

## Validation Expectations

- Keep 36 separate subbundle rows in `reviews/01-execution-report.md`.
- Run source scans for no Core, no driver API, no UI/media drift, no stubs, and no collapsed proof rows.
- Run build, full unit tests, and focused integration tests for dispatch, route, subprocess, projection, finalizer, and execution boundaries.
- Record artifact-backed proof for critical phase gates under `proof/SBxxx/`.
