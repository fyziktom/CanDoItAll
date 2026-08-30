# Performance review: shared providers and request history

Reviewed providers-shared at 3fc10d2db7ba7e4e15bc94f50e66f815f31c4219 against development at 1625b336e4f60ddb64987240c3a3dc485591d20f. Reviewed projects target .NET 10. No application, database, build, test, benchmark, or profiler was run. All findings below are **static source evidence**, not measured latency regressions or predicted speedup factors.

Workload assumptions: sustained shared inference, long chat histories/tool definitions, detailed standalone capture, and retention spanning multiple policy windows. Traffic requirements and hardware were not supplied. Search already has SQL projection, keyset pagination and request limits. Actionable risks concern retention completeness and repeated work around inference.

## Pass 1: Initial Performance Review

### PERF-01 — P2 / Moderate: input-detail tombstones are never reclaimed

**Impact/workload:** Each detailed standalone request/input revision creates an input detail with EntryId=null. Expiry clears its text, but metadata cleanup deletes only response details. Once every referencing attempt is deleted, the input row and indexes remain forever, increasing storage and database transfers despite retention settings.

**Evidence:** src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryTextProtector.cs:37; HistoryRetentionStore.cs:14, :22, :46; HistoryDetailConfiguration.cs:8, :13; HistoryDatabaseTransferHandler.cs:50. Searching all HistoryDetailRow deletion paths found no input-detail reclamation path. The entry-to-input FK restricts deletion while retries reference the input; it does not remove an unreferenced input.

**Minimal fix:** Add bounded deletion of expired zero-byte input rows with no referencing HistoryEntryRow. Preserve policy locking, transactions and retry input deduplication; handle concurrent attachment and existing tombstones. Do not indiscriminately delete input rows associated with each purged attempt.

**Validation:** Extend tests/Integration/CanDoItAll.Tests.Integration/ProviderHistoryPersistenceIntegrationTests.cs with an input shared by two attempts with different metadata expiries. First expiry must retain input; final expiry must reclaim it. Repeat maintenance, test concurrent retry attachment, verify quota counter equality, and reclaim existing orphans. Inspect the purge query plan against many old tombstones.

### PERF-02 — Capacity risk requiring measurement: fixed maintenance drain ceiling

The host performs one pass every 20 seconds, one outbox batch per pass, and one source batch capped at 100. Thus source cleanup retires at most 100 shared invocation records per 20-second tick per host (5/s before database cost). Default BatchSize=500 gives outbox projection a theoretical ceiling of 25 mutations/s; its single transaction also stops after two seconds. Each shared invocation stages start and finish mutations. These are scheduler ceilings, **not measured throughput or a confirmed workload violation**.

**Evidence:** src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryMaintenanceHostedService.cs:29, :59, :61, :71; HistoryOutboxProcessor.cs:26, :34, :36; src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions/HistoryPolicy.cs:9; src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderHistorySource.cs:24, :52; SharedProviderInvocationAuditService.cs:68, :112.

**Proof work, not unconditional redesign:** Establish expected sustained rate and acceptable projection/cleanup lag; test the actual hosted schedule with a controllable clock, multi-batch backlogs, sustained arrivals, a slow source and cancellation. Existing tests/Integration/CanDoItAll.Tests.Integration/ProviderHistoryRuntimeIntegrationTests.cs:41 drains retention directly in a tight loop and therefore does not prove the production schedule keeps up. Capture backlog age/count, lock waits and cleanup lag. If the SLO is violated, drain additional bounded chunks within the existing time budget and schedule a prompt continuation with fairness. Raising BatchSize alone cannot bypass the source cap.

### PERF-03 — P2 / Moderate: relay copies and parses the same payload repeatedly

**Impact/workload:** Successful buffered responses are read, rewritten, and reparsed for usage. Rewrite and usage extraction each call JsonDocument.Parse(payloadUtf8.ToArray()), adding two body-sized allocations solely for parsing. Near the 64 MiB buffered limit those copies alone allocate approximately 128 MiB per response, excluding read/output buffers, parser metadata, image decoding and the defensive result copy. SSE repeats conversions/parsing per frame; Responses terminal inspection adds a third parse.

**Evidence:** src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs:16, :102, :106, :110, :190, :312, :335, :357, :366; SharedProviderRelayUsageExtractor.cs:19, :41; SharedProviderSseRelayStream.cs:148, :167, :250; src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderRelayRuntimeContracts.cs:496. Incoming requests also execute full normalization twice in src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs:47 and :77; normalization serializes canonical bytes and the result clones them (SharedProviderRelayRequestPolicy.cs:166, :1287; SharedProviderRelayRuntimeContracts.cs:259).

**Minimal fix:** First accept owned ReadOnlyMemory<byte> or a live JsonElement in internal document helpers, then derive rewrite, usage and terminal evidence from one parse. The sibling normalization code already uses JsonDocument.Parse(ReadOnlyMemory<byte>) at SharedProviderRelayRequestPolicy.cs:89. Preserve document lifetime, immutable/public boundary ownership, validation bounds and error behavior. Do not remove defensive result copies blindly. Separately consider minimal route extraction followed by a single target-aware normalization if request-side measurements justify it.

