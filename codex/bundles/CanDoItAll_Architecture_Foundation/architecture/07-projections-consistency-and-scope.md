# 7. Projections, consistency, and scope

## Choose the smallest suitable read mode

Owner query, live batch composition, materialized projection, and historical snapshot have different freshness/lifetime guarantees. Do not turn every lookup into an event-driven cache. Existing CRM documentation composes technical AgentReference data with local binding/governance [SRC-010]; improve that owner boundary rather than create another catalog.

Structure normally reads technical fields from Agents and CRM fields from CRM. An accidental Agents -> CRM cache -> Structure cache chain should not become the default. An intentionally published composite has documented provenance and coverage.

## Projection envelope

A persisted read model records source reference(s), source revision/sequence, data incarnation, project/domain scope, necessary access partition/policy version, observed/produced times, freshness, and coverage. A UI may hide fields but the server must enforce their meaning.

Composite reads have a source-version vector, not a fictitious global revision. Independently observed CRM roles and provider prices can serve a display with disclosed coverage; assignment/budget commits revalidate relevant owner versions. Card LastUpdated is not a transactional snapshot of all domains.

Distinguish current, stale, partial, unavailable, tombstoned, unsupported, and denied. An outage is not “no agents.” Security policy may mask Denied as NotFound without implying a globally complete catalog.

## Duplicates, ordering, and gaps

Deduplicate by source event identity/stream, not title/time. A newer full snapshot can replace an older one only under the owner's ordering contract. Deltas cannot skip a missing earlier update merely because a later one arrived first. Restore order or resnapshot. Unknown schema semantics cannot be silently ignored.

Tombstones prevent older events from resurrecting source data. Their retention and receipt retention must cover supported retry/replay/backup windows. Shortening that retention is an architectural change, not arbitrary cleanup.

## Rebuild with concurrent changes

Owner supplies a consistent snapshot and cursor/barrier, or another proven mechanism. Build a new read generation, catch up after the barrier, validate coverage/scope/count, and switch safely. Without such support, use a maintenance fence/resnapshot or simpler live queries; do not claim gap-free rebuild.

Rebuild never deletes native notes/tasks/bindings/layouts. Native and projection namespaces remain distinct. Reading a projection does not invoke contribution commands or repeat actual work.

## Read your writes

After confirmed commit, accept the owner's identity/revision/result. A delayed older projection must not overwrite it. Display saved/pending synchronization and perform a bounded authorized owner read where needed, not a second editable store.

Failed invalidation or CRM synchronization does not undo an Agents save. Removing workflow GetStatus side effects must include an explicit update path so closing the canvas does not stop writeback [SRC-026].

## Profile, restore, access

Database profile selection is a runtime scope, not automatically tenancy. Pin operation scope at admission; partition caches by relevant profile/generation/source/access. Scope switches detach old preparations/subscriptions. Late results cannot appear or write in the new profile. A durable run continues in its original available scope or remains blocked; never rebind it to current.

Clone/restore requires a data-incarnation/history fence. Identical IDs or sequences need not represent identical histories. No specific new table is mandated; the mechanism must be proven. Changing incarnation invalidates caches but must not mint new remote keys to repeat old external work.

Recheck access at sensitive reads, content streaming, commands, and continuation. Cache invalidation is not authorization. Cache keys need user/purpose/access partition where results differ. Already disclosed data cannot be recalled; subsequent reads/disclosure must still be prevented.

## Operational visibility

Observe lag, oldest pending work, retries/failures/dead letters, reconciliation, coverage, and tombstone conflicts. Operators need traceable rebuild/repair. Set actual thresholds from measured product requirements; this foundation invents no zero-latency or performance guarantee.
