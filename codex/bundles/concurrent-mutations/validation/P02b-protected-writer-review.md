# P02b protected writer — independent source review

Status: read-only review, 2026-10-16. No builds, tests or passing runtime evidence
are supplied here. The coordinating owner is implementing the replay-race
correction below; this record does not claim that correction has been validated.

Inspected `ProjectDefinitionOperationProtocol`, `ProjectDefinitionWriter`,
`ProjectMutationAuthority`, `ProjectDefinitionReceiptReader`,
`ProjectDefinitionOperationStore`, `ProjectOperationEpochService`,
`CoordinatedDatabaseTransaction` and `SearchIndexing`.

The protected writer's first receipt lookup precedes source authority acquisition.
An existing accepted operation can therefore return its original result without
requiring new effect authority. Lookup validates the current namespace/caller,
receipt access, stored result version and indexed result metadata; the protocol
also compares the semantic digest before replaying different submitted intent.

The operation scope key includes namespace, epoch, caller kind/key and operation
ID. It serializes duplicate operations independently of target-project locks.
The receipt key provides the durable uniqueness constraint. Search projection
uses the owner's existing PostgreSQL transaction through the coordinated context;
owner rows, children, search projection and receipt are saved before the same
commit. A receipt encoding/search/write failure must therefore roll back the
whole transaction. Actual PostgreSQL failure-injection tests remain necessary.

`CommitAttempted` is recorded before the commit path; `Committed` follows known
commit completion. A lost commit acknowledgement remains Unconfirmed. Known
commit plus source/owner cleanup failure preserves Accepted. Activity runs after
commit and logs failures without changing the accepted outcome. Replayed paths
must continue to bypass search writes, owner mutation and activity effects.

One concrete replay race was found: governed writes establish a Serializable
snapshot during epoch admission before waiting for the operation advisory lock.
A duplicate can acquire that lock after the first operation commits, yet its
transactional `FindLockedAsync` still cannot see the newer receipt. Gating a fresh
lookup on that stale existence read produces a rejection/serialization failure
instead of exact acceptance. CAS and receipt uniqueness prevent a second commit,
but the duplicate must recover its accepted result explicitly.

The coordinating owner selected a correction that avoids nested connection-pool
pressure: capture receipt observation separately, release the owner transaction
and source lease, then perform a fresh authorized receipt lookup. A known
Serializable rollback may also trigger that lookup outside the transaction. It
must compare original intent, authorize/decode the exact receipt, and only then
set Accepted/Replayed. Receipt existence alone is not accepted-result authority;
access-denied, unsupported/corrupt detail and mismatched intent remain distinct.
This is a recovery read, never an implicit second mutation attempt.

Required focused proof includes two same-operation governed calls overlapping
the first commit, receipt/search/owner rollback together, lost commit response
followed by lookup, same-ID changed-intent rejection, denied receipt access, and
post-commit cleanup failure preserving exact acceptance. A bounded concurrent
case should also prove that recovery releases its owner connection before taking
the separate lookup connection.
