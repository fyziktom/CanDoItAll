# Provider request history: canonical records, durability, and performance

Status: architecture proposal and source analysis only, 2026-08-28. No application code, database, retained prompt, secret, or live provider request was changed or inspected. No build, test suite, benchmark, SQL execution plan, or runtime timing was run for this report. The two mandatory performance passes were executed in order: direct source review first, then the pattern skill and its relevant references.

## Scope and workload assumptions

The feature is manual history search, including requests received by shared providers and requests without an existing conversation owner. It is not a second conversation store, a billing ledger, an IDM integration, or exact wire replay. The existing usage summary remains a separate read model; attaching a history tab must not implicitly run its unbounded source readers.

For implementation validation, propose an isolated PostgreSQL fixture with 1,000,000 retained attempts, 20 provider profiles, 100 models, a mix of canonical/direct/shared origins, and 10 concurrent searches while bursts of 50 attempts/second are recorded. These are test assumptions, not measured production traffic. The reference fixture must declare CPU, RAM, database version, storage, payload distribution, and cold/warm state before evaluating latency targets.

The focused pattern corpus is the 14 source files listed below, totaling **6,362 lines**. Smaller files were read in full; the 3,372-line execution slice store and 664-line workflow store were inspected around the relevant read, write, identity, and query paths. Structural counts additionally cover product C# under `src`, excluding build output, migrations, generated `*.g.cs`/`*.generated.cs`, and `wwwroot`. This is not a claim that every product method received a performance review.

## Existing canonical evidence and search paths

| Owner or path | Existing identity and evidence | Consequence for this bundle |
| --- | --- | --- |
| Agent execution | `ProviderUsageObservation.Id`; nullable provider profile, run, agent and chat session IDs; provider/model, phase, response/request IDs, workflow/process/correlation fields, usage and execution-time price evidence. `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs:23` | Link to the canonical observation and run. Provider/model are search dimensions, never uniqueness keys. Legacy string correlation fields do not establish a trusted cross-owner match. |
| Agent persistence | Per-run `run.json` and `usage` records plus orphan usage; `LoadProviderUsageEvidenceAsync` enumerates every run directory and usage file. `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs:55` | Build a small query projection from commits and bounded reconciliation. Do not call this full-history reader from a history search or row renderer. |
| Agent lock boundary | The evidence read holds the store semaphore and cross-process workspace lock. `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs:302` | Parallelizing more readers does not remove this serialization boundary. Keep new projection I/O outside the canonical lock after recording a durable change marker. |
| Agent history mutation | Usage is inserted into an existing run detail through the existing execution mutation path. `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:144` | Extend the durable run commit integration with a small metadata export; do not save a second run or transcript. |
| Simple Chat invocation | `(OperationId, Ordinal)` is the primary key; provider/start index; separate outcome, usage and pricing evidence with snapshot hash/version. `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/EntityConfigurations/LlmChatOperationConfigurations.cs:41` | Reuse the invocation as canonical evidence. Preserve attempt ordinal and operation identity separately. |
| Simple Chat content | `LlmChats_Messages` has entry identity, text, unique conversation/sequence and conversation/turn/role indexes. `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/EntityConfigurations/LlmChatConversationConfigurations.cs:49` | Resolve existing turn/message content only after an explicit detail request and authorization. No copied prompt/response in history rows. |
| Simple Chat usage query | Scalar projection and `AsNoTracking` are already used, but the four-way join has no date/provider predicate or page bound before `ToListAsync`. `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Usage/SimpleChatProviderUsageProjectionSource.cs:29` | Reuse semantics, not the unbounded query, for history. |
| Workflow usage | Immutable observation ID, invocation ID and attempt; provider/model, workflow/node/run origin and price evidence. Mapping an agent/provider observation preserves `observation.Id`. `src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowUsageModels.cs:57`, `:160` | Workflow attribution can describe the same observed provider execution as an agent fact. Preserve one attempt and its owner/context links; do not add both costs. |
| Workflow persistence and process attribution | Immutable append verifies duplicate facts; timestamps are canonicalized to PostgreSQL microseconds; SQL aggregates cast token sums to `long`; process-origin IDs are stored and queried. `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs:20`, `:121`, `:328`, `:361`, `:573` | Reuse immutable-fact conflict behavior and explicit process/workflow context. Do not make the neutral history assembly depend on process or workflow persistence. |
| Workflow paging | Existing `ListPageAsync` performs `CountAsync`, then `Skip/Take`; page size only has a positive lower bound. `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs:87` | This is useful existing filtered-query behavior, but not the unlimited-depth pagination contract for new all-provider history. |
| Shared relay audit | Existing `SharedProviderInvocationRecord` already records request/publication/provider IDs, authenticated subject, access context, trace/correlation, public and upstream model, outcome, usage, price, expiry and concurrency token. `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecord.cs:6` | Keep this authoritative relay record. It contains no prompt/response body, so replacing it does not remove duplicated conversation content. |
| Shared relay integrity | Unique `RequestId`, publication/start and expiry/completion indexes, restrictive publication/profile FK and optimistic concurrency. `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecordConfiguration.cs:78` | Preserve relay idempotency and terminal-write semantics. Add a projected search row, not a second authoritative relay log. |
| Shared relay query | Reads every invocation joined to a full provider profile, then maps/groups in memory. `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs:29` | New history must project only scalar metadata and must not join mutable provider configuration for historical truth. |
| Usage aggregate facade | Every selected source is read concurrently, contributions are materialized and deduplicated by workload/contribution ID, then consumer/provider/model summaries are built. `src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs:8`, `:81`, `:129` | Do not implement history by adding UI filters after this call. Its current workload-scoped deduplication is not a universal physical-attempt identity. |

