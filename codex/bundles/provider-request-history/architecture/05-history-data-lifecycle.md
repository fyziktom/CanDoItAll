# Identity, Storage, Projection And Lifecycle Contract

Normative design for SB01/SB03/SB04/SB05/SB06. Proposed names are not existing APIs.

## Typed Identity

| Concept | Shape and invariant |
|---|---|
| `HistoryPartition` | Stable origin-instance identity + database-profile/storage-lineage identity + trusted security partition. These survive process restarts and switching away/back. Profile is selected by the host, never by an untrusted DTO. |
| `HistoryExecutionFence` | Transient active-profile generation and authorization revision captured by the host. Used to reject stale work, never as persistent row identity or historical visibility partition. |
| `HistoryEntryId` | Stable server-created GUID for every indexed row, including LegacyAggregate. Never changes when source evidence is updated. |
| `SortAtUtc` / `TimeBasis` | Immutable index timestamp plus provenance: actual attempt start when known, otherwise the canonical source's recorded timestamp. Actual StartedAtUtc stays nullable for old evidence; never fabricate a request start. |
| `ProviderIdentity` | Local provider-profile GUID; preserve provider name/kind snapshot after rename/deletion. |
| `ProviderModelIdentity` | Exact ordinal external model ID. Preserve requested/resolved model plus imported source/publication/public-route/upstream mapping where known. No display-name identity. |
| `ProviderRequestId` | Server-created GUID for one logical operation; correlation with run/chat/batch is separate. |
| `ProviderAttemptId` | Server-created GUID per application-visible provider-call attempt. Never reuse batch InputId or trace/correlation ID as an attempt ID. |
| `CanonicalEvidenceReference` | Stable identity: closed source kind + source instance/profile + owner/run/conversation/operation ID + evidence ID/ordinal. Source version/generation is a separate concurrency value, never part of uniqueness. Opaque values only at storage/protocol boundaries. |
| `OwnerLink` | Links an attempt to agent, chat, workflow/process, batch item or relay evidence. Role distinguishes primary evidence, content owner and additional lineage. |
| `RemoteRequestReference` | Configured source identity + observed publisher response request ID. Relation only; not permission, local uniqueness or proof of success. |

Use readonly record structs/records and enums for these concepts; do not create a
string/object metadata dictionary. The neutral Abstractions project has no dependencies.
A caller's claimed workload/context is never accepted as authority to suppress recording
or read content. Only an internal adapter can supply a canonical owner reservation.

## Granularity And De-duplication

- New records identify `ProviderCallAttempt`, not necessarily each SDK-internal HTTP retry.
  A transport retry count or child record is present only when actually observed.
- Buffered simple-chat legacy invocation evidence may aggregate two empty-response
  attempts. Mark it `LegacyAggregate`; never invent individual attempts from aggregate tokens.
- Agent/workflow observations that retain the exact same observation/attempt ID can
  produce one row with two owner links. Provider/model/time/prompt equality is insufficient.
- Distinct genuine calls remain distinct even with identical messages, model, token
  counts, correlation or timestamp. Same-subject/different-credential calls stay separate.
- Replaying or advancing a source version upserts the same stable entry and owner mapping.
  Old versions cannot replace newer evidence. Conflicting facts
  for the same immutable identity are a typed conflict, not last-writer-wins.
- Older sources without trustworthy cross-source identity remain separate with explicit
  granularity/coverage; do not deduplicate guesses. Existing aggregates must not count
  new metadata projections as additional use.
- One source legacy aggregate linked to several newly captured attempts is owner evidence,
  not an extra row to sum with those attempts. Backfill consults the source-to-attempt
  mapping before emitting a legacy row.

## Tables And Ownership

Names are proposed and must be mapped to existing naming conventions in SB03.

| Table | Contents and constraints |
|---|---|
| `ProviderRequestHistoryEntry` | EntryId primary key in the stable partition. Nullable AttemptId is unique when present; LegacyAggregate has no invented attempt. Immutable SortAtUtc/TimeBasis, nullable actual start/end, compact identifiers, elapsed/queue duration, operation/workload/diagnostic classification, outcome, nullable usage, price summary/provenance, caller snapshot, capture/owner/coverage states. No transcript, provider configuration, bearer token, binary media or exception body. |
| `ProviderRequestHistoryOwner` | Typed owner references and roles; required EntryId FK and unique partition/stable-source/evidence/EntryId tuple. Version and role updates do not create a new link. EntryId is nonnullable even for legacy rows, so PostgreSQL nullable AttemptId semantics cannot defeat uniqueness. Separate source and attempt identities preserve multi-owner lineage without duplicate charges. |
| `ProviderRequestDetail` | Optional bounded current-turn detail parts and safe parameters with byte counts, redaction/omission/truncation state and expiry. Input is stored once per logical operation/validated input revision and linked by its retry attempts; each observed attempt owns its response part. Unique typed owner/part/policy snapshot keys and restrictive FKs prevent duplicate retry inputs. No general message-block/content-addressed store. Never filled for a pending canonical content owner. |
| `ProviderHistoryProjectionOutbox` | Metadata-only source mutation, source generation/version and replay identity. Same DB transaction as the owner change. Bounded worker lease/retry; no provider execution. |
| `ProviderHistoryCheckpoint` / deletion marker | Source-specific backfill cursor, high-water mark, lag/error state and monotonically ordered deletion/expiry evidence. Prevent stale replay from resurrecting removed data. |

