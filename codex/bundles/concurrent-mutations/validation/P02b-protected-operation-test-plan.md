# P02b protected Projects operation integration source

Status: source prepared on 2026-10-16; **not built or run by this work unit**.
The coordinating owner owns discovery, execution, portability-static review and
final evidence. The counts below are source-derived expectations, not passing
results.

| Source | Expected cases | Boundary |
| --- | ---: | --- |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectDefinitionOperationTests.cs` | 13 | Normal duplicate create/replace, historical receipt, changed digest, full/scalar no-op revision, required-search rollback, current read denial, invalid context |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectDefinitionOperationTests.Recovery.cs` | 2 | Real commit acknowledgement loss plus provider restart; two-connection-pool replay and owner-connection release |
| `tests/Integration/CanDoItAll.Tests.Integration/ProcessCatalogAuthorityPersistenceTests.ProjectDefinitionOperations.cs` | 2 | Real governed Serializable duplicate; expired real process claim with receipt lookup before mutation acquisition |
| Total | **17** | PostgreSQL owner integration |

Suggested discovery/execution filter:

```text
FullyQualifiedName~ProjectDefinitionOperationTests|FullyQualifiedName~Concurrent_process_definition_attempts_recover_the_receipt_after_a_serializable_snapshot_miss|FullyQualifiedName~Expired_process_claim_can_recover_its_accepted_definition_without_reacquiring_mutation_authority
```

The normal duplicate test pauses both independent receipt reads while they still
observe no accepted operation, then permits the actual owner transactions to
race. It requires one initial acceptance and one replay, byte-identical encoded
results, one receipt and one project. It does not claim Serializable coverage.
The governed case uses the existing real process fixture and original invocation
factory. Both Serializable snapshots are established before releasing the
operation advisory-lock barrier; both locked receipt reads must miss the later
commit, and fresh recovery must still return the same accepted bytes.

Required-search faults are injected at the real Search SQL write while enlisted
in the owner transaction. Replacement changes a phase, inserts a phase and
removes an option. Whole-table phase/option checks include potential orphan rows;
root definitions, search and receipts must match their pre-attempt state. The
no-op test separately checks accepted revision equality with persisted state and
exactly one root UPDATE per full/scalar operation despite the second receipt
flush.

The acknowledgement-loss interceptor throws from `TransactionCommittedAsync`,
after the provider completed COMMIT. It then makes the original provider's
receipt reads unavailable, requiring Unconfirmed. The first provider is disposed
before a new provider opens the same externally owned test profile. Fresh lookup
and explicit retry must recover the stored exact result and one owner effect.
This does not simulate acceptance by throwing before COMMIT.

The small-pool case uses an owned PostgreSQL lease with `MaxPoolSize=2`. A SQL
interceptor observes the locked receipt read, and connection close/disposal
interception verifies that the replay owner's connection was released before
the fresh receipt query. The altered connection-string pool is cleared explicitly
after provider disposal, before the database lease is dropped; unrelated pools
and the ordinary instance are untouched.

The expired-source case wraps the real source authority only to count acquisitions.
The first write acquires its real lease. After advancing the existing process
fixture clock past the dispatch claim, lookup and original replay must not acquire
another lease. A distinct operation must acquire and fail the expired authority,
without changing the previously accepted definition.

The standard fixtures own their isolated databases/directories. Restart and
small-pool cases record ownership in their fixture scopes and dispose in reverse
order. Concurrent calls are joined in the barrier release path, with cancellation
deadlines passed to every writer. No runtime resources were created while writing
these source files.

This slice does not replace separate lifecycle, restore ABA, HTTP identity,
browser recovery or performance lanes. In particular, read denial is exercised
through the receipt access boundary and the real process read policy is exercised
by the governed cases; a full authenticated HTTP revocation scenario remains in
the API lane. Known-commit cleanup-error preservation remains a distinct failure
case from commit acknowledgement loss.
