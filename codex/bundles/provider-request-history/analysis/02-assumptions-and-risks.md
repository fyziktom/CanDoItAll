# Assumptions And Risks

Status: preparation only, 2026-08-28. These are the accepted design assumptions, remaining implementation risks and required treatments. They do not certify implemented behavior. Read together with the [target solution](../architecture/01-target-solution.md), [lifecycle contract](../architecture/05-history-data-lifecycle.md), [security contract](../architecture/09-search-security-contract.md), [pricing/capture contract](../architecture/10-pricing-and-capture-contract.md) and [performance analysis](../architecture/07-history-performance-analysis.md).

## Assumptions

- Scope is all authorized providers in the current local instance, stable database-profile/storage lineage and security partition. It does not retrieve another server's logs or imply coverage of sibling RAG/embedding implementations that were not traced.
- Preparation changes bundle documentation only. No inference, migration, retention cleanup, build, product test or benchmark has been performed as proof of this feature. Browser verification of the live instance was unavailable because of the sandbox/runtime failure; the user's observed unpriced request is not a reproduced UI result. The relay finalizer's missing price is independently supported by source inspection.
- Existing agent, Simple Chat, workflow and relay evidence keeps its current authority. The new table contains one compact metadata entry per observed application-visible attempt or explicit legacy aggregate. A direct untracked call uses that same row as its authoritative metadata, not a second identical direct-log table.
- `EntryId` and stable canonical source identity survive source updates. Source version is a concurrency/replay ordering field. A legacy aggregate may have no `AttemptId` or actual `StartedAtUtc`; immutable `SortAtUtc` has an explicit `TimeBasis`. Unknown physical attempts, credentials and request times are never fabricated.
- A stable partition does not contain transient runtime generation or ordinary profile-switch epoch. Those values fence operations, cursors and worker scopes. Switching back to a retained profile must reveal its existing history rather than create another history namespace.
- A trusted internal caller declares canonical ownership before dispatch. A pending/missing owner record is not permission to capture an assembled prompt. Expiry of an orphan reservation does not permanently suppress independently retained canonical evidence that commits later.
- Canonical projections follow source lifetime and deletion, including records older than 30 days. Direct/relay policy retention does not shorten retained chats or workflows. A later trusted owner commit can establish its source-governed metadata projection after orphan expiry, without reviving expired payload or deleted source content.
- `Detailed` is explicitly bounded logical current-turn input/response, not exact wire replay. Only known typed current-turn shapes are eligible. Arbitrary multi-turn relay transcripts with no reliable boundary use `UnsupportedDetailShape` plus metadata. Prior conversation, system/tool/RAG expansion, binary attachments and base64 are not copied.
- A supported input is stored at most once per logical operation; retries reference that small input part and retain their own response parts. There is no content-addressed block store or cross-conversation deduplication. Reuse does not extend recorded expiry.
- Pricing preserves provider-reported evidence, including an authoritative reported zero. Otherwise it uses an immutable execution-time tariff with explicit usage/unit/currency completeness. No current-catalog lookup is presented as historic cost, and old all-zero placeholders do not become explicit free tariffs.
- Search is manual and uses live keyset pagination. It promises neither an insertion snapshot nor a stable multi-page membership set while backfill, outcomes or retention change. It never scans transcripts or launches backfill as part of a search.
- Before EGCP, metadata access is a named operator permission. Content access separately validates the selected canonical resource and current authority. Valid provider invocation/catalog access, matching subject or a known history GUID is not content permission.

Accepted bootstrap defaults and bounds:

