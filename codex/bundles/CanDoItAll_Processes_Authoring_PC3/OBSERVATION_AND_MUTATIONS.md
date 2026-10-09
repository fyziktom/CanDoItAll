# Observation, drafts and command results

## The PC2 rule to retain, and the rule to change

Retain the distinct read generation, write-operation identity, completion sequence and exact
opening/profile/project-lifetime fencing. A refresh or tab change is not the cancellation
of a write. A different definition/incarnation or disposed opening must not receive its
predecessor's result, error, notification or finally cleanup. Profile notifications remain
subscribed/unsubscribed correctly.

Change the blanket rule that every version unequal to an accepted version is stale. That
rule masks the current request-local owner reset, but cannot support a durable store shared
by two editors. Equality of opaque tokens is useful; ordering them lexically or by timestamp
is not a causality contract. Introduce an owner-defined revision/observation relation scoped
to the exact aggregate/incarnation. A monotone aggregate revision is one valid implementation.

## Required observation behavior

| Incoming observation | Clean draft | Dirty or invalid draft | Receipt |
| --- | --- | --- | --- |
| Older than known committed revision | Retain confirmed content and stale/read status | Preserve draft/raw text/baseline | Retain exact committed receipt |
| Confirms the submitted commit | Adopt accepted normalization/identities | Merge against the immutable submitted values; preserve later edits | Confirm it without replay |
| Authoritative newer revision | Advance current content and baseline | Preserve local fields/raw validation; expose conflict and deliberate reconcile/discard path | Keep historical receipt independently |
| Incomparable/wrong owner or retired incarnation | Do not apply | Do not rebind the draft to it | Never move an old receipt into the successor |
| Read unavailable | Keep explicitly stale authorized state | Keep draft with recoverable warning | Preserve known outcome |
| Access/admission revoked | Invalidate sensitive accessible view as policy requires | No save under the revoked authority | Do not use historical success to grant current access |

A render-only session scope does not prove server transaction ownership. Current profile ID
plus public project ID is not enough to rebind a retired project's mutation. Origin tokens
used for UI fencing may be compared, but trusted admission is captured/validated natively.

## Five panels, one logical revision

A role change can affect step references and canvas labels; a step or import changes the
canvas; a definition lifecycle change affects catalog and runnable selection. The native
result must identify the committed aggregate revision and changed sections, and the host
must reconcile relevant loaded projections atomically. Load still-unopened sections lazily
from that revision or a classified newer observation. Do not mix r2 roles with r1 expected
step/canvas version while advertising one coherent document.

Do not update all VersionTokens blindly. For every dirty panel keep: original baseline,
immutable submitted patch when pending, current local values/raw buffers, and separately
observed remote revision. A new revision can be accepted only through a defined reconciliation
that preserves its intended changes. A conflict is not permission to silently auto-resubmit.
The owner's revision is the concurrency identity, not the per-render request counter.

## Preserve detailed PC2 behavior

Add/Delete role results adopt their selected identity only while the original selection
operation is still eligible. Deleting the last unreferenced role clears selection, dialog,
validation and pending submission. Later explicit selection wins over an older response.
Save merges normalization field by field against the submission, never the pre-edit baseline.

Step patches use the actual selected step's DecisionRoleKey and preserve the other steps.
Pair rows by real stable identities; preserve untouched normalized values, later changed
fields and raw invalid numbers, and do not copy deleted-row fields into a surviving row.
The same applies when authoritative add/import results allocate new canonical keys.

Coalesce only final canvas positions for the same target nodes. Never coalesce distinct Add,
Clone or Import intents. Queued geometry must use the confirmed current revision only after
reconciling node existence/meaning; a disappeared node cannot be recreated by stale movement.
An import retains its captured source/target despite later library search/preview/selection.

## Outcome and recovery protocol

Known validation/policy/concurrency refusal is distinct from a commit. Preserve the draft,
show safe diagnostics, retire that submission and permit explicit correction. A refused
operation cannot later be interpreted as an accepted one by a stale callback.

Known commit returns durable operation/receipt and committed revision even when notification,
read-back or UI reconciliation fails. Secondary failure becomes a warning/status, not a second
write or a new operation ID. Only the matching operation can release its busy slot.

For a truly unknown result, preserve the captured operation ID and submitted payload, block
blind duplicate actions and offer an explicit native status/recovery path. Resolve the same
operation under its original admitted owner; do not switch to the currently selected profile.
After a new host/session, the durable operation record must be queryable by an appropriately
authorized caller. A confirmed rejection/absence under the owner's contract permits the
defined retry; a timeout alone does not. Same ID/different payload is always a conflict.

Do not solve this by serializing draft text, credentials or authority into URLs or browser
storage. Keep only permitted opaque locators where existing navigation/recovery rules allow;
server-side authority and receipt data remain the truth. No background write replay is
triggered solely because a component rendered or a reconnection occurred.

## Mandatory controlled scenarios

Test both orderings of read versus commit; accepted r1 followed by delayed r0; accepted r1
then confirmed r2 from another editor (clean and dirty); role r2 invalidating stale step r1;
unknown after real commit then a second host retries the same ID; rejected patch followed
by corrected explicit intent; queued move after node deletion; import completion after
selection changes; and profile/project/definition A -> B -> A success/error/finally races.
Assert exact native operation counts and persisted revisions, not just UI text.