Supporting CodeAnalytics snapshot: `snap-20260828134930-4eb1620a`. A focused type/reference query confirmed the relay record and its audit/configuration/transition dependents. That snapshot covers 10 selected projects; absence from it is not proof that another source or caller does not exist. Source searches supplied the file-backed agent and workflow evidence above.

## Pass 1: Initial Performance Review

**Summary assessment.** The main scaling risk is unbounded evidence I/O and materialization while holding an agent workspace lock. No measured slow endpoint was supplied; the design should prevent work from growing with all retained transcripts on each search.

**Root cause.** Existing usage readers return whole-source contribution sets and the aggregate facade filters only by workload. The agent reader physically traverses records, and the relay reader materializes provider entities per joined result. A manual button alone limits frequency, not the cost of each request.

**Recommended changes.** Introduce a bounded metadata query path, project only fields needed for the result page, and resolve canonical content separately. Keep existing execution-time usage/pricing evidence and immutable source identity. Use server-side range/filter predicates, deterministic cursor pagination, and explicit source completeness. Keep existing aggregate semantics compatible until their replacement is separately justified.

**Expected impact.** Search work becomes proportional to an indexed range and a bounded page instead of all retained bodies. This is a complexity/design expectation, not a speedup estimate; no latency or allocation improvement has been measured.

**Trade-offs.** A derived index requires durable reconciliation and deletion handling. Additional small metadata rows and indexes are justified; duplicating canonical text or introducing a second direct-call metadata ledger is not.

## Ownership and storage decision

Use one neutral per-attempt metadata table with typed ownership/authority, plus an optional bounded detail payload table. For a direct/untracked call, the metadata row is authoritative. For an already tracked call, it is a query projection linking to the canonical owner. Do not create both a direct-call log table and an identical direct-call search table.

Keep `SharedProviderInvocationRecord` authoritative for relay protocol audit in this bundle. Replacing it would require simultaneous migration of its idempotency, publication/profile FK, recovery and terminal-write behavior, without reducing prompt duplication. A pure federation of per-source readers is also rejected: the agent source currently has no indexed per-attempt provider/date search. Neither alternative is smaller while satisfying the required query bound.

The proposed `ProviderHistory.Abstractions`, `.Application`, and `.Persistence` split is sufficient. Contracts have no product project references; application policy and query/content orchestration depend on neutral contracts; EF/storage/worker code owns persistence. Owner adapters stay with their existing agent, Simple Chat, workflow and relay owners. No neutral assembly references those owner implementations, Blazor, or `ProviderManagement`. This is a real boundary; do not add a repository/factory interface for every trivial helper.

Persist at least these concepts with typed values rather than string commands or JSON-discriminated domain objects:

