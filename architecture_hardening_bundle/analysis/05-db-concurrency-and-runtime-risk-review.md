# DB, concurrency, and runtime risk review

## Save flow risk

Observed in `ProcessesService.Persistence.cs`:
- validation occurs before save, but the pipeline still relies on destructive graph rewrite,
- intermediate `SaveChangesAsync` calls exist inside child persistence,
- the full flow is not wrapped in one explicit transaction,
- slug selection is race-prone,
- next version selection uses `Max + 1`.

### Risk
A partial write or concurrent conflicting write can produce surprising outcomes or late DB exceptions without a clean domain-level conflict response.

## Publish flow risk

Observed in `ProcessesService.Publication.cs`:
- publish changes state and then clones a new draft,
- version-selection logic is vulnerable to conflict,
- clone logic still carries compatibility-era dependency behavior.

### Risk
Concurrent publish or draft generation can create version races or unexpected uniqueness failures.

## Runtime transition risk

Observed in `ProcessesService.Runtime.cs`:
- the step transition method owns multiple state transitions and side effects,
- there is no visible optimistic concurrency token on `ProcessRun` or `ProcessStepRun`,
- conflicting transitions are likely to rely on last-write-wins or provider behavior.

### Risk
Concurrent operators or automated callers can lose updates or create inconsistent transition intent.

## Infrastructure note

`AppDbContext` contains SQLite write coordination behavior. That is useful and should stay respected, but it is not enough for aggregate correctness. The module still needs domain-visible optimistic concurrency and conflict translation.

## Target DB/concurrency stance

- application-managed concurrency tokens on aggregate roots,
- explicit transactions around critical mutation flows,
- deterministic translation of conflict failures into the module’s result/error pattern,
- additive migrations for both SQLite and PostgreSQL,
- tests that intentionally create conflicts with two contexts.