| Policy or limit | Default / bound |
| --- | --- |
| Direct and relay metadata | 30 days; does not govern canonical source retention. |
| Optional history-owned detail | 7 days, no longer than the applicable history-owned metadata lifetime. |
| Capture mode | Light; required metadata has no silent disabled mode. |
| Input / response capture | 32 KiB UTF-8 each by default, 128 KiB hard maximum per field; valid-Unicode truncation and completeness flags. |
| Detail quota | 256 MiB per partition, enforced atomically; no silent oldest-record eviction. |
| Projection / cleanup / backfill batch | 500 by default; hard maximum 1,000. |
| Query deadline | 10 seconds server-side, with cancellation and explicit failure. |
| Query interval | Last 24 hours in the draft UI; at most 31 days per query; any retained old interval is valid. |
| Result page | 50 by default, maximum 200; at most page size plus one scalar rows fetched. |
| Implementation proof | Existing .NET 10 / EF Core / PostgreSQL and host/component patterns; no framework upgrade assumed. |

The synthetic performance workload in report 07 is a proposed validation fixture, not observed customer load. Numeric latency/allocation budgets must be evaluated on a declared baseline environment; their presence in the bundle is not evidence they have been met.

## Critical Path Risks

- Resolve the identity, source durability, authorization and query risks below at their
  named foundation gates; downstream UI work cannot substitute for that proof.

Identity/contracts and persistence proof unlock capture; capture and canonical replay/deletion proof unlock complete search and retention activation. UI work cannot waive these prerequisite gates.

| Risk | Consequence | Treatment and gate |
| --- | --- | --- |
| Observation, logical request, retry and relay-hop identity are conflated | Duplicate or missing costs; repeated legitimate requests disappear. | Keep EntryId, nullable AttemptId, logical operation and canonical reference separate. Exact shared observation identity permits multi-owner lineage; model/time/prompt equality does not. Test retries, aggregate-to-attempt mapping, shared hops and partial legacy coverage before capture integration closes. |
| Source version or runtime generation becomes row identity | Updates duplicate rows; profile changes hide old history. | Unique canonical mapping excludes source version and transient generation. Versioned compare-and-set updates preserve EntryId and SortAtUtc. Test profile switch-back, source enrichment, replay and duplicate conflicts before schema/cursor contracts close. |
| Pending owner is treated as untracked or its expiry becomes a permanent deletion | Private prompts are copied or valid retained canonical history is lost. | Reserve trusted ownership before dispatch. Timeout becomes explicit unavailable ownership. Permit later retained canonical metadata under its own lifetime, but never restore deleted sources or expired detail. Include a clock-controlled case beyond the orphan retention horizon. |
| Required audit write is detached or provider execution is retried on persistence failure | Missing requests or duplicate paid inference. | Durable start before dispatch; awaited finalization with a bounded independent token; durable retries never invoke the provider. Inject failures before dispatch, after upstream completion and during terminal persistence. Preserve honest interrupted/unknown states. |
| DB owner and outbox use unrelated contexts/transactions | Canonical evidence and projection permanently diverge. | Stage metadata intent in the same AppDbContext transaction as canonical mutation; checkpoint projection atomically. Verify rollback, commit uncertainty, duplicate delivery and concurrent workers on PostgreSQL. Post-commit callbacks alone are not durable delivery. |
| File replay repairs only pending first links | Later mutation/deletion of an already-linked source is never projected. | Require a durable metadata-only file mutation/deletion journal integrated with the owner's canonical commit/recovery protocol. Reconcile by source identity/revision and durable checkpoints; release the workspace lock before database work. Crash tests cover both first attachment and subsequent deletion. |
| Replay, backfill or policy changes resurrect deleted/expired content | Privacy deletion is ineffective; retention silently grows. | Source deletion markers dominate stale updates. Expiry is based on original event time, not replay time. Distinguish history-policy orphan expiry from canonical-owner deletion. Keep tombstones until replay frontiers make compaction safe; test late events and profile restore. |
| Pricing loses provider-reported amounts or invents complete tariffs | Known requests appear unpriced/free or wrong currencies are summed. | Preserve reported/calculate/explicit-free provenance independently of completeness; accept authoritative reported zero. Validate long counters, exact models, cached/reasoning semantics and supported currencies/units. Retain unavailable historical evidence instead of repricing from today's catalog. |
| Instrumentation assumes every path uses one runtime handle | MAF, media, batching, relay or diagnostic calls remain invisible. | Maintain the typed boundary inventory and test production composition for each path. SDK-internal retries are separate only if observed. Health/model operations remain explicitly operational unless a priced child inference is actually observed. Reopen capture scope when another path is discovered. |
| Neutral history projects acquire producer-specific dependencies or grow broad managers | Cycles and difficult maintenance negate the architecture work. | Keep Abstractions independent; owner adapters remain in their source assemblies. Small collaborators own capture, policy, query, persistence and lifecycle separately. Apply dependency/type-responsibility gates and file-growth review before downstream integration. |

