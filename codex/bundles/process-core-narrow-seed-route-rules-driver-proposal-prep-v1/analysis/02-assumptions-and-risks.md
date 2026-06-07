# Assumptions And Risks

## Assumptions

- `CanDoItAll.Processes.Contracts` already owns public process status/kind enums used by pure rules.
- `CanDoItAll.Processes.Core` may reference `CanDoItAll.Processes.Contracts`, but must not reference `CanDoItAll.Modules.Processes`, Infrastructure, AgentFramework, EF, Workspace, Storage, UI, or plugin projects.
- The first Core seed must be small enough that rolling back is trivial.
- Module-local adapters remain the compatibility boundary.

## Critical Path Risks

1. **Over-extraction risk** — moving route handlers or route services into Core would mix orchestration with pure rules.
2. **Dependency leak risk** — a Core project reference to Infrastructure, AgentFramework, Storage, EF, or Modules would invalidate the cutline.
3. **Behavior drift risk** — changing route order or eligibility decisions would alter process execution semantics.
4. **Driver creep risk** — adding helper-driver interfaces during the first Core cutline would combine two architectural changes.
5. **Test illusion risk** — focused tests may pass while a full runtime path breaks; require focused route/dispatch integration plus source scans.

## Validation Risks

- Full integration suite may be slow; focused integration proof is acceptable only if route/dispatch paths are covered.
- Architecture tests must target the active bundle, not stale older bundle fixtures.
- No UI proof should be created for runtime-only changes.

## Reopen Triggers

Reopen earlier subbundles if any of these occur:

- Core references `CanDoItAll.Modules.Processes`.
- Core references EF, Infrastructure, Workspace, Storage, AgentFramework, UI, plugins, or driver namespaces.
- Route stage order differs from the current canonical order.
- A route handler or service moves to Core.
- A process-driver interface/registry/DI/runtime selector appears in production source.
- Any existing route/dispatch integration test fails.
