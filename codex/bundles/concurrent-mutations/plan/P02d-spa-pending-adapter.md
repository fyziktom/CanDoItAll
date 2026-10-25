# P02d — Thin SPA pending-store adapter

Status: design for review, 2026-10-16. This card authorizes no implementation or
Vue writer activation by itself. SPA preparation is committed at
`2fe9e8ee1b6b7ce9e8777320b7daa19f895cfebb`; the generated protected client and its
actual OpenAPI proof remain separate prerequisites for the later editor cutover.
No build, test, installation, generation, server or database action was performed
for this card. The inactive, cleanup-blocked `.vite-hooks/_` remains untouched.

## Boundary and source evidence

Reuse the exact engine module pinned by
`packages/app/project-pending-store.manifest.json`: engine commit
`702d0a93b1081c42d5a06893e7070219aee5d089`, 20,884 LF bytes, SHA-256
`caaec982e24f9008683fe5a2be6ad81da1786a268573bc9d95fb6f5b38fddc96`.
`project-pending-store-asset.mjs` already verifies and emits these bytes and exposes
their base-aware URL through `virtual:project-pending-store-url`.

The module owns database `candoitall.projects.pending-submissions`, version 1,
the `submissions` and `metadata` stores, strict transaction completion, accounting,
global revisions, CAS transitions and acceptance dominance. The adapter owns only
checked TypeScript input/output, immutable returned values, loading/lifetime and
useful failure categories. It must contain no IndexedDB calls, localStorage
fallback, lock/lease implementation, automatic retry, HTTP call or operation-ID
creation. Do not duplicate `measure`, revision allocation or transition rules.

Native precedents inspected in the managed engine checkout are
`BrowserProjectPendingSubmissionStore.cs`, `ProjectPendingSubmissionContracts.cs`,
`ProjectPendingReferenceContracts.cs` and `ProjectPendingOperationSession.cs`.
The native store initializes once and validates results after interop; editor
session disposal only retires that session. Accepted knowledge is captured before
browser persistence and draft reconciliation. These lifetime rules carry over;
the native recovery codec's numeric-epoch representation does not.

## Exact local transport types

These types describe the already-pinned JavaScript boundary, not invented HTTP
DTOs. Keep them in the adapter file unless its size justifies a separate file.

| Type | Required representation and validation |
| --- | --- |
| `PendingScope` | Readonly `{ namespace, callerBinding }`; nonempty canonical lowercase UUID; `pc1:` followed by exactly 64 lowercase hexadecimal characters. These are server-issued binding values, not credentials or authorization. |
| `PendingKey` | Readonly scope plus `epoch` and `id`. Epoch is canonical decimal **string**, `1..9223372036854775807`; use length and lexicographic comparison, never Number, parseInt or BigInt. ID uses the same nonempty UUID validation. Reject whitespace, signs, leading zeroes and line terminators. |
| `PendingRevision` | Branded positive safe integer, at most `Number.MAX_SAFE_INTEGER`. This is browser CAS metadata, not the owner epoch. Counts/usage may be zero; revisions and stored-byte counts may not. |
| `PendingStage` | Closed union `prepared`, `dispatching`, `unconfirmed`, `accepted`, `rejected`, `closedAbsent`. |
| `PendingRecord` | Readonly schema 1, key, revision, stage, `frozenRequest`, nullable `targetProjectId`, `hadStarterPlan`, nullable `attemptId`, `hadUnconfirmedDispatch`, nullable `acceptedPayload`, nullable `absenceProof`. Copy only these fields from the wire object; validate its address against key and its positive safe `payloadBytes`, but do not expose or recompute engine accounting. |
| `PendingMetadata` | Key, revision, stage, targetProjectId and both booleans; **no payload fields**. |
| `PendingReference` | Metadata plus `payloadBytes`, for all-scope inventory and explicit local-evidence disposal. It is not an outcome proof. |
| `PendingLimits` | Readonly five positive safe integers. Initial values: 64 records, 16,777,216 total bytes, 2,097,152 frozen-request bytes, 2,097,152 accepted/absence bytes, 16 per page. The two per-record limits are retained schema-one maxima, not current server admission settings. |

