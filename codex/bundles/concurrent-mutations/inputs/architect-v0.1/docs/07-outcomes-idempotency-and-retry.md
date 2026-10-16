# 07 · Outcomes, durable operation identity and retries

## Do not confuse a result with knowledge about a result

Existing UI guidance distinguishes rejection, commit, secondary warning and genuine uncertainty (SRC-02). Reuse those semantics across adapters; do not force every owner to replace an established public enum during the Projects fix.

| Observed outcome | Meaning | Permitted client response |
|---|---|---|
| Rejected: validation/authorization/lifecycle | Known no effect for this admitted mutation attempt | Correct the request or stop; preserve draft |
| Conflict: target content | Original edit state no longer matches | Load safe current state, resolve/reconsider, send a new intention |
| Conflict: decision basis/approval/authority | Proposal is no longer valid under its mandate | Replan or request an explicit new decision; never just refresh a token |
| Committed change | Owner commit is confirmed with accepted identity/stamp | Reconcile the submitted draft; do not replay the effect |
| Accepted no change | Owner accepted a defined no-op | Treat as completed; follow documented stamp/activity policy |
| Committed with secondary warning | Primary effect is known, a nonprimary activity failed | Retry only that permitted follow-up or a read |
| Unconfirmed to the caller | Client cannot prove rejection or commit | Reconcile operation status; prevent blind replay |
| In progress | An operation with that identity is currently admitted | Poll/subscribe with bounded delay or surface pending state |

`Unconfirmed` is a statement about the observer. It is not necessarily a durable terminal row value: the owner may already have a committed receipt that the client has not read. Conversely, "receipt not found" on a lagging/read-replica query cannot prove noncommit. Resolve authoritative status through the canonical owner database.

## Projects receipt: proposed minimum

The present runtime creation receipt and creation reservation have narrower meanings; the Projects README explicitly does not grant them durable tool replay semantics (SRC-15). The Projects pilot needs an explicit result lookup for an ordinary submitted create/update that loses its response. Before adding storage, inspect existing owning journals and decide whether an existing durable fact can fulfill every requirement; do not duplicate it merely because its type name differs.

If a new Projects-owned result record is needed, propose these logical fields (final names/mapping remain P01 decisions): owner/operation identity, canonical dataset namespace, trusted request scope, versioned semantic request hash, target ID/lifetime, operation kind, accepted stamp and accepted row-identity mapping, commit time, minimal safe result, schema/hash-domain version and retention metadata. Store it in the **same transaction** as the Projects effect. No record claiming committed may precede the commit of that effect in another database.

Keep the public payload small. Store only enough accepted values or an immutable result reference to reconcile the submission accurately; do not store every editor snapshot in every generic receipt. A later current-state read is not the original commit result and must be labelled separately, especially if another writer has already advanced the revision.

## Concurrent admission and deduplication

Use a uniqueness scope that cannot collide across unrelated owners, actors/clients, profiles or dataset epochs. The exact actor/client binding must accommodate a tool runtime invoking on behalf of an operator without granting one caller access to another caller's receipt. A display-name string is not an authenticated actor identity.

Two equal requests with the same operation ID can race. The owner must serialize/resolve that identity using a unique constraint or proven owner protocol. One effect commits; the other obtains the same accepted result. A different semantic body, basis, target or precondition under the same ID is a mismatch, not a retry. Changes to irrelevant transport metadata must not change semantic identity. Hashing/canonicalization is versioned, bounded, deterministic and secret-aware; stable hashing is not authorization.

For a short all-SQL mutation, an optional preliminary lookup can reduce work, but correctness comes from the transactional unique outcome/effect protocol. An external precommitted `InProgress` claim is not automatically required. If a design introduces one, it also needs crash recovery, lease/fencing and a rule preventing the claim from being mistaken for an applied effect.

Committed replay takes precedence over rechecking the original target revision as though this were a new write. The original revision is expected to be obsolete after success. Still check current permission to disclose the result; revoked access does not justify returning protected original values.

## Three kinds of retry

**Technical retry:** same operation ID, same original command, same basis and expected stamp; fresh transaction/DbContext after a retryable failure that is known safe to replay. Bound attempts and total deadline. Technical conflict classification must distinguish a deadlock/serialization retry from a business target conflict. The shared helper currently groups these exception families (SRC-04), so callers must not infer one policy from its boolean result.

**Semantic reconsideration:** load the relevant new facts, generate/confirm a new proposal and use a new operation ID, linked to the superseded proposal for audit. A human-selected conflict resolution is also a new intended mutation. New approval may be required.

**Outcome reconciliation:** query the original operation. Do not create a new ID simply because the response was lost. A caller that generates a new ID for every HTTP retry defeats deduplication.

The distinction follows established retry/commit-uncertainty considerations (EXT-06, EXT-10); the exact state machine here is a product proposal.

## Integration with existing runtime records

Tool invocation journals, prepared launches, workflow contribution records, storage placement intents and creation reservations may each already have an operation identity and recovery protocol (documented candidates in SRC-11/SRC-14/SRC-15). Preserve their authority. An adapter records the linkage to the owner's receipt rather than inventing another independent committed/not-committed truth.

Before a new relation is added, answer: which record proves the actual business effect, which record proves authorization, and which record records execution progress? They can be separate records as long as transitions and recovery cannot contradict the primary effect. An in-memory runtime receipt alone cannot provide cross-host durable retry protection.

## Retention, deletion and restore

Choose a maximum retry/replay horizon covering disconnected clients, retained tool checkpoints, operator recovery and scheduled work. Full result data can have a shorter retention policy than compact deduplication evidence. After expiry, either retain a bounded tombstone that refuses unsafe replay or use an explicit expiry contract that prevents old operation IDs from executing as new. Blind deletion of all receipts after N days while checkpoints can replay after N+1 days is unsafe.

Deleting a project must not accidentally delete proof needed to reconcile its old operation. Keep result disclosure and personal/secret content retention minimal. Imported receipts are history only unless a specifically designed migration protocol says otherwise. Destructive restoration requires a dataset-namespace decision: old future-client operations must not become fresh requests merely because their receipt row is absent from the restored backup.

## Fault cases required before closure

Test duplicate request before commit, after commit, while a first request is active, after caller cancellation, after restart and after receipt-detail expiry. Test same ID/different body, current target revision advanced after success, authorization revoked before result lookup, database failure during commit acknowledgement, and cleanup failure after confirmed commit. For creates, verify reservation, public ID, local lifetime and receipt refer to exactly the same accepted creation.
