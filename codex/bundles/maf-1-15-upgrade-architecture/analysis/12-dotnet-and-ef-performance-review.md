# .NET and EF Core Performance Review

## Scope and Decision

This is a targeted static review of the MAF runtime, approval/session continuation, handoff adapter, workflow event normalization, workflow runtime manager, and workflow persistence stores. It is not a repository-wide performance claim and it does not replace traces, allocation profiles, generated SQL, or database command counts.

The 1.15 upgrade should make only the following performance-adjacent changes:

1. Fail closed when an approval-bearing run cannot persist the MAF session state required to bind the response.
2. Preserve stable framework request/call identities; never allocate a replacement identity for an approval-capable call.
3. Add the cheap non-required-finalizer guard before building per-update finalizer snapshots and usage projections.

Handoff projection must be characterized before it is changed. Repeated session parsing, reflection compatibility code, approval-cache lifetime, and all EF query tuning are deferred unless a 1.15 characterization test or runtime measurement makes them release-blocking.

No broad repository refactor, new persistence abstraction, native workflow persistence subsystem, or provider-specific optimization is justified by this audit.

## Pass 1: Initial Performance Review

### Upgrade-Relevant Findings

#### P1. Approval session serialization can fail open

**Impact:** `MafRuntimeSessionPersistenceDriver` wraps asynchronous serialization in `Task.Run`, times out only the waiter, catches every non-cancellation exception, and returns `null`. `MafAgentRuntime` can then return actionable pending approvals with no serialized state even though 1.15 approval-response binding depends on that state.

**Evidence:** `MafRuntimeSessionPersistenceDriver.cs:73-99,113-139`; `MafAgentRuntime.cs:983-1006`; `MafRuntimeSessionBuilder.cs:27-37`.

**Minimal change:** call the asynchronous serializer directly with a linked timeout token, distinguish timeout from serialization failure, and reject an approval-bearing completion when serialization or attachment scrubbing produces no state. Returning `null` remains valid only for paths that have no pending approvals.

**Risk:** changing cancellation or exception semantics can surface provider-specific serialization defects that were previously hidden. That is intentional for approval-bearing runs and must be covered by deterministic tests.

#### P2. Snapshotting can manufacture identity and retain mutable provider content

**Impact:** opaque tool calls without a call ID receive a random GUID, while unknown `AIContent` is retained by reference. The random identity breaks deterministic approval correlation; retained provider objects can be mutated or reused after the update is yielded.

**Evidence:** `MafProviderStreamingRunner.cs:275-316,319-337,354-369`; `MafApprovalContinuationDriver.cs:46-77`.

**Minimal change:** require the framework request ID or tool-call ID for approval-bearing content and fail predictably when both are absent. Do not synthesize an identity. Keep unknown non-approval content opaque unless 1.15 exposes a supported typed copy path.

**Risk:** a provider emitting an invalid approval request will now fail instead of presenting a non-resumable approval. That is the correct failure mode.

#### P3. Per-update finalizer work can grow quadratically

**Impact:** every streamed update is deep-snapshotted and invokes required-finalizer response construction. The construction snapshots accumulated invocations/traces and builds usage from all updates before the assembler reaches its `finalizerMode` early return. Usage grouping rescans the accumulated update array for each response ID, so a long stream can approach \(O(n^2)\). The JSON-repair path snapshots each repair update twice.

**Evidence:** `MafAgentRuntime.cs:547-591,785-794,1457-1473`; `MafRuntimeResponseAssembler.cs:24-27,83-105,147-173`.

**Minimal change:** return before taking finalizer snapshots or building usage when the finalizer mode is not `Required`. Avoid the second JSON-repair snapshot only if a characterization test proves the same immutable update can safely feed both collectors. Do not redesign the stream pipeline in this upgrade.

**Risk:** moving any guard below required-finalizer observation could delay or lose a valid early completion. Test normal, required-finalizer, repair, tool-call, and usage-bearing streams.

#### P4. The handoff wrapper buffers the full stream and owns a second projection

**Impact:** `HandoffDepthGuardAgent.RunCoreAsync` buffers every update and calls `ToAgentResponse()`. This adds stream-sized memory and may bypass MAF 1.15's non-streaming terminal workflow projection.

**Evidence:** `MafHandoffWorkflowFactory.cs:108-120`. The id-less handoff deduplication fallback at `MafHandoffWorkflowFactory.cs:152-160` is sequence-derived and must also be exercised.