- History partition: stable instance/workspace, database-profile/storage-lineage and trusted security-partition identity. Transient runtime generation, profile-switch epoch and authorization revision fence operations/cursors; they are not persisted row identity or retention boundaries.
- Stable `EntryId` for every row, including `LegacyAggregate`. New observed dispatches also have an `AttemptId`, logical operation ID and positive attempt ordinal; legacy `AttemptId` may be null. A source version is a compare-and-set/replay ordering field, never part of the canonical source identity or a reason to create another entry. Relay hop identity remains separate.
- Provider profile ID and provider/model snapshots, requested versus effective/upstream model when different; missing legacy profile identity is explicit.
- Trusted owner declaration, canonical record key, optional typed attribution to agent/session/workflow/process, and source revision/checkpoint.
- Immutable `SortAtUtc` and explicit `TimeBasis` identifying observed start versus source-recorded evidence time. Actual `StartedAtUtc` is nullable for legacy evidence; never rename a known recorded timestamp to a fabricated start. Source enrichment must not change the sort key. Operation, nullable start/completion timestamps, terminal outcome and stable failure category remain separate from usage/pricing completeness.
- Execution-time cost/currency/provenance and available pricing snapshot identity; distinguish unknown, known zero and calculated cost.
- Trusted client/credential reference and display snapshot if available. Never raw API keys, request authorization headers, secret values, or unconstrained claims dumps.
- Content availability/completeness, typed retention authority, applicable metadata/detail expiry, projection state and optimistic concurrency token.

One primary canonical content owner and typed secondary attribution are enough for the observed paths. If multiple independent owner references require relational links, use a small relation containing only typed keys; never another cost-bearing row. A shared provider hop and its caller can legitimately have distinct local request rows. Cross-instance correlation is a relationship, not authority to deduplicate charges or expose remote content.

### Attempt granularity and existing limitations

The non-streaming `ProviderBackedLlmInvocationAdapter` can retry an empty response twice and returns aggregated usage (`src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs:35`). `AuditedLlmChatInvocationPort` records ordinal `1` around that aggregate (`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/AuditedLlmChatInvocationPort.cs:26`, `:99`). Legacy records therefore cannot always prove individual physical attempts.

The streaming adapter already exposes `LlmStreamingAttemptStarted` and terminal attempt evidence (`src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmStreamingInvocationAdapter.cs:35`, `:85`, `:217`). New capture must observe the actual retry/dispatch boundary and preserve that identity through the owner adapter. Do not infer physical attempts from token totals, timestamps or prompt hashes.

Legacy migration is metadata-only. Preserve canonical IDs and a typed `LegacyAggregate`/`ObservedAttempt` granularity. Empty legacy IDs require an owner-supplied stable record key, not a current enumeration index or name/model match. Unknown provider IDs remain unknown. A stable name is a display/filter value, not permission to attach an old record to a newly created provider.

### Durable indexing and owner races

| Situation | Required behavior |
| --- | --- |
| Direct dispatch | Persist the single started attempt metadata row before irreversible provider dispatch. If required audit persistence fails, return a typed failure before sending the request. Finalization is awaited with a bounded commit deadline independent of caller cancellation. |
| Expected canonical owner | The trusted caller declares the expected owner before dispatch. `PendingCanonical` is not `Untracked`; absence of a saved owner record must never trigger prompt copying. A durable started reservation or existing durable owner dispatch record must cover a crash before terminal owner persistence. |
| Owner and index in one database transaction | The owner adapter stages neutral metadata changes using the existing unit of work and profile fence. Commit owner facts and the projection together when practical. Do not open an unrelated factory context inside that transaction and claim atomicity. |
| Deferred DB projection | Commit an idempotent metadata-only outbox event in the same owner transaction. Worker writes the projection and its checkpoint atomically. A durable outbox is only needed where projection is deferred; do not build a general event bus. |
| File-backed owner | Add a replayable metadata change marker to the existing canonical commit/journal protocol while under its lock. The marker records scope, owner key, revision and affected attempt IDs, not bodies. An adapter publishes the small projection after the canonical commit. Durable markers/checkpoints survive process failure; no detached task is the delivery guarantee. |
| File reconciliation | Reconcile bounded batches from durable markers or a checkpointed source manifest, validating source revision. A one-time explicit legacy backfill may enumerate old files in bounded resumable batches, but search never invokes it. Publish coverage/checkpoint status; do not claim complete history before it finishes. |
| Index succeeds, checkpoint fails | Replay by the same attempt/owner identity is a no-op for equal facts; conflicting facts fail visibly. Advance the checkpoint only with the successful idempotent projection commit. |
| Owner commit fails or is late | Keep explicit pending/unavailable owner state and observed metadata. A late valid owner attachment uses the same stable identity. Even after the orphan reservation expires under HistoryPolicy, independently retained trusted canonical evidence can be indexed under CanonicalOwner lifetime. This does not revive expired payloads or a deleted source. Never manufacture a second attempt, copy the conversation as recovery, or rerun the provider call. |
| Cancellation or streaming failure | Preserve an already observed attempt and partial usage/completeness. Final audit persistence must not disappear merely because the request token was cancelled. No token-by-token database write is added. |
| Worker wakeup or process crash | Durable storage is the source of truth. No unawaited write, in-memory-only queue, unbounded channel, or drop policy may be responsible for history correctness. Use bounded batches with explicit retry/error state. |
| Profile switch | Capture the database profile/fingerprint/generation at operation start and fence reads, writes, worker scopes, and detail resolution. An old operation cannot resolve a new active factory and write into the new profile. Stop/retry in the original permitted scope; never redirect silently. |