Privacy and storage risks require their own proof, even when basic search works:

| Risk | Treatment |
| --- | --- |
| Metadata permission opens another owner's conversation | Every detail request repeats partition, owner existence/retention and resource authorization. No client-supplied owner marker, trace, subject equality or remote reference grants access. Test forged IDs and denied-owner links. |
| Raw keys or sensitive payloads leak through metadata, errors or browser state | Persist only validated credential IDs and bounded approved snapshots. No headers, token hashes, provider configuration, raw exceptions or binary body copies. Redact known patterns before permitted detail persistence; disclose that arbitrary user text cannot be universally sanitized. |
| Encryption or key-ring failure produces plaintext recovery | Use the existing persistent protection boundary; return explicit content unavailable on missing keys. Test restart, key loss and restore without reading production secrets. No plaintext fallback. |
| Same-operation input reuse bypasses quota or outlives its references | Count each persisted shared input once, account atomically with per-attempt response parts, and release quota only with durable deletion/ref-lifetime updates. Define accounting in persisted protected bytes and verify concurrent capture/purge and failed writes. A retry does not renew input expiry. |
| Retention cleanup removes active work or unrelated canonical data | Cleanup touches only history-owned data through its owner adapter, excludes active attempts and uses bounded transactional batches. Recover stale starts honestly before expiry. Source-owned projections follow source deletion rather than deleting the source. |

Performance risks and treatments:

| Risk | Treatment |
| --- | --- |
| History calls the existing all-source usage facade or loads provider entities | Dedicated scalar index query; range/filter predicates in SQL; no body columns, per-row source lookup, eager count or source-file enumeration. Assert physical reads and query counts, not only returned rows. |
| Offset paging or mutable sort keys skip/duplicate results | Immutable SortAtUtc + EntryId cursor, explicit TimeBasis, validated cursor/filter/partition binding. Use live membership semantics; test equal timestamps, late backfill and concurrent outcome changes. |
| Detailed logging serializes the whole request before truncating | Project allowed typed fields and enforce byte limits before unbounded encoding/copying. Current-turn input once per logical operation; per-attempt response bounds; media references only. Measure logger allocations separately from existing provider payload construction. |
| Backfill holds the workspace lock or shares an unbounded work queue | Capped resumable batches, durable file/DB checkpoints and visible coverage. Release file locks before DB/network waits. Use bounded durable worker leases; an in-memory queue cannot be the correctness path. |
| Token totals overflow or broad summaries dominate search | Use checked/wide counters and separate explicit bounded SQL aggregation. Keep unknown and partial usage/cost distinct from zero. Do not attach summary materialization to every page. |

## Validation Risks

- Distinguish source inspection from executed proof. Missing database/browser fixtures,
  skipped or zero-discovered tests, and unmeasured performance remain explicit gate failures.

