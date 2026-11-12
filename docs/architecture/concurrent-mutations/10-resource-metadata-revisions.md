# Resource metadata revision migration

The original-state Resource owner requires an explicit nonempty revision on every row.
It compares the caller's captured revision and dataset/project binding after acquiring
the owner exclusions. Accepted Save and promotion outcomes carry the exact committed
snapshot; Delete carries the exact deleted expectation. Current reads or absence do not
resolve an earlier unconfirmed write. This revision is not a durable operation receipt.

The Resource schema change is separate from C3's Workbench receipt installation. It adds
one uuid column, a nonempty check and a nonunique project/lifetime/connector candidate
index to the existing table. It adds no entity, receipt family or finite-operation pool.
The canonical model remains at 169 entities and the Resource owner at one.

Run the migration only with all application writers stopped and after the complete
reviewed owner/contract/caller cutover. A database constraint cannot make an old binary
rotate Revision on an update. Running mixed old and new writers would defeat the
original-state comparison even when every row has a nonempty token.

Backfill each retained row once without changing any previous business column, timestamp,
raw configuration, secret-reference JSON or Search projection. Unknown connectors,
oversized or malformed retained configuration and unbound or missing-project references
must remain intact. The editor's new bounds do not authorize migration-time cleanup.
Application writes generate subsequent revisions; there is no persistent database
default. Repeated migration/readiness and restarts must not refresh existing revisions.

Unexpected predecessor columns, defaults, keys, indexes, row security, rules or user
triggers are explicit refusal conditions. A partial Revision installation is not adopted.
Resolve schema drift through a separately reviewed maintenance decision, not by deleting
history entries, forcing a default or bypassing the guard. A lock-wait refusal means the
stopped-host condition was not established. The failed transactional attempt must leave
schema, migration history and retained data unchanged before a separate admission.

Downgrade is permanently refused with PostgreSQL `P0001`: removing Revision would discard
concurrency history. Recovery requires an explicitly coordinated stopped-host restore of
matching database, activation state and binaries. Do not describe a failed acknowledgement
as rollback; inspect the retained migration history using the existing operator procedure
before deciding the next maintenance action.

The index narrows promotion candidates; it does not prove bounded comparison cost or
maximum concurrency. The baseline Resource owner still uses exclusive project gates.
Import lifecycle NOWAIT and the fresh deletion handoff must pass their actual prerequisite
tests before changing those gates. Shared-project admission, promotion G/R coordination,
capacity and production activation remain separate gates.

The source-only migration proof is `ResourceMetadataMigrationTests`: three Facts for
retained upgrade/restart, live schema/model constraints and drift/lock/Down rollback.
These do not replace the 18 owner Facts, 343 deduplicated ordinary cases or four
separate browser cases. At authoring, canonical migration generation and all three new
Facts remain unexecuted; their source is not a migration success claim.