Capture inputs into plain readonly values before the first await; strings are
immutable. Check UTF-16 length before bounded TextEncoder work, then enforce UTF-8
byte bounds. Freeze new result objects, keys and arrays so consumers cannot mutate
an earlier observation. Validate schema, closed values, booleans, nullability,
key/target identity and state-field consistency, including the native adapter's
stricter prohibition of `prepared`/`rejected` with sticky uncertainty. Do not use
`as PendingRecord` to turn an unchecked module result into a typed result.

`frozenRequest`, `acceptedPayload` and `absenceProof` remain distinct bounded opaque
string types. Their constructors certify only local representation and size; names
must not imply that these are server-authenticated receipts. Do not JSON parse,
normalize, stringify, convert dates or hash these bodies in this adapter. Later
generated API/session code must validate operation, request/result version, kind,
target and complete acknowledgement mapping before advancing outcome state.

The later HTTP frozen format must be versioned and retain the exact request string
sent on every explicit retry. Its wrapper overhead must fit the existing 2 MiB
store bound; a maximum-sized HTTP body plus a second escaped JSON wrapper is not
automatically storable. Check the finished representation before preparation,
without silently enlarging the retained format's limits. Raw invalid date text
and post-dispatch edits belong to the live draft and are not promised by this
frozen-submission-only recovery format.

## Methods and checked results

Use the pinned export names internally. Public methods retain the same intent;
body-bearing methods also take the original explicit scope and reject a key
outside it before calling the module. This is an accidental-disclosure guard,
not proof of current authentication. A future editor must obtain context from the
server and fence its own use of late results after route/auth/dataset changes.

| Module export | Checked result and important use constraint |
| --- | --- |
| `initialize(limits)` | Exactly supported `{ schema: 1 }`; coalesce concurrent initialization. |
| `prepare(input)` | `applied` or `unchanged`, with matching nonnull record and exact frozen text/target/starter flag. Unchanged may already be terminal; it grants no dispatch. |
| `beginDispatch(key, revision, attemptId, explicitRetry)` | `applied`, `changed`, `unchanged` or `missing`. Applied must contain the exact key/attempt and `dispatching`; only this branch can enable a later HTTP invocation. Lost acknowledgement or other branches must not send. |
| `observeAccepted(key, payload)` | `applied`, `unchanged` or `missing`; nonmissing record must be accepted and contain the identical accepted text. The later semantic validator must run first. Missing never recreates evidence. |
| `observeAttempt(key, attemptId, outcome)` | Outcome is only rejected/unconfirmed. Applied/unchanged/missing may return the current winning observation; unchanged acceptance must remain acceptance. No client-side attempt to downgrade it. |
| `observeClosedAbsent(key, proof)` | Applied/unchanged/missing; nonmissing record is closedAbsent. Do not require identical proof text on unchanged: repeated owner observations can have different observation timestamps and the pinned store retains its first proof. Validate the proof's meaning in the later API layer. |
| `read(key)` | Matching checked record or null; no automatic lookup, restore, send or decode. |
| `list(scope, take, afterKey)` | Metadata-only page. Validate requested scope, maximum page length, distinct keys, continuation equals last item when present and stays in scope. |
| `cancelPrepared`, `clearResolved` | Applied with null, changed with matching record, or missing with null. Do not convert changed/missing into a successful cancellation. |
| `listReferences(take, afterKey)` | Unscoped metadata-only page plus nonnegative safe count/bytes. Permit totals above current capacity so cleanup remains possible. Validate unique keys and continuation; do not decode bodies to display metadata. |
| `discardEvidence(key, revision)` | Applied/null, changed/matching reference or missing/null; decode through the reference shape only, never the full record shape. Explicit UI confirmation will be required before calling it. |
| `dispose()` | App-owner lifetime only; invalidates in-flight results and closes this module's connection, retaining all records. |