| Limitation or weak-proof risk | Required evidence before implementation closure |
| --- | --- |
| Live browser unavailable during preparation | Later use the approved browser/test environment and isolated host. Verify both tabs, manual Search, cancellation, scope changes, authorization errors, content states and existing component wrappers. Do not label the source-derived design as a verified live UI fix. |
| Static pattern counts are mistaken for runtime measurements | Report 07 records actual scan counts and proposed budgets only. Establish repeatable pre/post baselines for query plans, latency, allocation, record count, storage and cancellation on the same declared machine. No speculative speedup claim. |
| EF InMemory/SQLite masks PostgreSQL behavior | Run real PostgreSQL coverage for timestamp precision, UUID/cursor ordering, indexes, transactions, unique constraints, concurrency, bounded deletion and migration/restore. Inspect generated SQL and representative plans. |
| Scope-specific CodeAnalytics snapshot omits a producer/test project | Reconfirm changed source and sibling boundaries at each phase; use explicit source searches and actual impacted-test seeds. A missing symbol in a scoped snapshot is not proof of absence. |
| Successful normal-path tests miss crash boundaries | Inject failure at durable reservation, canonical commit, outbox/journal publication, projection commit, checkpoint, quota update and deletion. Repeated restarts/replays must converge without another provider request. |
| Time and concurrent workers make retention tests unreliable | Use controlled clocks, deterministic leases and deliberate interleavings. Test pending-owner expiry followed by a valid late canonical commit, separate from deleted-source and expired-detail cases. |
| Profile fences cover invocation but not maintenance/detail readers | Switch profiles during query, owner attachment, outbox/journal processing, cleanup, quota mutation and content resolution; verify writes remain in the original allowed scope and transient epochs do not rewrite identity. |
| Existing acceptance setup can mutate providers/secrets | Run owner integration and two-instance acceptance only on disposable fixtures. Do not run destructive fixture setup or real paid inference against the user's live instance for convenience. |
| Migration/backfill coverage is overstated | Show indexed-through/coverage and error state until the resumable backfill finishes. Search never starts a hidden full scan. Test rollback/restore with stable source identity mappings and deletion checkpoints. |
| Source changes during a long bundle execution invalidate preparation | Record the starting revision/diff and actual dependency graph when execution begins. Re-evaluate moved boundaries, migrations and tests instead of treating this source inventory as permanently current. |

Existing test homes/selectors and the proposed workload are listed in report 07. Prefer extending the owner suites for integration; new focused fixtures are justified for new history contracts. Passing unit tests does not substitute for database, privacy, lifecycle or UI proof.

## Reopen Triggers

Reopen the earliest affected contract/work unit and revalidate its dependents when any of these occurs:

- A source cannot provide a stable canonical identity, immutable sort time with honest TimeBasis, or reliable attempt boundary. Do not substitute provider/model/time matching.
- A new source version changes EntryId, an ordinary profile switch hides history, or a legacy record requires an invented AttemptId/StartedAtUtc.
- The design or tests hide retained canonical evidence merely because it is older than 30 days or arrived after orphan reservation expiry.
- File storage cannot durably journal later mutation/deletion, or a crash can lose its projection/deletion intent. Pending-row polling alone is insufficient.
- A deleted source or expired payload reappears through replay, restore, backfill, quota recovery or a new policy revision.
- Required audit can be lost by cancellation, detached tasks or queue pressure, or a persistence retry sends another inference.
- Canonical transcripts, assembled prior messages, arbitrary relay bodies or media bytes must be copied to satisfy the proposed Detailed feature. Full wire replay/content-block storage is new scope and needs a redesigned preparation.
- Provider-reported cost/zero cannot be preserved, another currency/unit needs support, or model/usage evidence cannot justify a calculated price. Reopen pricing, not just display formatting.
- Query authorization requires per-row source reads, untrusted context can suppress logging, or content can bypass the source owner's permission/retention checks.
- Query/backfill/cleanup cannot meet bounded-work or atomic quota requirements on the measured fixture. Revisit indexes, storage and workload assumptions before relaxing proof.
- A previously untraced provider path, SDK retry layer, sibling repository or diagnostic inference must be covered.
- A new dependency cycle, producer reference from neutral code, broad manager or unexplained file-growth exception appears.
- The user requires exact multi-page snapshots, full-text body search, external federation, export, chargeback or EGCP person mapping. Those require a scope/design update rather than an implicit extension of this bundle.
- Browser, PostgreSQL, migration or failure-injection evidence remains unavailable at implementation closure. Keep that proof gate open; preparation alone is not completion.
