# P02b client, API and migration review

Reviewed the P02b implementation card and architecture 06/07 against P02a's current
request capture, accepted acknowledgement and explicit native recovery. This is design
review only: no P02b source, build, test, migration or execution proof is claimed.

The accepted-only receipt design, exact result/current-read distinction, guarded epoch
closure, watermark-before-purge and explicitly managed restore boundary are coherent.
The following corrections must be concrete before enabling protected client writes.

## 1. Preserve the original caller partition as a checked precondition

The full operation address includes trusted caller scope, but the current proposed wire
operation carries only namespace, epoch and operation ID. After changing OpenApi to
AuthenticatedSubject, or switching subjects, a restored pending envelope could look
NotObserved in the new partition and execute a second effect. Prose forbidding remapping
does not provide a client restart/API fence.

Add a non-authoritative `ProjectCallerBinding` to write-context metadata and the frozen
operation envelope. It is an opaque stable, versioned fingerprint of the server-selected
caller partition in the dataset, not issuer/subject text, a credential or a permission.
The adapter derives current authority normally and compares the expected binding before
lookup or effect. It never uses the supplied binding to choose a principal/scope. A mismatch
is an explicit caller-context-changed result; it cannot become a new partition lookup followed
by execution. Native/open/secure adapters use this same checked precondition. Persist it with
the original pending record. Reusing it after an auth-mode change grants no access to the old
partition; that requires a separately reviewed recovery authority, if later offered.

Decisive cases: identical subject with rotated credential retains the binding; changed
subject, auth mode or surface cannot replay into a fresh partition; unknown/malformed
binding cannot select another user's historical payload. Random operation IDs remain
collision avoidance, not proof of permission.

## 2. Freeze protocol interpretation as well as values

Persist command schema and normalization/digest version before first dispatch. The writer
must retain the necessary comparison/decoder support throughout the issued receipt horizon.
An application upgrade must not normalize an old pending request under new rules and then
misclassify it as another semantic request. Unknown/unsupported versions fail explicitly
without new effects; already accepted bytes remain historical data, not a new current read.

Canonical equivalence is intentional: whitespace or date-kind representations that normalize
to the same owner meaning can share a digest. Retain the original frozen submission values
separately for UI reconciliation; the digest is not the user's editable draft. Child order,
blank submitted positions and original expectation stay significant because accepted slot
mapping depends on them. Never reserialize a later current definition as the original request.
Count/UTF-8 bounds must precede collection copying and normalization allocations, not only
the final codec check after immutable command construction.

Decisive cases: semantically equivalent raw inputs, different blank slot positions, old
request replay after supported codec upgrade, unsupported version, and oversized in-process
collection before copying. A changed desired value is a new intentional operation even if
the previous request was known rejected; the client does not mutate a frozen pending record.

## 3. Rejected attempt does not resolve an ambiguous operation

With accepted receipts only, a pre-admission rejection or rolled-back transaction leaves no
durable record. Another attempt with the same address may still be in flight or may have
committed after a fresh lookup. `Rejected` therefore describes this attempt, not proof that
the operation has never committed.

Client uncertainty is sticky once any dispatch becomes Unconfirmed. A later rejection plus
NotObserved in an open or deadline-expired-but-draining epoch must not clear the original
pending record or permit an automatic new ID. Exact Accepted resolves it. A fully retained,
authoritatively closed epoch can establish absence only after admitted transactions drain;
return that closure observation explicitly if the client is allowed to resolve it. A retired
epoch, expired detail, missing disclosure rights or changed dataset never proves historical
rollback. Even authoritative absence does not automatically submit a replacement intent.

Keep these states distinct in the lookup contract rather than relying on explanatory text:
open/draining NotObserved; closed-and-retained absence; expired/unavailable historical
outcome; exact accepted receipt. The API may use shared status codes, but typed payloads
must preserve the distinction. A failed first attempt without any earlier uncertainty can
remain a normal known rejection; the protocol must not force a generic rejection journal.

Decisive case: first attempt is paused before commit, a duplicate times out or is denied,
fresh lookup is empty, and the first then commits. The client must keep the same pending
address and recover the accepted result, not create another command.

## 4. Make restart retention explicit and bounded

The card currently allows session-only recovery while 06/07 describe client restart
retention. Choose a release requirement. For durable native recovery, persist a versioned
record containing caller binding, full operation reference, frozen typed command, digest
version, submission status and optional exact accepted payload before dispatch. Do not
serialize EditContext, FieldIdentifier dictionaries, object references, live leases or bearer
credentials. These are not durable addresses or authority grants.

Use per-operation keys under dataset/caller binding, not one editor slot that a second tab
can overwrite. Store failures before dispatch prevent dispatch visibly. Quota/expiry cannot
silently evict unresolved operations. A storage event or restart never automatically submits
an operation. Reconcile current binding/permissions before showing stored payload; do not
render another scope's draft after account change. This storage is bounded same-origin
plaintext data, not a new confidentiality boundary between people sharing a browser profile.

Persist exact Accepted separately before applying it to the draft; failure to store or apply
it cannot change known commit into known rejection. Clearing a record is conditional on its
exact operation/version so one tab cannot clear another operation. If clearing fails, retained
accepted evidence may be shown again, but cannot authorize another effect.

The frozen dispatched command alone does not preserve typing performed afterwards across
circuit/browser restart. Either separately checkpoint a bounded latest typed draft with a
reviewed row-reconciliation scheme, or explicitly limit the restart promise to the frozen
pending submission and recovered accepted result. Existing in-memory field/reference merge
cannot be assumed to survive serialization. P02a's raw incomplete native date limitation
also remains; do not claim browser fragments were persisted merely because DateTime values
were. Starter-object seeding requires its own operation/result and is never inferred from
a replayed project-definition receipt.

## Migration and restore findings

No additional restore mechanism is needed beyond the card's explicit offline activation
state machine. Keep ordinary migration, idempotent first bootstrap, alias attach and
destructive reactivation distinct. Applying schema migration must not silently rotate or
adopt namespaces. A restored matching SQL/control-plane pair cannot prove its own freshness;
the mandatory post-restore step and stopped-writers prerequisite must remain in runbooks and
the acceptance drill. There is no defensible automatic detection guarantee for a restore
that bypasses this supported operation.

Retained request/receipt codec support and issued epoch parameters are migration inputs.
Changing defaults cannot shorten an old horizon. Logical import must preserve target operation
evidence and never turn source history into active target replay rights. Closed/retired state
must be read from a fresh guarded transaction after snapshot or lock failures.

The current 1,000-option performance fixture exceeds the proposed 256-option limit. That is
already identified in the implementation card and remains a concrete rollout decision,
not permission to reduce the fixture silently or claim compatibility from empty-journal
benchmarks. Capacity, WAL, cleanup and restore proof remain parent-owned release gates.

These corrections refine the existing owner-specific protocol. They require no generic
mutation framework, precommitted InProgress journal, new identity service or new UI history
feature. The persistence design owner is reconciling them into the implementation card.
