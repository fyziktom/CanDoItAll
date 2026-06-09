# Assumptions, Risks, and Reopen Triggers

## Assumptions

- Branch `maf-processes-refactor` is the active implementation branch.
- PostgreSQL/test database setup may need either existing dev configuration or test-specific fixture setup.
- Some process scenarios may require fake/test providers instead of real paid LLM calls; fake providers are acceptable for structural E2E proof when the goal is runtime plumbing.
- Browser validation should use a large desktop viewport only.

## Critical Path Risks

- Review the enumerated critical path risks before each critical gate and reopen affected subbundles when any risk materializes.

1. Removing bundle-path tests can accidentally weaken architecture coverage.
2. UI process launch may reveal broken dependency injection, missing seed data, or broken route/component wiring.
3. Process template UI may work while dispatch/finalizer/artifact paths are broken.
4. Driver read-only adapters could leak into runtime mutation paths if integration is not tightly allow-listed.
5. Software-development scenarios can hide domain leakage inside generic Process Core if tests only use `.NET`.
6. Business-analysis scenarios can expose missing genericity in template/process run models.
7. Codex may again close a bundle by proving reports rather than real source/UI/runtime behavior.

## Validation Risks

- Build/unit tests are insufficient.
- API tests are insufficient.
- Driver package tests are insufficient.
- Need at least one application startup proof and one large-screen UI process-launch proof.
- Need runtime proof that a process run advances through persisted state, not only in-memory DTO tests.

## Reopen Triggers

- Any test still reads `codex/bundles/<specific-bundle-name>` after SB006.
- The app cannot start.
- The UI cannot display process templates or start a process.
- A `.NET app` scenario or business-analysis scenario cannot create a process run.
- Dispatch creates a run but never advances state.
- Process Core references driver abstractions, modules, infrastructure, UI, workspace, storage, EF, or AgentFramework.
- Runtime host, registry, selector, DI hook, manager command, scheduler/workflow hook, shell/Graph/file/storage/process mutation appears without explicit approved scope.

