# 06 · Write protocol, transaction modes and lock ordering

## Short owner transaction; long-lived intention outside it

The user or agent may prepare a draft for minutes. No database transaction or business gate survives that preparation. At dispatch the adapter freezes the submission, operation identity, original stamp, source/basis and trusted actor context. The authoritative owner performs the short publication attempt.

### Recommended logical sequence

1. Validate bounded input shape and resolve trusted request/actor context. Authenticate before disclosing target or receipt details.
2. Resolve the existing operation when the command contract supports retries. A matching committed receipt returns its original accepted result subject to current disclosure authorization; a mismatched payload is rejected. A missing row is not by itself proof that no concurrent attempt is running.
3. Acquire the required **source/claim authority lease before the owner transaction**, preserving current owner-specific order. Do not invent an additional global gate.
4. Begin the selected transaction mode. Acquire the complete declared guard set according to the lock-class protocol, and validate the required admissions/basis under that protection.
5. Load only the required current rows. Check the original expected state, lifetime/epoch, structural conditions and proposal validity. Normalize and validate the intended result.
6. Apply the effect with an atomic target predicate or an equivalently proven owner-guarded protocol. Advance relevant content/scope generations and write any required receipt/projection intention on that same transaction.
7. Recheck current source/claim authority where the existing protocol requires it, then commit. The owner records its commit knowledge immediately, before observer callbacks or cleanup can throw.
8. Release owned gates and participation. Publish optional notifications/read-back separately. A failed observer or disposed page does not uncommit the operation.

This is a semantic sequence, not a mandatory generic pipeline class. The existing Projects source path has explicit rules and may combine steps. Preserve those rules while adding content state (SRC-06).

## Mode matrix

| Mode | Suitable operation | Required proof | Not sufficient for |
|---|---|---|---|
| Atomic conditional row/aggregate update | Simple protected editor save or narrow field intent | Original token in write predicate; children + root + receipt commit atomically | Arbitrary relational invariants or external freshness |
| Existing guarded serializable owner transaction | Hierarchy/task/source-authorized writes already using it | All relevant writers, guard ordering, bounded retries, current source authority | Minutes-old editor intent without original expected state |
| Short coherent edit read | Project scalar + phase + option snapshot | Single coherent query or short appropriate transaction snapshot | Holding a user lock or authorizing a future effect |
| Protected multi-owner publication | Agent decision based on current source facts in same DB | Explicit enlisted owner ports; complete conflicting-writer exclusion or stated serialization semantics | Cross-database or remote atomicity |
| Immutable version publication | Deliberate pinned input/definition | Exact immutable identity plus target publication condition | Claiming current live-input freshness |
| Durable external intention | Nontransactional bytes/API effect | Provider-specific conditional/idempotent delivery and outcome reconciliation | Universal exactly once |

The initial pilot can retain the existing protected transaction for compatibility; do not reduce isolation while adding concurrency. A cheaper mode is a later measured optimization with separate proof. Equally, do not add serializable transactions and process stripes to every mutation merely for uniformity.

## Captured-token implementation requirement

The server must not read today's token, apply an old body's values and let EF compare that newly loaded token. The conditional write must represent the token obtained with the original edit read. EF Core supports original-value concurrency predicates (EXT-01). The owner still validates admission, actor policy and domain rules; the content predicate does not authorize the write.

A conceptual SQL shape is:

```sql
-- Conceptual protocol only; use actual mapped columns and the owner transaction.
UPDATE "Projects_Projects"
SET "Name" = @accepted_name,
    "ConcurrencyToken" = @next_stamp
WHERE "Id" = @project_id
  AND "LifetimeId" = @expected_lifetime
  AND "ConcurrencyToken" = @original_edit_stamp;
```

Zero affected rows is a known non-application of that conditional statement. How the response distinguishes missing, retired and stale must follow authorization and avoid a race-prone information leak. Child changes and the operation receipt must roll back if the root condition fails. A successful row update is not yet proof that the transaction commit succeeded.

## Source-basis validation: avoid a false guarantee

Reading source tokens and writing the target in a Repeatable Read transaction gives a coherent historical view, not an automatic promise that no source changed before publication. Serializable execution defines a valid serial ordering, and overlapping transactions can legitimately serialize before one another (EXT-04). Decide what the product needs.

