# Structured Input

## Core Objective

- Execute the Cognitive Memory P0 roadmap phase and update docs to reflect the resulting implementation state.

## Success Criteria

- Oversized Cognitive Memory surfaces are split into more focused files or components without changing behavior.
- Projection rebuild is exposed as an explicit product path that consumes stale projection records and records outcomes.
- Scheduled automation settings drive observable ingestion/consolidation work through an explicit service.
- MAF context contribution has a clear process-critical fail/skip policy and agent-facing DTOs are separated from diagnostic recall payloads.
- Targeted tests/build pass and docs/roadmap are updated to the actual post-P0 state.

## Hard Constraints

- Use the bundle workflow and keep raw-note closure evidence.
- Keep abstractions minimal and aligned with existing project patterns.
- Do not silently hide errors; process-critical paths must fail predictably.
- Do not make Qdrant/RAG canonical memory.
- Do not destabilize the Blazor operator route while refactoring.

## Allowed Side Effects

- Cognitive Memory module code, Cognitive Memory API code, focused tests, docs, and this bundle.

## Source Artifacts

- `docs/cognitive-memory/roadmap/roadmap.md`
- Cognitive Memory module, API, page, MAF integration, and tests.

## Input Coverage Signals

- Execute P0, not just document it.
- Validate/test the implementation.
- Update docs and roadmap after implementation.

## Dependency And Sequencing Signals

- Structural splits should happen before behavioral additions so new services land in the right files.
- Projection rebuild and scheduled automation need tests before docs claim they are implemented.
- Agent context policy should be validated before final docs move it out of P0.

## Validation Expectations

- Bundle validator prepared/completed passes.
- Targeted unit/integration/component tests for changed surfaces pass.
- Build or targeted project compile passes.
- Browser validation is required only if rendered Blazor markup behavior changes.

## Evidence Contract

- `dotnet test` targeted commands for Cognitive Memory unit/integration/component tests.
- `dotnet build` or targeted compile command.
- `git diff --check`.
- Bundle validator results.
- Browser proof if UI markup changes.

## UI Validation Strategy

- If Blazor markup changes beyond mechanical component extraction, run the Cognitive Memory route in browser at a large viewport and one narrower viewport. If no rendered UI behavior changes, record N/A with rationale.

## Browser Validation Analytics

- Subbundle 01 records browser proof only if `CognitiveMemoryPage.razor` markup behavior changes.
- Other subbundles are service/API/docs work and should record N/A unless host-visible behavior changes.

## Working Assumptions

- P0 can be completed with focused service/API splits and operational services without changing database schema unless source inspection proves new durable run records are required.
- Existing test patterns are the preferred validation path.
- Docs should be updated after tests pass, not before.

## Primary Risks

- Moving large code sections can introduce namespace/using or partial-class compile errors.
- Adding scheduled execution may accidentally create hidden background writes; keep it explicit and logged.
- API endpoint refactoring can break route mapping if not covered by tests/build.