Existing durable seams to reuse: `EfLlmChatUnitOfWork` owns transactions and post-commit callbacks (`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatUnitOfWork.cs:35`); callbacks alone are not durable delivery. `DatabaseProfileLlmChatCommitFence` wraps durable operations in the recorded profile snapshot (`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Runtime/DatabaseProfileLlmChatCommitFence.cs:10`). Shared audit already verifies identical begin/finalize replays (`src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationAuditService.cs:11`, `:75`).

### Canonical deletion and retention

- Proposed history-policy defaults: untracked/direct and relay audit metadata 30 days, optional captured detail 7 days, detail never longer than the corresponding metadata. These are validated bounded options; lowering retention must have documented retroactive cleanup behavior. The relay currently establishes a 30-day lifetime at start, but has no discovered expiry consumer.
- Canonical agent, Simple Chat and workflow projections use `RetentionAuthority.CanonicalOwner`, not that 30-day cutoff. Their metadata stays searchable for as long as the owner retains the evidence. Any bounded 31-day interval inside retained source history can be searched, including old intervals. This avoids hiding records the user already stores.
- Query and detail resolution enforce the relevant authority's expiry/deletion immediately. Physical cleanup lag does not make expired content readable. Canonical content keeps its own retention and access policy; history settings cannot extend it.
- For `RetentionAuthority.HistoryPolicy`, anchor expiry to original observation/attempt time, not projection or retry time. For canonical projections, preserve source lifetime/deletion state; do not assign a new `now + 30 days` cutoff. Replaying an expired history-policy fact cannot extend its lifetime. Later independently retained canonical evidence may establish its owner-governed projection even after orphan expiry; source deletion and payload expiry are never reversed by that attachment.
- Canonical deletion revokes detail resolution immediately and purges/suppresses its projected history row unless another proven retained canonical owner still grants the same observation. An independently authoritative direct/relay hop has its own lifetime; it must not keep a deleted transcript copy. Deleting a history row never deletes canonical chat/workflow content.
- Owner deletion/revision markers and projection checkpoints prevent a stale export or backfill from resurrecting deleted content or associations. Tombstones can be compacted only once all relevant replay checkpoints have crossed them.
- Purge expired payloads and terminal metadata in batches, proposed default 500 and hard maximum 1,000. Order by expiry and stable ID; support concurrent workers through transactional claiming/`SKIP LOCKED` or a verified equivalent. Do not materialize all expired records into the EF tracker.
- Exclude active attempts from ordinary deletion. Recover abandoned attempts using bounded timeout/lease rules, then expire them. The existing relay recovery already selects bounded candidates (`src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecoveryService.cs:15`).
- Relay `DeleteAfterUtc` is present and indexed, but the product-source search found only its record, mapping, transition and audit uses; no expiry deletion consumer was found. Implement and test relay cleanup, not just a settings field.
- Existing Simple Chat event retention is separate from transcript/invocation retention: 7-day event default and 500-event cleanup batch (`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Application/Application/LlmChatStreamingOptions.cs:21`). Its PostgreSQL delete is already bounded with `LIMIT` and `FOR UPDATE ... SKIP LOCKED` (`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatOperationEventRepository.cs:150`). Do not delete transcripts when pruning provider history.

## Detail capture without conversation duplication

**Accepted v1 scope assumption for review:** `Detailed` means bounded, redacted logical current-turn input and response, not a byte-exact full prompt or wire transcript. Default is light metadata. For canonical owners, detail always resolves existing authorized content; no retained provider-history payload copy is created, including during the owner persistence race.

For supported untracked typed text shapes, optional capture stores at most 32 KiB UTF-8 of current-turn input and 32 KiB of response by default. Truncate on valid Unicode boundaries and record original byte counts plus `Truncated`/`ContextNotCaptured` completeness. Enforce size limits before constructing an unbounded serialized representation. Store only safe structured error categories, not raw exception text.

Do not heuristically extract a last message from an arbitrary external transcript and call it the current prompt. If the invocation contract cannot establish a reliable current-turn boundary, record typed `UnsupportedDetailShape` plus light metadata. Shared arbitrary multi-turn relay traffic can still be searched and priced; full prompt capture is not promised. System/prior-conversation instructions are not copied by this mode. Media, attachment content and base64 are never copied; keep only authorized references and bounded metadata.

