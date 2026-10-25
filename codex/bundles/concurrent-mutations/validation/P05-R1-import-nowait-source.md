# R1 stage 1: exclusive-import lifecycle refusal

Authored and independently source-reviewed, 2026-10-17. No build, discovery, test,
database action, static scan or runtime result is claimed. Parent selected design
`8A30FA595CB42DDCBD214574CA2B58EAF252521CBB14F7C5BC5D3AA7EDCC11AC` and authorized only
this first correctness stage. Resource locks, Project deletion, models and migrations
are unchanged. Main was not edited.

`DatabaseTransferLifecycleGuard.AcquireImportExclusionBeforeSnapshotAsync` uses fixed
`LOCK TABLE ... IN EXCLUSIVE MODE NOWAIT` after the existing PostgreSQL/transaction
validation and utility lock-timeout setup. Only `DatabaseTransferImportScope` calls it.
The generic transfer caller and its waiting lifecycle SQL remain unchanged. There is no
runtime strategy flag. The existing session-key handoff, table exclusion, lifecycle
binding check and cleanup remain intact.

The original three source images and both managed/main predecessor hashes were recorded
before edits in [the predecessor record](../reviews/P05-R1-import-nowait-predecessors.json).
All three original main and managed hashes matched. That retained record and its copied
images are immutable; the source amendment inventory records successor hashes separately.

| Current source | SHA-256 |
| --- | --- |
| `DatabaseTransferLifecycleGuard.cs` | `25523BDAB66CD23C2A9AAC40848D07425AFAEBFD6BEE4F244C7C1E39DF5BAA8F` |
| `DatabaseTransferImportScope.cs` | `F585D751A66145A8A292AAFD9C202E379818B474CC576A2593004E5FD0F1F4CF` |
| `DatabaseTransferLifecycleTests.cs` | `B81082E0AF95E7FA4290E8FAE9E1470DFFEDB706D6C7C9CA01261A020EF69CCE` |

The test class has **8 source cases**: 2 Facts and 6 InlineData rows across 3 theories,
with no dynamic data, skips or category exclusions. This adds one Fact to the original
7 cases. The generic transfer row still positively observes lifecycle waiting and its
fresh snapshot. The import row now requires direct SQLSTATE 55P03 before callback entry,
independently acquires the released session key while the lifecycle holder remains,
then explicitly invokes a separate admission after the writer commits. It verifies the
retained receipt's exact payload, revision and lifetime. That is deliberate test admission,
not product retry.

The new Fact pauses the actual production import immediately before its lifecycle lock,
after session B is held. A second real owner enters the actual Serializable scope with
P then B. A third real RC transaction holds lifecycle SHARE then waits P. Live
`pg_blocking_pids` observes both exact PID edges before releasing the import command.
The expected 55P03 releases B; both other owners must finish, independent P/B acquisition
must succeed, lifecycle identity remains exact, and no Project/receipt effect exists.
Barriers release, cancellation fires and tasks drain before contexts are disposed.
The third owner models the proposed deletion prelude's conflict graph; it is not proof
of the still-unauthored deletion handoff implementation.

The runner's import commit knowledge changes only inside its actual commit delegate.
Lifecycle refusal occurs before that callback, so it remains a definite pre-effect
exception; no Acknowledged or Unconfirmed import wrapper is fabricated. Existing cleanup
may aggregate its own failures but must retain the original pre-effect cause. The
unchanged retained-file import cases remain a mandatory regression selection:

* `FullyQualifiedName~CanDoItAll.Tests.Integration.DatabaseTransferLifecycleTests.` — 8.
* `FullyQualifiedName=CanDoItAll.Tests.Integration.ProjectTransferHistoryPersistenceTests.Package_import_compensates_only_before_commit_and_retains_bound_files_after_commit_uncertainty` — 4.
* `FullyQualifiedName=CanDoItAll.Tests.Integration.ProjectTransferHistoryPersistenceTests.Row_import_preserves_acknowledged_counts_and_never_claims_an_unconfirmed_commit` — 2.

These are **14 disjoint expected source cases**, not a runtime result. Parent must build
the affected Infrastructure project and standard Release Integration output, discover
the exact selection, run against the existing isolated PostgreSQL18 lease fixture, and
complete full portability-static review/enforcement after integration. Preserve failure
evidence and fixture cleanup. The unchanged generic transfer path must not silently
gain Project/B session keys later without a new wait-graph audit.

Independent runtime peer verified all three hashes and found no source blocker in the
fixed import entry, causal barriers, failure knowledge or cleanup. This is source-only
approval. Stage 2 and shared Resource P remain separately gated by the
[implementation work card](../plan/P05-R1-lock-freshness-work-card.md).