**Minimal change:** first compare direct MAF non-streaming output, wrapper output, and the full production streaming runtime. Change the wrapper only if the fixture proves a semantic mismatch; do not inspect private MAF state or execute the workflow twice.

**Risk:** an optimization that delegates blindly to the inner non-streaming method can remove depth-guard observation. Correctness outranks allocation reduction here.

#### P5. Serialized session JSON is parsed repeatedly

**Impact:** a restore-capable ordinary run can parse the same serialized session up to four times: session-message selection, restore eligibility, deserialization, and prompt-input selection. Large provider history state increases CPU and temporary allocation.

**Evidence:** `MafAgentRuntime.cs:292-309`; `MafRuntimeSessionBuilder.cs:27-30,58-69,211-223,253-309`.

**Minimal change:** defer until allocation profiling shows material cost. If justified, compute restore eligibility once inside the existing session-builder responsibility; do not introduce a second compatibility model.

#### P6. Two hot compatibility paths use reflection

**Impact:** six property-discovery calls occur in opaque tool-call snapshotting and workflow event normalization. Event normalization can execute for every workflow event, and reflection is also fragile across an SDK upgrade.

**Evidence:** `MafProviderStreamingRunner.cs:413-426`; `MafWorkflowEventNormalizer.cs:70-81,139-245`.

**Minimal change:** validate the 1.15 public event/content surface. Replace reflection only where a supported typed member now exists. Add no new reflection and no private-state dependency.

#### P7. Pending approval cache has no explicit bound

**Impact:** `pendingApprovalCache` is process-wide and entries for abandoned sessions remain until a later run explicitly clears them. A long-lived host can retain approval objects indefinitely.

**Evidence:** `MafApprovalContinuationDriver.cs:25,35-44,110-130`.

**Minimal change:** defer unless a soak test confirms growth. Prefer lifecycle cleanup at the existing session/run boundary over a timer, second cache, or silent eviction of resumable approvals.

### EF Core Findings Deferred from the Upgrade

#### E1. Workflow result persistence performs fan-out I/O

`WorkflowRuntimeManager` loads the complete existing event collection, performs in-memory duplicate checks, and then sequentially persists events, external requests, checkpoints, and artifacts. The persistent store commonly performs a `SELECT` followed by `SaveChangesAsync` for each item.

**Evidence:** `WorkflowRuntimeManager.cs:488-514,727-745`; `PersistentWorkflowStores.cs:1826-1875,1904-1936,2082-2133`.

This is not an ORM navigation N+1. It is explicit application-level I/O fan-out and full-payload materialization. Measure database command count and payload bytes for realistic workflow result sizes before designing a batch API.

#### E2. Usage analytics scans the same filtered set four times

`ReadSnapshotAsync` issues separate aggregate queries for totals, runs, provider/models, and nodes. `ListAsync` is unbounded, and `ListPageAsync` rejects non-positive sizes but has no maximum page size.

**Evidence:** `PersistentWorkflowUsageObservationStore.cs:72-118,125-233`.

Keep this out of the MAF package upgrade. Establish query duration, row counts, and generated SQL first; then decide whether a materialized projection, bounded paging, or fewer aggregate passes is warranted.

#### E3. Catalog search loads and deserializes more than the page needs

Catalog list/search selects full `DefinitionJson` and deserializes it per row to recover five metadata values. Three parameterless `ToUpper()` calls participate in search/order and can prevent normal index use.

**Evidence:** `PersistentWorkflowStores.cs:41-60,76-101,1425-1443`.

Do not denormalize metadata or change database collation during this upgrade. Capture the generated SQL and query plan first; use provider-supported case-insensitive comparison or persisted normalized columns only in a separately reviewed schema change.

#### E4. Idempotency retry can spin on the wrong unique constraint

`TryClaimAsync` retries forever when any unique constraint fails and the subsequent scope query finds no record. A collision on the unique reserved-run index, rather than the requested scope index, satisfies that condition.

**Evidence:** `PersistentWorkflowLaunchIdempotencyStore.cs:28-50,174-200,299-309,444-464`.

This is a pre-existing correctness and database-load defect, not a 1.15 regression. Track it separately: classify the violated constraint by its strongly typed database error metadata, retry only a scope race, and fail explicitly for other unique violations.

### EF Core Positive Evidence

- No lazy-loading or navigation-loop N+1 pattern was found in the targeted stores.
- Read paths contain 35 `AsNoTracking()` calls.
- Contexts are obtained from `IDbContextFactory<AppDbContext>` and disposed per operation.
- Existing paged workflow queries apply ordering, `Skip`, and `Take` in SQL.