Capture a supported logical input at most once for its operation; repeated attempts link to that bounded input instead of copying it. Responses remain attempt-specific. The small payload representation must preserve this reference relationship without a second direct-call metadata record. A canonical owner may be attached later only by trusted explicit identity, never a content hash heuristic.

Full multi-turn replay would require scoped immutable content blocks/manifests, reference lifetime tracking and more privacy controls. It is deliberately outside this bundle. This avoids claiming exact full-prompt fidelity while silently dropping or repeatedly storing the conversation prefix.

## Query and index contract

Both tabs use the same application query contract. Selecting a tab, provider, date or filter changes draft state only. An explicit Search/Refresh applies a validated immutable query; cancellation and a request generation prevent an older response from replacing newer results. Detail loads only on explicit expansion. Changing scope clears old results and fences subsequent requests.

| Concern | Proposed bound or invariant |
| --- | --- |
| Time range | UTC half-open `[from, to)`; default last 24 hours; maximum 31 days per query. Any such interval in retained canonical history is valid; only direct/relay rows follow the 30-day history-policy default. Explicit invalid-range error, never silent broadening. |
| Result page | Default 50, maximum 200. Fetch at most `pageSize + 1` scalar rows to determine continuation. No automatic exact `CountAsync`. |
| Ordering | Immutable descending `SortAtUtc, EntryId` for observed attempts and legacy aggregates. Preserve actual nullable `StartedAtUtc`/`AttemptId` and explicit `TimeBasis` separately. Cursor binds normalized filters, stable partition, transient profile/authorization fences and the final sort key. The database comparison uses the same ordering semantics as the index. Live keyset paging makes no insertion or multi-page snapshot promise; later backfill, outcome changes and retention can change membership. |
| Scope/provider filters | Required authorized scope first, optional provider/model/client/credential/publication/workload/outcome/completeness filters in SQL. Exact model identity by default; do not copy the workflow display-name uppercase normalization into provider/model identity. |
| Historical names | Snapshot labels are returned from metadata; deletion/rename does not hide history through an inner join to current provider configuration. |
| SQL projections | Only list DTO fields; no transcript/content/secret/property-bag columns, EF entities or per-row owner reads. No application `Where` after full `ToListAsync`. |
| Aggregates | Separate explicit bounded summary query when requested, with SQL aggregation and `long` token/count totals. Unknown usage/cost stays unknown; do not use zero as a substitute. |
| Query deadline | Proposed command deadline 10 seconds; propagate cancellation. Surface partial coverage, pending indexing and failures explicitly. |
| Initial indexes | `(Partition, SortAtUtc DESC, EntryId DESC)`, `(Partition, ProviderProfileId, SortAtUtc DESC, EntryId DESC)`, `(Partition, CredentialId, SortAtUtc DESC, EntryId DESC)` where credential filtering is supported. Stable EntryId primary identity; unique partition/non-null AttemptId and stable canonical source-key mapping, excluding source version and transient generation. |
| Additional indexes | Add provider/model/time or publication/time only after representative plans show their need. Use an expiry/time/ID index for cleanup and an available-at/ID index for outbox work. Do not index arbitrary text or all filter combinations. |
| Payload lookup | One explicit authorized detail lookup by EntryId, then bounded owner content resolution. Legacy rows need no invented AttemptId. Expired/deleted/pending/unavailable states are normal typed results. |

The credential index uses a stable non-secret reference from trusted authentication context, never an API-key prefix, a raw key, or a client-supplied arbitrary label. Cross-instance identifiers are not accepted as local authorization. Query permissions must not imply permission to read canonical conversation content.

## Pass 2: Deep Pattern Scan

After Pass 1, loaded `analyzing-dotnet-performance` and `critical-patterns`, `async-patterns`, `collections-and-linq`, `io-and-serialization`, `memory-and-strings`, and `structural-patterns`. Regex construction signals were absent; critical regex checks were still run. Every selected detection recipe was executed with `rg`; counts below are **matching source lines**, not automatically confirmed defects. The explicit corpus excludes tests and generated code.

### Scan execution checklist before classification

