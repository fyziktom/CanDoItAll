# Codex review checklist

## Canonicality
- [ ] Dependencies now have one canonical representation.
- [ ] Legacy dependency fields are isolated behind a compatibility boundary or removed.
- [ ] Validation is pure and normalization is explicit.

## Persistence and conflict handling
- [ ] Save, publish, and critical transition flows are wrapped in explicit transactions.
- [ ] Application-managed optimistic concurrency is present on the required aggregates.
- [ ] Concurrency and uniqueness conflicts are translated into the module’s result/error contract.
- [ ] Differential persistence preserves stable child identities.

## Publication and runtime
- [ ] Publish lifecycle and clone logic are separated.
- [ ] Version/slug allocation is race-aware.
- [ ] Runtime transition logic is decomposed into smaller policy/planner services.
- [ ] No new runtime god service was introduced.

## Query and performance
- [ ] Common query surfaces use slimmer projections.
- [ ] Read-side work did not create a second canonical store.
- [ ] Analytics and list queries are not doing avoidable broad graph loads.

## Consolidation and UI
- [ ] Shared helper extraction respects ownership.
- [ ] No generic dumping-ground helper was introduced.
- [ ] `ProcessWorkspace` is materially smaller and easier to reason about.
- [ ] Domain rules did not drift into Razor markup.

## Schema and migrations
- [ ] Entity/configuration files are easier to audit.
- [ ] Relationship/delete behavior is explicit where needed.
- [ ] SQLite and PostgreSQL migrations/snapshots are coherent.

## Testing and proof
- [ ] Existing regression tests were preserved or strengthened.
- [ ] New tests cover concurrency, stable IDs, and canonicality.
- [ ] UI-changing phases have real browser proof.
- [ ] Review gate memos exist and any corrective subbundles were completed before downstream work continued.
