# Bundle intent and target direction

## Bundle intent

This bundle exists to give Codex a **precise, dependency-aware, execution-grade plan** for repairing the `Processes` module safely.

The plan is intentionally designed to:
- stabilize correctness before broad cleanup,
- keep one source of truth per concept,
- avoid replacing one monolith with another,
- preserve current callers behind a thin façade while internals are decomposed,
- force architecture reviews and stop conditions at predictable checkpoints.

## Target direction

### Canonical model direction

- Dependencies must have **one canonical persisted representation**.
- Legacy dependency fields must become compatibility-only, be isolated behind one adapter, or be removed if data migration makes that safe.
- Validation must become **pure** and must not mutate editor or aggregate state.

### Persistence direction

- Save flows must be **transactional** and **conflict-aware**.
- Graph persistence must become **differential**, not delete-and-recreate.
- Aggregate mutation must preserve stable child identities whenever the logical entity survives the edit.

### Runtime direction

- The public service façade may remain for compatibility, but runtime orchestration must move into smaller, testable policy and planner services.
- Transition rules, dependent activation, skip semantics, status recompute, and journaling must be independently testable.

### Query direction

- Common read surfaces must stop assuming “small enough to load and aggregate in memory”.
- Query-specific projections and services should shape data close to the database.

### Shared-infrastructure direction

- Generic duplication should be extracted once.
- Domain-specific duplication should only be extracted to a shared location if the semantics are genuinely the same.
- Avoid creating a generic `Utils` dumping ground.

### UI direction

- `ProcessWorkspace` must be decomposed into smaller components and a clearer state layer.
- Domain logic must not drift into Razor markup or UI event handlers.

## Explicit non-goals

- Do not redesign the product scope.
- Do not add unrelated new process features.
- Do not introduce a second canonical store for process state.
- Do not centralize every helper in `SharedKernel` just because it appears twice.
- Do not claim completion without real proof.

## Compatibility rule

Where caller compatibility matters, prefer this pattern:

1. keep `ProcessesService` as the public façade,
2. extract internal services with explicit responsibilities,
3. delegate from the façade,
4. migrate call sites only where that improves clarity without broad churn.

That pattern keeps the refactor bounded while still improving architecture.