| Recipe / confirming inverse | Exact hits |
| --- | ---: |
| Literal `IndexOf` without comparison | 0 |
| `Substring` | 0 |
| Literal `StartsWith`/`EndsWith` without comparison | 0 |
| Literal `Contains` without comparison | 0 |
| All `IndexOf`/`StartsWith`/`EndsWith`/`Contains` candidates | 18 |
| `StringComparison.Ordinal*` | 22 |
| Parameterless `ToLower`/`ToUpper`; invariant casing | 0; 4 |
| Three chained `Replace`; any `Replace` | 0; 0 |
| `params` declarations | 0 |
| LINQ `All/Any(char...)`; all `All/Any` | 0; 4 |
| `async void`; sync-over-async candidates; `Task.Run` | 0; 0; 0 |
| `async` declaration candidates; `ValueTask`; `Task.WhenAll` | 65; 5; 2 |
| New byte/char arrays; `stackalloc`; `ArrayPool` | 0; 0; 0 |
| `string.Format`; compound `+=` | 0; 11 |
| `new Regex`/compiled regex; generated regex | 0; 0 |
| `IndexOfAny`/`ToCharArray` | 0 |
| `ContainsKey`; `TryGetValue` | 0; 29 |
| Static readonly dictionary; static readonly frozen dictionary | 0; 0 |
| `new List<T>`; `new Dictionary<T>` | 15; 7 |
| `StringComparer.CurrentCulture` | 0 |
| Collection recipe `Select/Where/Cast/Take/Aggregate` | 90 |
| Core LINQ recipe `Select/Where/OrderBy/GroupBy` | 132 |
| `ToList/ToArray` sync/async materialization | 62 |
| `new HttpClient`; HTTP `SendAsync/GetAsync`; `ResponseHeadersRead` | 0; 0; 0 |
| Explicit `new JsonSerializerOptions`; all option declarations | 0; 2 |
| Serializer call lines; source-generation declarations | 6; 0 |
| `new FileStream`; asynchronous FileStream option | 1; 1 |
| `ReadAsync/WriteAsync` candidates | 1 |
| Unsealed concrete class declarations; sealed class declarations; sealed partial declarations | 0; 21; 0 |
| Explicit `IEquatable` implementations | 0 |

Manual inverse and false-positive checks:

- **21/21** scoped eligible concrete class declaration sites are sealed. The broader structural-only scan found **2,231/2,288** sealed eligible declaration lines, with 57 unsealed candidates across 2,395 source files. Partial declarations are counted as lines, not unique types. These candidates were not proven leaf types; no codebase-wide sealing change is proposed.
- **2/2** actual string-search calls have ordinal comparison; the other 16 search matches operate on collections. The four casing calls intentionally normalize persisted workflow display search keys; new provider identity remains exact.
- **2/2** JSON options instances are reused, including target-typed `new`. The six serializer lines contain **7 calls**, all using those options; none uses source-generated metadata. Generic existing workspace serialization is not a reason to refactor every serializer in this bundle.
- **1/1** FileStream constructor uses asynchronous and sequential-scan flags. The `ReadAsync` candidate is an application projection method, not a stream byte-array overload; there are zero confirmed legacy stream overloads.
- All **11/11** `+=` matches are numeric, not string concatenation. There are zero branched replace chains, string-format sites, or char-LINQ findings.
- The seven explicit dictionary constructions include six method-local sites and one static empty-snapshot initialization. Fifteen explicit list constructions include one capacity-specified list. These are counts, not a recommendation to replace every collection or LINQ expression.
- Five ValueTask-returning declarations were checked; no repeated awaiting of a ValueTask was found. The two `Task.WhenAll` sites operate on Tasks; re-awaiting those tasks for their result is not the ValueTask antipattern.

### New findings only, deduplicated against Pass 1

#### DP1. Retry input preparation can multiply large-input allocation (1 chain, Moderate)

**Impact:** The buffered retry loop rebuilds prior-turn objects and copies attachment byte arrays per attempt; adding a whole-request logging serializer here would multiply those allocations again.

**Files:** `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs:43`, `:127`, `:208`, `:262`.

**Fix:** Project the bounded logical history input once outside retries, retain scalar per-attempt metadata and references, and never serialize/copy attachment bodies for logging. Reusing the existing provider payload itself would require proof of driver immutability and is not part of this preparation-only change.

**Caveat:** Allocation size and timing were not measured. This matters for large inputs and retries; it is not evidence that ordinary small requests are slow.

#### DP2. Reusing the workspace JSON comparison/write chain would multiply projection serialization (1 chain, Moderate)

**Impact:** A changed existing usage projection can serialize current and next values for comparison, then serialize next again for writing: three serialization calls, not one. Full-source rebuilds would also repeat the I/O already identified in Pass 1.

**Files:** `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs:1987`, `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs:80`, `:301`.