**Validation:** Before/after allocated bytes, GC collections and latency with 1 KiB/1 MiB/near-limit buffered bodies, image payloads and long SSE streams, under concurrency and cancellation. Extend existing tests/Unit/CanDoItAll.Tests.Unit/SharedProviderProtocolContractTests.cs and tests/Integration/CanDoItAll.Tests.Integration/SharedProviderStreamingIntegrationTests.cs as appropriate; preserve model rewriting, usage totals, terminal markers, malformed input rejection and caller isolation. No speedup factor is claimed.

### PERF-04 — P2 / Moderate: cache hits still load and validate the entire catalog

**Impact/workload:** Resolving one routing ID queries every published provider/profile, evaluates all model eligibility, serializes a catalog-wide stamp and only then checks cache. Concurrent inference repeats work proportional to the complete catalog before exact persisted-target validation.

**Evidence:** src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogQueryService.cs:46, :55, :65, :76, :77, :97. Exact dispatch validation already exists in SharedProviderRelayApplicationService.cs:199. Deduplicate this issue with the provider reviewer.

**Minimal fix:** At minimum defer eligibility evaluation until cache miss and query narrow stamp data. Prefer exact publication/profile lookup for inference routing using the strict codec and authoritative eligibility/credential/revocation checks. Keep catalog generation separate. Do not introduce stale TTL fallback behavior.

**Validation:** Compare rows materialized, allocated bytes and warm routing latency for 1/100/1,000 publications with large model lists. Extend tests/Integration/CanDoItAll.Tests.Integration/SharedProviderCatalogApiIntegrationTests.cs and tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRelayPolicyTests.cs to retain immediate effect of publication removal, credential deletion, model updates and concurrent catalog changes.

## Pass 2: Deep Pattern Scan

[Reproducible script](performance-scan.ps1), [exact recipe checklist](performance-scan-counts.csv), [all matched locations without source contents](performance-scan-locations.csv), [160-file scope](performance-scan-scope.json). The six production scopes cover provider-history abstractions/application/persistence, shared-provider abstractions/HTTP, provider-management shared-provider code, MAF history decorators and file journals. UI, migrations, tests, unrelated providers and build outputs are excluded from automated counts; relevant API/test files were also read directly. Counts are matching source lines, not confirmed defects.

### Scan execution checklist (before classification)

| Recipe | Matching lines |
|---|---:|
| critical.indexof_string_no_comparison | 0 |
| critical.substring | 0 |
| critical.startswith_endswith_literal_no_comparison | 0 |
| critical.contains_literal_no_comparison | 0 |
| async.async_void | 0 |
| async.blocking_candidates | 1 |
| async.task_run | 0 |
| async.value_task | 33 |
| async.blocking_collection | 0 |
| memory.case_without_culture | 0 |
| memory.three_replace_chain | 0 |
| memory.params | 2 |
| memory.linq_char | 18 |
| memory.stackalloc | 1 |
| memory.byte_char_arrays | 2 |
| memory.array_pool | 2 |
| memory.string_format | 0 |
| memory.plus_equals_candidates | 17 |
| memory.indexofany | 0 |
| memory.searchvalues | 0 |
| regex.compiled | 0 |
| regex.generated | 0 |
| regex.new_regex | 0 |
| regex.all_regex_declarations | 1 |
| regex.nonbacktracking | 1 |
| regex.match_success_or_next | 1 |
| collections.static_dictionary | 0 |
| collections.static_frozen_dictionary | 0 |
| collections.new_list | 8 |
| collections.new_dictionary | 3 |
| collections.current_culture_comparer | 0 |
| collections.linq_chains | 139 |
| collections.containskey | 2 |
| collections.trygetvalue | 9 |
| io.new_httpclient | 0 |
| io.new_serializer_options | 1 |
| io.serializer_calls | 28 |
| io.json_source_generation | 0 |
| io.file_stream_constructors | 0 |
| io.async_file_options | 1 |
| io.response_headers_read | 2 |
| io.http_send_get | 8 |
| io.stream_legacy_read_write | 0 |
| structural.unsealed_class | 0 |
| structural.sealed_class | 146 |
| structural.equatable | 0 |
| inline.indexof_literal | 1 |
| inline.comparison_candidates | 89 |
| inline.replace | 8 |
| inline.linq_select_where_order_group | 176 |
| inline.all_any | 116 |
| inline.public_internal_class | 0 |
| inverse.stringcomparison | 73 |
| inverse.ordinal_comparer | 22 |
| inverse.params_span | 0 |

Supplemental exact same-file check: SharedProviderRelayRequestPolicy has 35 Set lines = one helper declaration + three cached calls + two empty calls + 29 non-empty runtime calls. Reproduce with rg -n '\bSet\(' against that file.

### Inverse verification and rejected candidates