Pages are bounded observations, not a stable multi-page snapshot. The compound key
uses **lexicographic decimal epoch order**, not numeric or creation-time order.
Do not sort the continuation numerically, manufacture a cursor or claim a total
from `list`; only `listReferences` returns transactional usage totals. On refresh
restart pagination and deduplicate displayed observations by key/revision.

Scope filtering never substitutes for authorization. The metadata inventory stays
available without a current trusted context to help release old-binding capacity.
It must not offer body export, credentials, new operation identities or resubmit.
Exact-CAS discard means loss of local recovery evidence, not rollback or remote
absence. UI retirement and any known-accepted terminal state must precede discard;
a failed deletion keeps the evidence and cannot reopen the old draft for save.

## Loading, lifetime and failure behavior

Use two small production files:

1. `packages/app/src/lib/projects/project-pending-store.ts`: checked transport and
   its app-owned lifecycle, with an injected `() => Promise<unknown>` module loader
   for deterministic tests. It has no Vue, generated API or virtual-module import.
2. `packages/app/src/lib/projects/project-pending-store-runtime.ts`: import the
   verified virtual URL and lazily create one owner with
   `import(/* @vite-ignore */ moduleUrl)`. The URL is fixed by the build, never
   user-supplied or cache-busted. Do not activate this export from existing pages yet.

Validate all 13 required function exports before initialize; reject missing or
nonfunction exports and unsupported initialization schema. Extra exports alone
need not fail. Existing build-time byte/hash verification is the provenance gate;
do not fetch/eval or reconstruct the module at runtime. Later browser proof must
exercise the actual emitted URL under a non-root base.

One app owner holds the import and initialize promises, limits and a monotonically
increasing local lifetime generation. Concurrent callers share initialization;
failure is visible to all and permits a **later explicit** retry without a tight
retry loop. An established owner rejects different limits. Check retirement
before invoking and after each await. Dispose is idempotent, retires immediately,
waits for an in-flight import, and invokes module dispose once if import succeeded.
It never calls initialization just to dispose. Module disposal errors stay
visible. Route/component unmount disposes its editor/session, not this owner.
No reference-count-to-zero teardown: another route may immediately reuse it.

The ES module has global connection/limits/generation state per URL in one
document. Two independently created owners for that same URL would interfere.
The runtime therefore exports the single owner; do not instantiate one per
composable. Separate browser tabs have separate module instances and share only
IndexedDB transactions. A versionchange/connection close retires the engine
connection; an explicit reopen path is required after a visible storage failure.
Provide an app-owner `reopen()` action that coalesces with initialization and calls
the same module's initialize again with the same limits, even if the wrapper had
previously been ready. It does not create another owner, change keys or delete the
database. This avoids a permanently cached ready flag hiding the engine's closed
connection. No automatic recovery dispatch follows reopening.

Failure mapping preserves the original cause without logging payload bodies.
Recognize the engine's closed `.code` values and DOMException names, not message
text: capacity/payload limit; quota; unsupported strict durability; unavailable
or retired storage; corrupt/unsupported data; invalid request/transition; changed
preparation/contradictory outcome. Unknown errors stay explicit storage failure.
Some pinned paths emit plain Error for upgrade blocking and retirement; these
cannot currently support reliable fine-grained categories. Promise rejection
after a mutating store call does not prove it rolled back. The future session
must read/check explicitly before retrying any transition.

Before durable prepare or applied claim, any store failure blocks dispatch.
After dispatch, storage failure never turns uncertainty into rejection. Capture
known owner acceptance in session memory before persisting it; an acceptance put
can fail because the store does not reserve future receipt bytes. Preserve that
known outcome and do not resend or compensate it. Module/route disposal does not
clear unresolved records. Reopening lists only; it never auto-submits.