**Fix:** Use per-attempt metadata upserts and small durable change markers for new history; do not append a new all-history JSON projection to the existing comparison/write pipeline. Keep the current run store behavior unchanged unless separately profiled and tested.

**Caveat:** The three-call path requires an existing projection whose serialized value differs. Unchanged comparisons use two calls; earlier short-circuit conditions change the path. No speedup estimate is claimed.

Positive consistency evidence:

- Simple Chat scalar usage projection and workflow SQL aggregate queries already demonstrate the desired SQL-side projection approach.
- The existing event retention repository demonstrates bounded transactional deletion; reuse its concurrency principle rather than adding a load-all/delete loop.
- Existing no-tracking readers, async file streams, immutable-fact duplicate checks and cached JSON options should be retained.

### Focused scan corpus and source-size guard

| Source file | Lines |
| --- | ---: |
| `src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs` | 294 |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Usage/AgentProviderUsageProjectionSource.cs` | 152 |
| `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Usage/SimpleChatProviderUsageProjectionSource.cs` | 174 |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs` | 242 |
| `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecord.cs` | 58 |
| `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecordConfiguration.cs` | 104 |
| `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationAuditService.cs` | 141 |
| `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/LlmChatInvocationEvidenceCapture.cs` | 58 |
| `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/AuditedLlmChatInvocationPort.cs` | 129 |
| `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs` | 292 |
| `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatOperationEventRepository.cs` | 273 |
| `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs` | 409 |
| `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs` | 3,372 |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs` | 664 |

Do not add history querying, capture policy, retention and worker logic to the existing large stores or split it into new partial files. Add small cohesive collaborators at the actual owner boundary. Proposed new implementation-class budget is 250 lines normally and 400 only with an explicit review exception; file size is a guard, not evidence of good separation by itself.

The scan can be reproduced with the exact file list above and `rg -n --no-heading -- <recipe> <files>`, counting matching lines. Representative exact expressions are `\.IndexOf\("[^"]+"\)`, `\.(StartsWith|EndsWith)\("[^"]+"\)`, `\.(Select|Where|Cast|Take|Aggregate)\(`, `\.Result\b|\.Wait\(|GetAwaiter\(\)\.GetResult\(` and `^\s*((public|internal|private|protected|file)\s+)?(partial\s+)?class `. The loaded skill references supply the remaining literal recipes. Target-typed JSON options and multi-line serialization were verified by source inspection, not only grep.

## Verification plan and measurable budgets

These are proposed implementation acceptance budgets, **not achieved results**. Establish an unchanged baseline first, preserve captured machine configuration, then compare the exact same workload. Correctness and privacy gates are mandatory even if latency targets pass.

| Gate | Proposed measure |
| --- | --- |
| Manual loading | Zero history query/content requests on tab activation or draft filter changes; exactly one current query action after Search. Changing scope/provider cannot display an older result. |
| Query resource bound | At most 201 scalar result rows per page; no canonical body reads; no per-row queries; list response at default page size at most 256 KiB and maximum page size at most 1 MiB. |
| Search latency | On declared 1M-row fixture: warm p95 at most 500 ms, cold p95 at most 2 s for normal bounded provider/date and credential/date queries. Record SQL plan, buffers, returned rows, allocation and cancellation behavior. Review any target change explicitly after baseline evidence. |
| Attempt recording | Proposed metadata begin/finalize p95 overhead at most 25 ms each on the same fixture, separately from upstream provider time; no dropped rows at the 50-attempt/s burst. Record failures and backpressure rather than silently losing logs. |
| Detailed allocation | No input allocation proportional to the full assembled conversation by the logger; retained input/response byte caps enforced before persistence; bytes do not exceed the configured cap on multibyte/streaming inputs. |
| Retry attribution | N actual attempts produce N attempt identities and one logical operation; operation totals are not summed again as attempts. A replayed completion or outbox item does not increase count/cost. |
| Cleanup | At most configured batch size selected/deleted per transaction, active attempts excluded, repeat runs converge, two workers do not conflict or skip permanently. Expired payload is inaccessible before physical purge completes. |
| Reconciliation | Bounded batch/checkpoint restart; crash at every owner/index/checkpoint boundary; identical replays converge; source conflict is visible. Deleted source or expired payload never resurrects; late retained canonical evidence remains independently indexable after orphan-reservation expiry. |
| Profile isolation | Switch profiles during search, dispatch, canonical commit, outbox work, cleanup and content resolution; prove no old-scope result/write appears in the new profile. |

Existing test homes and exact selectors to extend/reuse:

| Existing test file / selector | Established behavior to preserve |
| --- | --- |
| `tests/Unit/CanDoItAll.Tests.Unit/ProviderUsageAggregationTests.cs`; `FullyQualifiedName~ProviderUsageAggregationTests` | `RetriesAddAttemptCostButOnlyOneOperationExecution`, `LegacyKnownTokensRemainUnpricedRatherThanFree`, `PartialSourceFailureIsVisible`. |
| `tests/Unit/CanDoItAll.Tests.Unit/AgentProviderUsageProjectionSourceTests.cs`; `FullyQualifiedName~AgentProviderUsageProjectionSourceTests` | `AmbiguousAgentLegacyEvidenceOnlyAppearsInBoth`; do not invent owner identity for ambiguous history. |
| `tests/Unit/CanDoItAll.Tests.Unit/AgentProviderUsageObservationAssemblerTests.cs`; `FullyQualifiedName~AgentProviderUsageObservationAssemblerTests` | Existing projection compatibility and appending repair usage without replacing runtime facts. |
| `tests/Unit/CanDoItAll.Tests.Unit/ProviderBackedLlmInvocationAdapterTests.cs`; `FullyQualifiedName~ProviderBackedLlmInvocationAdapterTests` | Empty-response retry, aggregated usage across attempts, preserving prior usage on failure/deadline, checked counters. |
| `tests/Unit/CanDoItAll.Tests.Unit/WorkflowUsageAnalyticsTests.cs`; `FullyQualifiedName~WorkflowUsageAnalyticsTests` | `RuntimePersistsOneCorrelatedFactWhenProgressAndBackendReturnTheSameObservation`, immutable replay/conflict behavior and database aggregates. |
| `tests/Integration/CanDoItAll.Tests.Integration/WorkflowUsagePersistenceIntegrationTests.cs`; `FullyQualifiedName~WorkflowUsagePersistenceIntegrationTests` | `PostgreSqlPersistsImmutableUsageFactsAndExecutesDatabaseAggregates`; use isolated PostgreSQL for new index/precision/ordering plans. |
| `tests/Unit/CanDoItAll.Tests.Unit/FileSandboxWorkspaceChatProjectionStoreTests.cs`; `FullyQualifiedName~FileSandboxWorkspaceChatProjectionStoreTests` | Current-index reporting, bounded trends, interrupted upgrade and concurrent index update. Reuse file-read diagnostics to prove no canonical reads on history search. |
| `tests/Unit/CanDoItAll.Tests.Unit/LlmChatDurableStreamEventTests.cs`; `FullyQualifiedName~LlmChatDurableStreamEventTests` | Partial failed output remains incomplete/noncanonical, coalescing, cancellation/disposal, bounded profile retention state. |
| `tests/Unit/CanDoItAll.Tests.Unit/LlmChatWholeUseCaseProfileScopeTests.cs`; `FullyQualifiedName~LlmChatWholeUseCaseProfileScopeTests` | `Profile_switch_after_first_read_rejects_active_operation_projection`. |
| `tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderTwoInstanceUiAcceptanceTests.cs`; `FullyQualifiedName~SharedProviderTwoInstanceUiAcceptanceTests` | Existing two-instance chat/image/vision provider acceptance. Extend on isolated fixtures; do not run destructive setup against the user's live instance. |

New history-specific contracts have no existing exact test home; a focused new unit/integration fixture is justified for query bounds, cursors, payload authorization and replay/retention state transitions. Prefer extending the existing owner tests for adapter integration. Do not add tests that merely restate DI configuration.

Required edge cases include same-time page boundaries, concurrent inserts, changed filter with old cursor, renamed/deleted provider, missing model price, known zero price, retry with partial usage, owner declared but not committed, duplicate workflow/agent attribution, two relay hops, 32-bit aggregate overflow, cancellation before dispatch versus after dispatch, multi-byte truncation, unsupported detail shape, forged credential labels, profile switch, expired payload, owner deletion and legacy backfill replay.

## Performance review result

| Severity | New pattern findings | Top issue |
| --- | ---: | --- |
| Critical | 0 | No confirmed critical pattern in the focused corpus. |
| Moderate | 2 | Retry input copying and compounded JSON comparison/write serialization are hazards for the new logging path. |
| Info | 0 | Counts and large-file guards are recorded without speculative micro-optimization recommendations. |

No performance claim is certified by this preparation. The architecture gates address bounded work, durable history and content ownership; implementation must produce the tests and measurements above before closure.

> ⚠️ **Disclaimer:** These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for your specific context. Always verify recommendations with benchmarks and human review before applying changes to production code.