## Pass 2: Deep Pattern Scan

The counts below are exact raw hits for the targeted audit slice. “Actionable” means a manual review found relevance to the MAF/workflow hot path; it does not mean every hit should be changed.

### Scan Execution Checklist

| Recipe | Hits | Manual result |
|---|---:|---|
| `async void` | 0 | Pass |
| `.Result` / `.Wait` | 6 | 0 actionable; all six are `ResultShape`-style identifier false positives |
| `Task.Run` | 1 | Actionable session-serialization wrapper |
| `ValueTask.AsTask` | 1 | Review only; do not change without measurement |
| `IndexOf` with literal | 0 | Pass |
| `Substring` | 0 | Pass |
| Literal `StartsWith` / `EndsWith` | 0 | Pass |
| Literal `Contains` | 0 | Pass |
| Parameterless `ToLower` / `ToUpper` | 3 | All three are EF catalog expressions |
| Three-or-more chained `Replace` calls | 0 | Pass |
| `params` signatures | 1 | `FirstNonEmpty`; two array allocations on the workflow-event path |
| Character `All` / `Any` | 0 | Pass |
| `string.Format` | 0 | Pass |
| String `+=` | 0 | Pass |
| `new List<T>` | 5 | Only accumulated stream collections are hot-path relevant |
| `new Dictionary<TKey,TValue>` | 8 | No immutable static candidate identified |
| Static `Dictionary` / `FrozenDictionary` | 0 / 0 | No candidate |
| `CurrentCulture` | 0 | Pass |
| `Select` / `Where` / `Cast` / `Take` / `Aggregate` | 146 | 13 actionable hot-path hits |
| `OrderBy` / `GroupBy` | 45 | 2 actionable hot-path hits |
| General `Any` / `All` | 6 | 3 actionable hot-path hits |
| `ContainsKey` | 2 | Not prioritized |
| Literal `new HttpClient` | 0 | Pass |
| Uncached `JsonSerializerOptions` | 0 | 10 of 10 options instances are cached |
| JSON serialize / deserialize | 21 | Session and workflow persistence require characterization |
| Legacy byte-stream APIs | 0 | Pass |
| File I/O | 1 | Already asynchronous |
| Reflection property discovery | 6 | Two compatibility paths; see P6 |
| Class declarations | 42 | 35 sealed + 7 static; 42 of 42 pass |

### Deduplicated Scan Findings

- **Critical:** one actionable `Task.Run`/timeout path is part of P1; its problem is cancellation and fail-open state, not merely scheduling overhead.
- **Moderate:** 13 selection/filter hits, 2 ordering/grouping hits, and 3 `Any`/`All` hits contribute to P3, E1, and E2. These are algorithmic/query-shape findings; replacing LINQ mechanically would not fix them.
- **Moderate:** three case-normalization hits are E3. Changing them without inspecting generated SQL, collation, and indexes is unsafe.
- **Information:** five list allocations, eight dictionary allocations, one `AsTask`, and 21 JSON operations are not independently actionable without allocation or latency evidence.
- **Positive:** there is no synchronous wait, literal `HttpClient`, uncached serializer-options instance, legacy stream API, unsealed concrete class, or obvious string-allocation loop in the audited slice.

## Minimal Validation Contract

### Required for the 1.15 Change

1. Serialize, attachment-scrub, restore, and continue a mixed approval session; prove the original bound arguments execute exactly once.
2. Simulate serialization timeout, exception, and caller cancellation. Prove the underlying operation observes cancellation and no actionable approval is returned without serialized state.
3. Exercise missing request ID, missing call ID, duplicate response, and unknown response; prove deterministic fail-closed behavior.
4. Compare ordinary streaming, required-finalizer streaming, and JSON-repair streaming. Record update count, snapshot count, elapsed allocation, and final response equivalence.
5. Compare direct MAF, handoff wrapper, and full runtime terminal output without duplicate execution.

### Deferred Performance Validation

1. Use EF command interception on a workflow result containing multiple events, requests, checkpoints, and artifacts; report command count and transferred payload bytes.
2. Capture generated SQL and query plans for catalog text search and usage analytics at realistic cardinalities.
3. Run a long-lived abandoned-approval soak and measure cache entry count and retained bytes.
4. Use an allocation profiler or a focused benchmark before changing session parsing, reflection compatibility, LINQ, collection types, or JSON handling.

> ⚠️ AI-assisted static audit: validate priorities with command-count instrumentation, allocation profiling, and 1.15 fixtures before changing production behavior.
