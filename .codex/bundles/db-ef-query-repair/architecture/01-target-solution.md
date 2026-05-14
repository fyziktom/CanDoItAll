# Target Solution

## Target Shape

- Keep `AppDbContext` and `IDbContextFactory<AppDbContext>` usage unchanged.
- Push ordering, paging, and date filtering into EF query expressions before materialization.
- Use `AsNoTracking()` only for read-only paths that do not later mutate the returned entity.
- Keep provider-specific `DateTimeOffset` handling explicit: SQLite uses client-side ordering after safe filtering because EF Core cannot translate `DateTimeOffset` `ORDER BY`; non-SQLite providers keep server-side ordering.
- Avoid new repositories, caches, or database abstraction layers.

## Boundaries

- Infrastructure services remain in `CanDoItAll.Infrastructure`.
- Module services remain in their existing module projects.
- No migrations or schema changes are planned.
- No browser-visible UI changes are planned.

## Non-Goals

- No database index redesign.
- No provider-specific SQL.
- No global `QueryTrackingBehavior.NoTracking`, because many services still rely on tracked writes.
