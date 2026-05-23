# Current-state analysis

## Architectural issue

SQLite was valuable as an early local development database, but the main CanDoItAll runtime has evolved beyond a single-user/simple-local persistence model.

The main application now includes or is expected to include:

- process runtime,
- workflows,
- automation runtime,
- command/outbox boundaries,
- plugin execution,
- agent orchestration,
- runtime database switching,
- profile control plane,
- long-running worker tasks,
- evidence/artifact tracking,
- future cognitive memory and knowledge modules.

In this architecture, SQLite support creates a lowest-common-denominator effect.

## SQLite costs

SQLite currently creates costs in these categories:

1. **Migration cost**
   - Duplicate migration projects.
   - Provider-specific migration differences.
   - Longer build and review cycles.

2. **Runtime complexity**
   - Provider switching branches.
   - SQLite connection normalization.
   - SQLite write coordination/interceptors.
   - Legacy SQLite profile recovery.

3. **UI/control-plane complexity**
   - Multiple profile source kinds.
   - SQLite file pickers and materialized path display.
   - Snapshot profiles that look like normal runtime DB profiles.

4. **Testing complexity**
   - SQLite test matrix.
   - Provider-dependent behavior.
   - Risk of tests passing on SQLite but failing on PostgreSQL.

5. **Architectural limitation**
   - Avoidance of PostgreSQL-specific concurrency and locking capabilities.
   - Artificially serialized workflow/process execution.
   - Extra caution around async worker behavior.

## Target state

PostgreSQL becomes the only persistent runtime provider.

Optional future SQLite use is allowed only outside the main runtime, for example:

- isolated local utility store,
- portable snapshot artifact,
- export/import archive,
- read-only cache.

That future use must not reuse the main `AppDbContext` as a runtime provider.
