# Phase14 hidden-gap summary

## Main finding

The current repo passes the previously defined gates, but those gates stop at the presence of runtime primitives. They do not yet prove that the new runtime primitives have the correct semantics under:
- restart,
- concurrent callers,
- repeated explicit operator actions.

## Required recovery direction

1. **Retire once-like triggers after first fire**
   - Either auto-disable them after the fire is persisted, or model an explicit consumed/retired state that the Quartz projection skips.
   - Add a restart-boundary proof test.

2. **Return the reloaded canonical trigger snapshot from SaveAsync**
   - Save + synchronize + re-read by id.
   - Do not return the pre-projection tracked entity.

3. **Normalize and atomically upsert ingress cursors**
   - Trim source identifiers before lookup.
   - Recover uniqueness races instead of surfacing raw first-write conflicts.

4. **Add a single-executor claim boundary for ingress materialization**
   - Persist a claim before invoking plugin code.
   - Concurrent calls must converge on one materialization result.

5. **Make direct connector processing lease-bound**
   - The public/manual process API must claim the same durable lease used by the worker path.
   - There must not be two execution semantics for the same command queue.
