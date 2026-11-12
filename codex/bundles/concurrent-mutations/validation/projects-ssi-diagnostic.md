# Projects SSI diagnostic

Status: diagnostic executed on the isolated PostgreSQL 18 fixture. The parent accepted
the narrowly scoped definition protocol recorded in the owner design before source edits.

Owned temporary resource before creation: database
`candoitall_concurrent_ssi_probe_20261016` on the bundle-owned PostgreSQL 18 server
recorded in `../resources.md`. Creation must fail if that name already exists.
The probe uses the existing ignored fixture credentials without printing them.
It drops only this database after its last use and verifies absence, including failure.
No new container, volume, network or application process is required.

The bounded SQL reproduction mirrors the current ordinary definition save's Serializable
transaction, project advisory key, root/phase/option predicates and disjoint updates,
using the same table/index shapes and 36-project / 1265-child-per-kind dataset. Eight
connections finish reads before any writes. Inspect `pg_locks` at that barrier and
EXPLAIN the read shapes. Compare default planning with transaction-local
`enable_seqscan=off` only to diagnose scan/predicate-lock effects; this is not proposed
production configuration. Root-only runs distinguish root-index/page effects from
collection predicates. No source authority behavior is simulated or claimed here.

Script and sanitized output are retained under the already owned
`.artifacts/concurrent-mutations/validation` directory. Production source and standard
build outputs remained unchanged during the probe.

## Result

All 12 samples accepted **1 of 8** disjoint writers; the other seven failed with SQLSTATE
40001 during root UPDATE or COMMIT. This held for full root/child writes and root-only
writes, with default planning and with sequential scans disabled. EXPLAIN already chose
index scans for the root PK and both child ProjectId indexes. Root-only held locks showed
eight root tuple predicates and eight primary-index page predicates. Collection reads
also held child-index and heap-page predicates. Thus the observed false coupling does
not require an unindexed scan, collection replacement, shared project identity or a
local semaphore collision. The synchronized probe deliberately increases overlap; its
1/8 acceptance is diagnostic evidence, not the service benchmark's throughput.

Evidence: `.artifacts/concurrent-mutations/validation/projects-ssi-diagnostic.json`
and neighboring `projects_ssi_probe.py`. The script completed successfully and verified
the exact temporary database absent after `DROP DATABASE ... WITH (FORCE)`. No fixture
container or volume was added. PostgreSQL's documented Read Committed conditional-update
recheck and Serializable predicate-lock behavior support the interpretation; the measured
query plans/locks, rather than the documentation alone, identify this workload's issue.
[PostgreSQL 18 transaction isolation](https://www.postgresql.org/docs/18/transaction-iso.html).

Decision: a dedicated source-null definition scope may use Read Committed under complete
owner advisory keys and original root CAS. Source-authorized operations retain their
existing Serializable checks; generic mutation, deletion and hierarchy protocols remain
unchanged. Performance and cross-connection correctness must both pass after implementation.