- **146/146** ordinary non-static/non-abstract class declarations matched as sealed. Records are outside this syntax recipe. No sealing-only work proposed.
- **0/1** apparent blocking calls actually block: HistoryReadConcurrency.cs:7 uses SemaphoreSlim.Wait(0) for immediate admission rejection. No .Result/GetResult finding; inspected ValueTask usages are directly awaited/returned, with no confirmed double-await.
- **1/1** regex is cached, nonbacktracking and timed (HistoryTextCapture.cs:8). The explicit new Regex recipe misses target-typed new; Generated=0, Compiled=0. Preserve this bounded engine. The .Success candidate is provider health, not Regex.Match.
- **1/1** explicit new JsonSerializerOptions candidate is cached via SharedProviderProtocolJson.Options (SharedProviderCatalogContracts.cs:182, :402); the local factory declaration is a grep false positive. **0/28** serializer call lines name source-gen contexts; cached/default contracts and persistence converters alone do not justify source-generation work.
- **2/2** actual HttpClient send calls use ResponseHeadersRead; six other SendAsync/GetAsync hits are higher-level APIs. No direct HttpClient construction. The two byte/char buffers are bounded; the two ArrayPool lines are Rent/Return.
- **0/2** ContainsKey sites repeat a same-key indexer lookup. One checks context removal, the other reconciliation absence. Static Dictionary=0/FrozenDictionary=0 is not a defect: the routing projection freezes its instance dictionary (SharedProviderCatalogProjection.cs:62).
- Comparison candidates include collection membership and char overloads, so 73/89 is not a valid semantic ratio. The one literal IndexOf is **1/1 Ordinal**, and four exact missing-comparison literal recipes report zero.
- **1/1** stackalloc is outside its loop (HistoryTextCapture.cs:48). **0/17** += candidates are string growth in loops: fifteen numeric accumulations and two one-time slash additions. No string.Format sites.
- Eight Replace lines cover bounded routing base64url conversions, path normalization and redaction. Across capture methods, redaction attempts one literal Replace per known secret (0–128) plus one regex Replace, then bounded UTF-8 capture/protection. Calls with no match need not allocate. This security-sensitive algorithm is not replaced without security proof and profiling.
- The two params declarations have **0/2** span overloads. One is the constant-set helper below; the other is a persistence-conflict path and does not warrant hot-path optimization.

### PERF-05 — P2 / Moderate: constant allowlists are rebuilt in message/tool validation (29 runtime sites)

**Impact:** Non-empty Set(...) calls allocate params arrays and construct FrozenSets from constant data. Only **3/32 non-empty constant-set sites are cached**. A valid 256-message chat with string-content user messages performs 256 role-set constructions per normalization, or 512 through both current normalizations, before dispatch.

**Files:** src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs:205, :232, :507, :508, :509, :607, :637, :656, :687, :753, :816, :817, :904, :916, :949, :993, :996, :1004, :1028, :1049, :1061, :1069, :1085, :1086, :1128, :1136, :1156, :1159, :1169. Helper at :1359; good existing static examples at :21, :37, :53.

**Fix:** Reuse named static immutable allowlists with the existing Ordinal comparer, or equally clear allocation-free fixed predicates. Deduplicate identical sets without conflating separate contracts. Do not add a runtime string-keyed cache. The two empty-set calls do not prove allocations and are excluded from 29 instances.

**Validation:** Before/after allocations for 1/256 messages, 128 tools, content arrays and stream options. Preserve existing malformed/unsupported-field and capability tests in tests/Unit/CanDoItAll.Tests.Unit/SharedProviderProtocolContractTests.cs and SharedProviderRelayPolicyTests.cs, including failure parameters. This is the only new deep-scan finding; catalog-wide LINQ and payload copying are already recorded in Pass 1.

## Additional proof boundaries and bundle implications

- Million-row search test source verifies global/provider/credential pages (tests/Integration/CanDoItAll.Tests.Integration/ProviderHistoryQueryIntegrationTests.cs:17, :38). It does not establish sparse-model/subject/request/correlation/external-reference plans or cleanup plans. Include sparse filters, continuation pages, expired/hidden rows and retention under concurrent capture; collect PostgreSQL EXPLAIN (ANALYZE, BUFFERS) before adding speculative indexes.
- Detailed capture takes a partition policy-row lock (HistoryCaptureStore.cs:26, :65; HistoryPolicyStore.cs:75). Shared quota enforcement requires coordination: measure detailed-mode lock waits before considering counter redesign. Light mode skips detail capture/locking; canonical-owned content is not duplicated.
- PERF-01 is required lifecycle repair. PERF-02 is a capacity proof obligation with conditional repair. PERF-03/04/05 are bounded hot-path improvements requiring before/after evidence. Keep security/protocol semantics unchanged and deduplicate PERF-04 with the provider review.
- Document capture completion versus eventual source projection, metadata/text retention, operational cleanup lag and diagnostics. Align schema/index export and SharedInfo/API skills with actual contract or migration changes. This review did not generate exports or change production code.

| Severity | Count | Top issue |
|---|---:|---|
| Critical | 0 | No confirmed critical performance issue in this scope |
| Moderate | 4 | Input-detail retention leak and repeated relay work |
| Info / proof obligation | 1 | Fixed scheduler capacity versus actual workload |

> ⚠️ **Disclaimer:** These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for your specific context. Always verify recommendations with benchmarks and human review before applying changes to production code.

