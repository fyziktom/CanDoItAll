# Codex Prompt 01A - Development Manager, Watch Loop, Capsules, and Tuning Mode

## Objective
Implement the local development manager that supervises `dotnet watch`, exposes a Codex-facing local API, generates capsule artifacts from source comments, and supports dev-only tuning requests from the running UI.

## Required reading
1. `README.md`
2. `docs/02-technical-requirements.md`
3. `docs/03-ui-architecture-and-ascii-layouts.md`
4. `docs/03a-workbench-tabs-canvas-and-state.md`
5. `docs/03b-development-manager-watch-capsules-and-tuning.md`
6. `docs/04-solution-architecture.md`
7. `docs/07-implementation-plan.md`
8. `docs/08-checklists.md`
9. `docs/09-validation-and-testing-plan.md`
10. `docs/10-executive-qa-review.md`
11. `docs/11-references.md`

## Constraints
- Use .NET 10 and C#.
- Keep the manager local-only and development-scoped.
- Use the official `dotnet watch` command with `--non-interactive`.
- Prefer ASP.NET Core Minimal APIs with `AddOpenApi` and `MapOpenApi`.
- Prefer SSE for streaming manager events.
- Keep code comments in English.
- Do not expose secrets, raw prompt payloads, or arbitrary file contents through the manager.
- Keep generated capsule artifacts outside rebuild loops or explicitly excluded from them.
- Add or update tests for the touched behavior.

## Scope
This prompt covers milestone M0A: the development manager, watch normalization, readiness contract, capsule generation baseline, and dev-only tuning workflow foundation.

## Tasks
1. Create the `CanDoItAll.Manager` tool project and add it to the solution.
2. Implement supervised `dotnet watch` execution for the main web app with stable state normalization.
3. Add a development-only runtime readiness endpoint to the main app so the manager can confirm real readiness instead of trusting console text alone.
4. Expose loopback-only OpenAPI endpoints for watch status, active app URLs, logs, wait-ready semantics, capsule summaries, and tuning-request status.
5. Implement SSE endpoints for watch and tuning events.
6. Define and enforce the baseline capsule format, plus a skip marker for allowed exemptions.
7. Implement source watching and incremental generation of capsule artifacts under a manager-controlled artifacts path.
8. Build the dev-only tuning-mode foundation in the UI, including tunable component boundaries and request packaging.
9. Track tuning requests through correlation ids, watch readiness, capsule drift status, and reviewable completion state.
10. Add unit, integration, and smoke tests for watch parsing, readiness confirmation, capsule generation, and tuning request lifecycle.

## Required deliverables
- `CanDoItAll.Manager` project
- normalized watch-state model
- development-only runtime readiness endpoint in the main app
- local OpenAPI and SSE endpoints
- baseline capsule parser and generated artifacts
- dev-only tuning request foundation
- tests for the manager subsystem
- local run documentation for the manager and watch loop

## Acceptance criteria
- the manager can start and observe `dotnet watch` for the main app
- the manager emits a trustworthy `ready` result only after build and runtime readiness are both confirmed
- recent watch logs and normalized events are queryable
- capsule coverage and drift are measurable through the manager API
- tuning mode is hidden outside development mode
- a tuning request can be created, tracked, and correlated to watch-ready completion
- generated artifacts do not create self-triggering watch loops
- the touched tests pass

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when the development loop is machine-readable, testable, and ready to accelerate the remaining implementation prompts.