The entry's `MetadataAuthority` is `CanonicalProjection` or `Standalone`.
`RetentionAuthority` is independently `CanonicalOwner` or `HistoryPolicy`.
Relay is a canonical projection with HistoryPolicy retention, since its existing audit is
already the canonical request log. Agent/chat/workflow projection uses CanonicalOwner
retention. A new direct call uses Standalone + HistoryPolicy.

Keep existing relay request-ID uniqueness, state/concurrency checks, restrictive ownership
constraints and recovery. Do not migrate all relay rows into the new entry table or add
a second standalone relay entry. Retain explicit unknown model/provider identity only
for legacy evidence; new resolved attempts require valid identity.

## Capture And Owner Commit

1. Resolve provider/model and authorized caller, freeze immutable request-time policy,
   pricing and profile/security context; create logical/attempt IDs.
2. Declare source ownership before dispatch. `PendingCanonical` is never interpreted
   as untracked merely because the conversation row is not committed yet.
3. Ensure a durable start reservation:
   - Relay reuses its existing successful `BeginAsync`; a metadata projection intent is
     enlisted with that same canonical transaction.
   - File-backed agent and standalone attempts reserve the neutral entry before provider use.
   - Simple-chat/new per-attempt calls reserve the entry unless an existing same-transaction
     dispatch record can provably carry/replay the full attempt identity. SB04 uses the
     reservation path by default; any elision requires characterization proof.
4. Execute exactly once at the observed adapter boundary. Retry policies create another
   attempt ID; persistence retry never re-executes the provider.
5. Finalize usage/outcome under a bounded token independent of caller cancellation.
   Existing source evidence remains authoritative for its transcript and for usage/price
   only at the same proven granularity. An operation/legacy aggregate linked to multiple
   attempts supplies lineage/content; it must never overwrite each attempt with aggregate
   tokens, total cost or aggregate long-context tariff selection. Do not distribute an
   aggregate across attempts by guessing.
6. Attach owner evidence after durable canonical commit. Projection only advances by source
   version and terminal evidence; an older callback cannot restore a superseded state.

The record state separates `Started/Succeeded/Failed/Cancelled/TimedOut/Interrupted`
from `OwnerPending/Linked/Unavailable/Deleted`, usage completeness, detail completeness
and projection freshness. Recovery marks interruption/unknown billing when it cannot
prove the provider result; it never asserts that nothing ran.

A stream is terminal only after its terminal protocol event/end-of-stream policy.
Enumeration creation, headers, first text and HTTP 200 are not success. Cancellation,
disposal and observed final usage must merge deterministically so a late unavailable
cancellation cannot erase richer captured terminal usage. No detached finalization task.

## Transaction And Replay Mechanics

- The EF owner adapters live in their existing persistence/module assemblies. They
  reference the history Persistence integration surface only to stage a metadata outbox
  item in the same `AppDbContext` and `SaveChanges` transaction as the canonical mutation.
  The staging helper accepts `AppDbContext` inside Persistence; EF never leaks into neutral
  producer ports. No ambient transaction or second independent context is assumed atomic.
- Direct entry/detail changes use one history-store transaction. Detail failure after a
  durable start leaves an explicit detail failure state; it does not erase metadata.
- A scoped worker acquires a database-profile lease, reads at most 500 projection intents,
  applies versioned idempotent changes, and advances the checkpoint in its transaction.
  Leases are cross-instance, bounded and cancellable. Durable failure remains visible;
  no in-memory channel is the sole delivery path.
- New file-backed attempts have a pending entry with exact trusted source IDs. After the
  atomic canonical evidence write, a small owner adapter publishes its metadata version.
  A crash between the two is repaired by reading that pending entry's exact source path,
  not scanning every workspace or reserializing its whole state. Pending-reference repair
  is supplementary: the reservation can expire before a late first canonical commit.
- For every file owner, an owner-local durable journal records first canonical creation/
  attachment as well as every relevant evidence update and deletion. It remains the
  replay authority even when a pending reservation has already expired. Under the existing workspace lock, persist a prepared
  metadata-only intent before canonical mutation, atomically replace the source with its
  version/commit marker, then mark the intent committed. Recovery reconciles the exact
  source identity/version; it never assumes two separate file writes were atomic. A
  deletion tombstone is durable before physical removal and retained until projection
  acknowledgment. An incomplete delete is reconciled explicitly against source state.
  The DB checkpoint advances only after the corresponding idempotent index change.
  SB05 must prove crashes at each handoff, concurrent writers and a first canonical
  commit after orphan expiry followed by a crash before publication; without this gate,
  linked-owner updates/deletions and complete coverage cannot be claimed.
