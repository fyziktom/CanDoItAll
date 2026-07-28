# Current State

## User-visible startup

- `AgentChatPanel` immediately renders a local generic string, then dispatches through `IAgentChatExecutionOrchestrator`.
- Early `ExecutionUpdated` entries are projected only when the selected workspace run matches the entry. A newly starting operation has no run identity yet, so the condition cannot represent its pre-run state.
- Process Manager chat calls the workspace service directly, sets only `managerChatIsBusy`, does not use the orchestrator, and does not subscribe to live execution updates.
- Manager chat initialization serializes reference-data load, agent resolution, run-specific session list/create/rename, workspace load, and runtime-snapshot load.

## Actual pre-first-event path

Before the first backend typed feedback, `SendMessageAsync`:

1. loads the file catalog;
2. resolves the agent;
3. resolves the provider through EF and optional file fallback;
4. reads the selected session;
5. enters split-store run creation, where it loads catalog and session again;
6. checks active-run indexes;
7. persists the initial run and user transcript;
8. resolves capabilities, memory, handoff, attachments, and credentials;
9. reloads and saves run detail for the first `Planning` log;
10. only then invokes `ExecutionUpdated`.

Every later runtime progress callback also saves the run detail before notifying UI. The file store uses an in-process semaphore plus a cross-process lock; contended lock acquisition polls at 25 ms. Warm split reads can bypass the lock, while cold initialization and all writes take it.

## Current preload and context

- `AgentChatPreparationPool` is scoped and caches only an `AgentDefinition` for about 20 seconds. It does not prepare MAF agents, provider clients, credentials, tools, skills, memory, context contributors, sessions, or approvals.
- The pool’s version fence correctly drops stale in-flight reference loads, but returned definitions expose mutable `List<T>` instances through `IReadOnlyList<T>` and therefore are not actually immutable.
- `AgentChatContextRegistry` already provides the right scoped capture boundary: a lock-protected monotonic version and copied fragment array with capture time.
- Project Structure, Projects, Workflows, Process Workspace, and Live Processes already publish current UI projection facts into that registry. Process builders consume loaded shell/live-run projections rather than querying the entire domain again.
- Module context reuse therefore exists, but preparation and event correlation do not carry a standardized snapshot revision/freshness contract.

## Current event surfaces

- `ExecutionUpdated` is a synchronous multicast event tied to a persisted run. A throwing handler can make a caller observe failure after persistence succeeded, stop later handlers, and prevent the execution sink publication.
- `CurrentProfileAgentFrameworkWorkspaceService` relays the same event without subscriber isolation and retains subscriptions to prior profile workspaces for the scope lifetime.
- `IAgentChatExecutionNotificationHub` publishes completion only. Concurrent publishes can overlap the same handler, handlers have no timeout, and a hung handler can delay successful completion indefinitely.
- Completion routing uses source identity but omits context version/digest. It is safe only as an invalidation hint followed by canonical reload and a current-generation fence.
- `BufferedAgentExecutionEventSink` is a diagnostic queue, has no subscriber API or organization/profile partition, silently drops old entries, and is bypassed by the current-profile workspace factory.
- There is no suitable generic typed operational activity stream today.

## Concurrency and persistence

- Agent catalog/chat/run persistence is file-backed, not EF-backed.
- Provider-profile resolution and floating-chat settings use `IDbContextFactory<AppDbContext>`.
- Shared reference-data loads are single-flight, but the first caller’s cancellation token owns the shared task while later waiter cancellation is ignored.
- Provider lookup and session read can overlap only after provider resolution consumes the already-loaded catalog; its current fallback may touch the same file store. Duplicate catalog/session reads must be removed/coalesced first.
- Runtime capability stages and tool-provider composition mutate shared ordered state and must remain sequential.
- Writes, coherent multi-file snapshots, progress callbacks, and shared `DbContext` operations must not be parallelized.
- The scoped workspace factory uses an unsynchronized `Dictionary`; concurrent resolution can create duplicate workspaces.

## Measurement state

- `IMafRuntimeCompositionMetrics` already exposes named capability-stage timings, but production registers a no-op implementation and measurements lack run/agent correlation.
- Provider streaming has an OpenTelemetry activity but no separate gate-enter, dispatch, or first-semantic-update measurement.
- Existing integration tests provide a real file store/provider-registry plus fake runtime seam suitable for no-cost cold/warm measurements.
- No reproducible baseline currently records time to first operational activity because no such activity exists.

## CodeAnalytics evidence

- Full solution: `snap-20260727121109-53bec4ab` (100 projects, 3,099 documents).
- Scoped startup: `snap-20260727121030-301e5d8a` (19 projects, 1,524 documents).
- Event/snapshot review: `snap-20260727121328-617a1f6c`.
- Known high-severity package findings involving `System.Security.Cryptography.Xml` are pre-existing and outside this bundle unless touched by a required dependency change.
