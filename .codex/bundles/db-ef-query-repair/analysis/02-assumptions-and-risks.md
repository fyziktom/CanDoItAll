# Assumptions And Risks

## Working Assumptions

- Existing service contracts and model shapes are correct; only query shape should change.
- SQLite and PostgreSQL compatibility matters for every EF query change.
- A scoped repair pass is more valuable than broad persistence redesign for this request.

## Critical Path Risks

- Moving grouping/order logic into SQL can expose provider translation differences.
- Adding `AsNoTracking()` is unsafe in methods that later mutate the same entity instance; those paths must stay tracked.
- Changing ordering before materialization can alter tie-breaking if the old in-memory order depended on provider row order.

## Validation Risks

- Passing unit tests alone is not enough because several repaired paths are integration-service paths.
- Full solution tests may be long-running; targeted integration and component coverage plus build proof is acceptable for this bundle.
- SQL text inspection is not required unless a patched query fails or provider translation is uncertain.

## Reopen Triggers

- Any patched query fails under SQLite.
- Any patched query fails under PostgreSQL translation during build/test execution.
- A test reveals behavior changed from newest-first, recent-page, or active-lease selection semantics.
- A later audit finds a real EF N+1 loop in a hot path that should be patched in the same bundle.