- Legacy file backfill is a separate bounded resumable enumerator: persist source manifest/
  partition cursor and last accepted source identity/version, process a capped batch,
  yield the workspace lock before DB work, and retry overlaps idempotently. Do not hold a
  workspace lock over the whole scan or over network/database waits. If current file layout
  cannot support incremental progress, add a metadata manifest beside the source through
  an owner-specific adapter; do not change transcript format or invent per-attempt evidence.
- Legacy DB backfill uses source-side keyset predicates and scalar projection. Search
  never initiates a backfill, catalog refresh, token refresh or source-file discovery.
- Search exposes indexed-through/coverage gaps on explicit load. Incomplete projection
  or source failure is an explicit status, never a complete empty history.

## Retention And Deletion

Proposed defaults:

| Setting | Default | Validation |
|---|---:|---|
| Standalone/relay metadata retention | 30 days | Positive bounded duration, 1–3650 days. |
| History-owned detail retention | 7 days | Positive and no longer than metadata retention. |
| Capture mode | Light | Closed Light/Detailed enum; no silent disabling of mandatory metadata. |
| Maximum input/response text | 32 KiB UTF-8 each | Positive, maximum 128 KiB per field; truncation on valid Unicode boundaries. |
| Detailed storage quota per partition | 256 MiB | Positive configurable byte budget; enforce atomically across concurrent captures. |
| Cleanup/projection/backfill batch | 500 rows | 1–1000; one leased worker per partition; bounded retry and time budget. |

HistoryPolicy retention begins at the original attempt timestamp, not indexing/retry
or owner-attachment time. For legacy relay rows use their recorded request timestamp;
missing timestamps remain an explicit migration exception, not today's date. CanonicalOwner
lifetime comes from the existing owner. Shared input detail expires at its original
logical-operation deadline and is not extended by a later retry or surviving link. Changing
settings does not silently extend existing expiry. Shorter retention is a reviewed
destructive operation; the settings UI shows its impact and requires explicit Apply.
Existing detail remains governed by its recorded deadline when mode changes to Light;
an explicit purge action may shorten it. Enlarging a duration affects future rows only
unless an operator explicitly requests a governed migration.

Canonical projections follow their owners and can be older than 30 days. Canonical
transcript retention is not reconfigured by this feature. On source deletion/expiry,
suppress its link/projection with a versioned deletion marker; preserve the row only
when another legitimate retained owner still justifies it. A late trusted canonical
commit may be independently indexed under that retained owner's lifetime even after an
orphan's HistoryPolicy metadata expired; reuse retained non-content identity mappings
when available. It must not revive an expired detail, expired standalone authority, or a
source whose deletion/tombstone wins. Reconciliation never extends content retention.

Expired detail is inaccessible immediately by query predicates even before physical GC.
Delete payload first, then history-owned terminal metadata, in bounded FK-safe batches.
Relay canonical rows and projections are purged through the relay owner's adapter.
Do not delete Started records to evade recovery; terminalize stale rows with honest
Interrupted evidence before applying the expiry policy. Provider or token deletion
must not cascade away unrelated history; retain safe identity snapshots.

Detail quota exhaustion omits optional body with `QuotaExceeded` and keeps mandatory
metadata. No silent oldest-row eviction. Failed required metadata persistence fails
before inference; persistent storage pressure is an actionable operator condition.

## Migration, Transfer And Rollback

- Add tables/columns/indexes with forward-compatible defaults. Legacy nullable fields
  remain explicitly unavailable. Include the new EF configuration assembly in
  `AppDbContextModelRegistry` through composition and in the PostgreSQL migration model.
- Stage identity/capture support before enabling readers. Backfill source metadata only;
  no inference replay, full transcript copy, fake old credential IDs or today's prices.
- Profile switching, transfer/export/import, recovery and cleanup use the existing
  database-profile fence. Freeze a transient generation at begin; either finish in that
  same leased profile or explicitly fail/recover there. Never continue old work into the
  newly active DB. Recheck the fence in every recorder/finalizer/worker and before publishing
  query/content results. Stable storage-lineage identity, not that generation, keys the
  persisted partition so a restart or switch back does not hide history.
- Transfer policy must include standalone entries/detail/outbox/checkpoints as owned data;
  derived projections may rebuild only if their original source and identity mapping
  survive. Preserve origin instance namespace or remap atomically; do not manufacture collisions.
- Roll back feature activation first. Keep additive data intact, drain/recover pending
  writes, and retain a usable canonical-source path. No automatic destructive down
  migration or dropping historical audit records. Test restore on disposable data.
- Retention runs only after SB03 migration/rollback and SB05 ownership/deletion gates pass.
