
# Validation strategy

## Validation principles

1. Validate invariants before validating convenience UI flows.
2. Prefer negative tests for integrity gaps.
3. Treat projection equivalence as a first-class architectural test.
4. Re-run the skill lenses after code changes; do not trust green tests alone.
5. Keep runtime evidence (screenshots / logs / traces) for QA review.

## Required validation sets

### Canonical invariant set

Must cover:

- containment / reparent rules
- illegal cycles
- illegal node transitions
- illegal role/node-kind combinations
- node-scoped assignment scope integrity
- actor-assignment ownership rules

### Projection equivalence set

Must cover:

- assembled graph → structure
- assembled graph → calendar
- assembled graph → Gantt
- assembled graph with actor overlays
- cache rebuild equivalence if a cache remains

### Lifecycle history set

Must cover:

- note → task
- note → decision
- task ↔ decision only if allowed
- preservation of XY / markers / links / assignments per transition policy

### Cross-module actor set

Must cover:

- project-level assignments
- node-level assignments
- meeting participants
- participant node identity links
- work-item assignee
- module-native responsibility read paths (resource / validation / test plan)

## Blockers in this environment

- `dotnet` missing, so none of the runtime validations were executed here
- all commands above must be run by Codex in a proper .NET environment before claiming the findings are closed