For **current-at-publication**, establish a source-owner guard that every conflicting source writer participates in, keep it through target commit, and validate the source after acquisition under the correct snapshot rules. Existing project gates can protect a small first pilot. A future shared/exclusive source guard can allow multiple readers, but only after verifying compatibility with existing exclusive writers and acquiring/upgrading order safely. With row locking, do not confuse a key-preservation lock with protection from ordinary field updates. Use the actual lock mode appropriate to the source's mutation pattern (EXT-03).

A specific proof obligation is **snapshot timing while waiting for a gate**. In a transaction-snapshot isolation mode, a statement issued before an advisory lock is granted may already establish the snapshot. Merely acquiring that lock later and then reading is not proof that the returned source revision includes the writer that released it. Test this exact wait sequence. The source-owner protocol must force an appropriate current/locking read or serialization failure/retry, or choose another explicitly proven acquisition/snapshot sequence. Do not work around it by silently lowering isolation across all existing source-authorized writes. This is a derived PostgreSQL protocol risk, not a reproduced defect in the inspected product paths (EXT-03/EXT-04).

For **serially valid planning**, a documented serial ordering may be sufficient even if a simultaneous human edit commits first in wall-clock time. Do not label that stricter freshness. The decision register recommends strict publication protection for material objective/mandate changes in the initial agent pilot.

## Predicate and membership dependencies

"All unfinished tasks" depends on the absence of additional matching tasks as well as versions of returned tasks. Row-token lists alone miss insertions, deletions, reassignment into the set and empty-result changes. Use an owner-maintained collection/scope generation or an explicitly proven predicate serialization protocol. Every relevant writer must update the generation in the same transaction. An oversized basis is rejected or decomposed into a deliberately weaker staged workflow; it is never silently truncated.

## Current lock mechanisms and risks

SerializableMutationScope's relational branch acquires one or more of 64 shared in-process stripes before the database transaction; its PostgreSQL advisory locks are then acquired per sorted key. The authorized Projects source branch instead starts its own transaction after source authority and does not take those same process stripes (SRC-04/SRC-06).

Do not remove local gates by inspection alone. They may serialize test-only state or protect behaviors not apparent from the local method. Conversely, using them in every new validation path can cause unrelated projects to wait. Instrument wait time and hold time separately. A held `await using` scope can outlive `CommitAsync`; Project Save explicitly disposes it before callbacks, while some hierarchy methods need lifetime measurement (SRC-03).

## Ordering protocol to specify in P01

Document lock **classes** first: source/claim authority, existing project/structural mutation gates, subordinate owner row/scope gates, and storage/remote boundaries where applicable. Determine existing class precedence from real callers. Within a class, acquire the complete stable ordered key set. Do not replace source-first ordering with a universal alphabetical string rule across all classes.

No arbitrary new key acquisition after a late data read. If validation discovers additional required keys outside the admitted set, exit/rollback and replan acquisition, or reject the operation as too broad. Nested entry into the same non-reentrant semaphore can self-deadlock; existing enlistment should participate in the owner transaction rather than creating another top-level scope. Shared-to-exclusive upgrades need a deliberate protocol, not an opportunistic upgrade while holding other keys.

## Enlistment and execution strategy

Use existing typed owner mutation/read ports and CoordinatedDatabaseTransaction to share the physical connection and transaction (SRC-05, EXT-02). Do not call ordinary independent reads and label them enlisted. Do not use Task.WhenAll for commands on that shared connection. Await each bounded owner operation before leaving the scope; no deferred callback inherits a valid transaction after retirement.

A retry of serialization/deadlock failure restarts the entire safe transaction with fresh DbContext state while retaining the **original** intention and preconditions. It does not refresh approval or expected content. No external side effect is placed in a retriable SQL transaction. Retry deadlines, backoff and cancellation are bounded; high conflict is not an invitation to spin.

## Cleanup and unknown commit

Fault-inject transaction dispose, gate release, lease finalization and observer exceptions. Ensure release paths run in finally-equivalent ownership cleanup without masking the original cause. SRC-04's disposal structure and SRC-06's postcommit source disposal deserve specific tests, not an assertion of an observed leak. If the database commit is confirmed, subsequent cleanup failure is an operational warning. If connection loss interrupts commit acknowledgement, reconcile by the stable operation identity (EXT-06).
