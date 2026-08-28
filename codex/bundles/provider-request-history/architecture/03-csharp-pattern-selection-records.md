# Pattern Selection Records

These are decisions tied to observed responsibilities. They are not a request to add a
framework or one class/interface for every pattern.

## ADR01 — Canonical Ownership Plus A Read Projection

**Pressure:** existing agent, simple-chat, workflow and relay records already hold the
evidence, but no bounded cross-source request index exists.

**Decision:** keep those sources authoritative and add scalar index entries/owner links.
Only otherwise untracked calls have history-owned canonical metadata/detail. Search never
runs the existing usage-source aggregate API. This is a focused read-model separation,
not a new event-sourcing architecture.

**Rejected:** another universal request transcript/usage store; UNION/read-all of every
source at Search time; approximate deduplication by provider/model/prompt/time.

**Cost and proof:** incremental consistency, source versions, deletion and coverage must be
visible. Multi-owner and legacy aggregate fixtures prove the index is not a second charge.

## ADR02 — Typed Adapters And One SDK Decorator

**Pressure:** MAF IChatClient bypasses generic provider handles; streaming callback results
are not usage; relay owns its terminal stream and audit.

**Decision:** share small lifecycle/capture policy, but observe actual typed boundaries.
Use an IChatClient decorator inside the existing application retry for MAF, typed
buffered/stream chat adapters, image/voice/item operation adapters and the existing relay
audit. Existing factories construct these collaborators through production composition.

**Rejected:** object/reflection payload traversal, a universal TResult observer, a replacement
runtime, or logging from only one higher-level entry point.

**Cost and proof:** more than one adapter, each small and responsible for one protocol shape.
Production factory tests must prove every identified path and no duplicated observation
through nested relay/direct capture. SDK-internal retries remain unclaimed unless observed.

## ADR03 — Real Transactional Outbox And File Commit Journal

**Pressure:** canonical evidence commit and asynchronous index update can fail separately.

**Decision:** stage metadata intent in the owner's actual DbContext transaction. File
owners use durable prepared/committed metadata journal records and exact-source recovery
under their existing lock. Leased workers apply idempotent versions and acknowledge only
after index commit.

**Rejected:** detached tasks, in-memory channel as the only delivery path, opening a second
DbContext and calling it atomic, reading every workspace repeatedly, or copying prompts
when the source is late.

**Cost and proof:** outbox/journal lifecycle and tombstones require bounded cleanup and
crash/replay tests. Retention cannot purge safety markers while stale replay remains possible.

## ADR04 — Explicit Identity, State And Price Evidence

**Pressure:** one operation can retry, one observation can have several owners, and unknown
price is currently rendered as Unpriced.

**Decision:** distinct logical request, actual attempt, stable entry, canonical source and
remote observation identities. Versions are concurrency, not identity. Separate execution,
owner, usage, price and detail states; preserve ProviderReported, Calculated, ExplicitFree,
partial and unavailable reasons with currency/provenance. Freeze configured tariffs before
dispatch. Existing state/concurrency machinery remains authoritative where present.

**Rejected:** correlation ID as attempt identity, unknown as zero, today's price as historic
truth, renaming every old zero tariff to free, or booleans whose combinations hide state.

**Cost and proof:** compatible nullable legacy migration and exhaustive state tests.
Use enums/readonly records and named validation; no string/object metadata bag.

## ADR05 — Application Ports Only At Real Boundaries

**Pressure:** runtime producers cannot depend on EF or concrete owner modules; queries need
authorization and deterministic tests.

**Decision:** neutral typed ports at persistence, host policy, canonical owner and capture
boundaries. Application orchestrates them. Composition injects a finite source adapter
collection keyed by a closed source kind and rejects duplicates. Pure algorithms are
ordinary concrete functions/classes with TimeProvider where time matters.

**Rejected:** one trivial interface per helper, service location, UI resolving DbContext,
an outer-feature reference from Abstractions, and callback bags hiding dependencies.

**Cost and proof:** three small projects and explicit registrations; graph/public-signature
guards and production host registration tests justify that separation.

## ADR06 — Bounded Current-Turn Detail

**Pressure:** full assembled prompts repeat long conversations and can contain tools,
retrieved documents, credentials and binary media.

**Decision:** default Light has no prompt snippets. Opt-in Detailed captures typed bounded
current-turn input once per logical operation/input revision and per-attempt response,
with expiry/quota/redaction/completeness. Canonical content is opened through its owner.
An arbitrary relay transcript without a trustworthy current-turn boundary is explicitly
unsupported for body capture.

**Rejected:** full-wire transcript replay, content-addressed message-block storage, blind
last-user-message heuristics, plaintext fallback, and silently treating late canonical
ownership as untracked.

**Cost and proof:** details are intentionally incomplete and labeled. Stream first-chunk,
allocation bounds, retries and secret/binary fixtures prove the advertised boundary.

## ADR07 — Shared UI Controller With Separate Form Authority

**Pressure:** two views need the same query semantics; existing provider EditForm encloses
Sharing and can treat Enter as Save.

**Decision:** one typed History panel/controller, hosted with SingleProvider or AllAuthorized
scope. Draft and applied query state are distinct. Hoist provider tabs outside the mutation
form; only editable panes share ProviderProfileEditorForm and its existing EditContext.
History owns its search form. Workspace owns the policy editor via neutral ports.

**Rejected:** two copied search implementations, nested forms, only changing button type,
a new Workspace-to-AgentFramework reference, or DataGrid in-memory paging over all records.

**Cost and proof:** a small form-boundary extraction plus component/browser regression.
Search/Enter, tab switches, draft filters, cancellation and overlays must be tested.

## ADR08 — Live Keyset Search

**Pressure:** retained history can be large and changes while paged.

**Decision:** immutable SortAtUtc + EntryId ordering, scalar indexed predicates and a
bounded protected cursor. Show query time, TimeBasis and projection coverage. Explicit
Refresh sees late rows before the cursor; no multi-page snapshot claim.

**Rejected:** offset-based deep paging, automatic total counts, a commit-sequence service,
a long database transaction across UI pages and pretending a PostgreSQL sequence is commit order.

**Cost and proof:** no arbitrary page jump; Previous can reflect current data. Tied timestamps,
late backfill, deletions and authorization-revision changes have explicit test cases.