## Implementation and proof gate

After review, implement only these two files and
`packages/app/test/project-pending-adapter.node.mjs`, plus the concise SPA proof
note. The existing Node >=22.18 configuration supports type stripping, so the
pure test can import the erasable TypeScript core directly without a new package
or generated client. Runtime import/base behavior remains a Vite/browser proof.
No main.ts, Vue page, router, package, generator, pinned JS or asset-manifest edit
is required for this transport-only slice.

Plan **18 individually named, non-parameterized pure cases**, with invalid variants
looped within the relevant case: (1) canonical key/max epoch and invalid forms;
(2) safe revisions/limits; (3) UTF-8 bounds and unchanged opaque strings;
(4) input capture and frozen output isolation; (5) full record/schema/state
validation; (6) method-specific change/null/key validation; (7) only exact applied
claim usable; (8) unchanged acceptance cannot downgrade; (9) repeated closed
absence retains first text; (10) scoped list/continuation and foreign body refusal;
(11) metadata reference decoding with deliberately unsupported bodies;
(12) reference CAS results/over-capacity usage; (13) missing exports/schema;
(14) concurrent one-time initialization; (15) explicit retry after init failure
and incompatible limits refusal; (16) dispose during import/in-flight invocation
and idempotence; (17) known/unknown error mapping without message parsing;
(18) no implicit module invocation after failure/retirement. Mock exports exercise
the wrapper, not IndexedDB durability. Authored source must confirm this count
before execution; there are currently zero new authored/discovered/passing cases.

Plan four real-browser arrangements after an owned static fixture is authorized:
actual emitted module under non-root base and retained data after reopen; two
route/session consumers sharing one app owner without unmount disposal; two tabs
competing for one dispatch claim with exactly one applied result and stale-CAS
clear refusal; real accepted-put abort/quota-style failure followed by fresh read
and metadata-only exact disposal. Use the unchanged module, actual IndexedDB and
request/transaction counters. These are four proof arrangements, not an existing
runner count or backend-recovery proof. Reuse the qualified engine fixture/tooling
where practical; do not add a browser package or start an ordinary instance.
Record ownership and exact origin/database cleanup before creating any fixture.

Run the existing 20 preparation tests when source changes affect their inputs;
run the new adapter cases, app type-check/build and owned format/lint checks.
Use inspected direct executables or the established process-scoped
`pnpm_config_verify_deps_before_run=error` guard before any package-manager command;
do not repeat the observed implicit-install/root-prepare side effect. Builds and
execution remain parent-scheduled. Preserve the prior blocked hook directory.

## Gates reserved for engine/API evolution

No store change is required for this thin transport. Do not vendor-patch it to add
error codes, new stages, format migrations, reservations or different bounds.
If reliable UpgradeBlocked/StorageRetired categories are required, add structured
codes upstream and repin its tested committed blob. The current generic failure
is safe and explicit.

Before Vue activation, the generated API boundary must prove that stable receipt
text excludes transport `replayed` and retains timestamp precision. Current engine
`ProjectDefinitionAcceptedApiResponse<T>` separates `Receipt` from `Replayed`,
and lookup returns the same receipt type. Persist a versioned stable receipt
representation validated against the frozen submission, not the complete response
envelope or Date-transformed generated model. The wrapper must not invent an owner
canonical digest. If actual write/lookup wire proof cannot establish stable receipt
representation, request an engine-owned receipt wire contract; do not weaken the
store's contradictory-outcome protection or fork canonicalization in the SPA.

Native LocalWorkspace and HTTP OpenApi caller bindings differ. Same-origin capacity
is shared, but native and HTTP body formats are not interchangeable. An unexpected
format in the current scope must be reported as unsupported and remain available
through metadata-only reference disposal. Any future cross-client interchange or
format migration belongs to the engine/browser-store owner and a versioned plan.
