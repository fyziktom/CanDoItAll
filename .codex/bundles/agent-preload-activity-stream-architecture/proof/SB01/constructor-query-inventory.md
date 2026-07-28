# SB01 constructor, lifetime, and query inventory

## Construction and lifetime map

| Surface | Current lifetime / owner | Construction paths and risks |
| --- | --- | --- |
| `AgentFrameworkWorkspaceExecutionService` | Private inner object owned by each `AgentFrameworkWorkspaceService` | Its primary constructor is at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.cs:5`. There is exactly one construction at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs:62`; the inner `ExecutionUpdated` event is synchronously forwarded at line 74. A constructor change must update this sole production call. |
| `AgentFrameworkWorkspaceService` | Scoped in the generic host; manually retained per workspace by the real module factory | Constructor: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs:14`. The generic host registers it scoped at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs:79`. The real app registers a scoped profile relay and its scoped factory manually constructs an inner file store/runtime/service graph at `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs:51`. Direct test constructions exist in `HrAgentProcessReviewServiceTests.cs` and `ProcessRunNarrativeGeneratorTests.cs`; integration tests also activate it through DI. |
| `CanDoItAllAgentWorkspaceFactory` | Scoped, retaining workspace services for the scope | Constructor/cache: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs:24`. Registered with a scoped interface/concrete alias at `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs:147`. The current dictionary is unsynchronized and string-keyed, and the factory manually creates another scoped object graph instead of composing registered scoped services. Stable future scope identity must be typed. |
| `CurrentProfileAgentFrameworkWorkspaceService` | Scoped relay | Constructor: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs:8`; registration line 150. It keeps every resolved service in an unlocked `HashSet`, subscribes at lines 468-477, and never unsubscribes. A profile switch can retain obsolete subscriptions. Its unit test uses reflection and assumes one constructor, so constructor changes must update that seam. |
| `AgentChatExecutionOrchestrator` | Scoped module application service | Primary constructor: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs:6`; scoped registration line 132. Three direct test constructions exist. It is the correct boundary for immediate operation admission because it already owns context capture plus completion publication. |
| `AgentChatPreparationPool` | Scoped mutable preparation stock | Constructor: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatPreparationPool.cs:21`; scoped concrete/interface alias lines 136-138. Eight unit-test constructions exist. It currently stores only `AgentDefinition` and serializes all misses behind one `SemaphoreSlim`; it is not an execution/runtime pool. |
| `AgentChatContextRegistry` | Scoped, lock-owned aggregation source | Constructor and lock-backed registry: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextRegistry.cs:51`. Registered scoped at module lines 126-128. Forty tests construct it directly. It remains the sole owner for atomic prompt-fragment plus typed-attachment publication. |
| `AgentChatExecutionNotificationHub` | Scoped bounded fan-out | Constructor: `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionNotificationHub.cs:7`; scoped alias lines 129-131. It caps subscriptions at 64, snapshots under a lock, fans out in parallel, and isolates handler exceptions. Four tests construct it directly. |

## Real-app versus generic-host lifetime split

The real module registers:

- scoped file store and MAF runtime;
- scoped DB-backed provider registry and reference-data cache/provider;
- singleton cancellation registry, invalidation hub, and `TimeProvider`;
- scoped context registry, notification hub, orchestrator, preparation pool, workspace factory, and current-profile relay.

The app composition calls `AddAgentFrameworkModule` at `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs:62`. It has no real-module `IAgentExecutionEventSink` registration, so the inner workspace currently receives the explicit `NullAgentExecutionEventSink` from the manual factory.

The generic host differs: it owns a singleton store, buffered execution-event sink, runtime, bridges, and a scoped workspace service. Baseline and future tests must state which graph they exercise.

## Startup query order before runtime

For split-store `SendMessageAsync`:

1. Orchestrator captures current context in memory, then calls the workspace.
2. The execution service loads catalog, resolves the DB-backed provider, and reads the selected session at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs:173`.
3. `BeginChatBackedRunWithSplitStoreAsync` reloads catalog and session, checks blocking summaries, and persists the preparing run/session at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:350`.
4. Handoff and attachment preparation still occur before the first `Planning` log.
5. `Planning` is appended at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:861`; runtime begins at line 887.
6. Every runtime progress callback returns to durable `AppendExecutionLogAsync`.
7. `AppendExecutionLogAsync` saves canonical run detail, synchronously invokes `ExecutionUpdated`, then awaits the execution-event sink at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:55`.

The DB-backed provider registry performs `SingleOrDefaultAsync` at `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:41`. File catalog loads can also enter in-process and cross-process locks at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs:53`.

## Existing preload/open path

Floating startup awaits settings and then warms the preparation pool. The pool queries active agent definitions only, has its own 20-second validation window, and does not feed `SendMessageAsync`. Opening a chat separately resolves/creates the session, then the panel loads workspace and run detail. This explains why a visually “prepared” floating agent still performs the complete execution startup sequence.

## Module-held runtime data that is currently discarded

### Project Structure

- The page already owns `ProjectStructureSurface` and selected nodes at `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor:758`.
- Its context provider publishes only prompt fragments and selected entity references.
- `project_structure_read` unconditionally calls the canonical service at `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs:893`.
- That path recreates a `DbContext` and reloads nodes, links, layout, and contributors. Contributors share a single context/projection and must not be mechanically parallelized.

### Process Manager

- `ProcessWorkspaceShell` already owns the shell projection and selected run at `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor:263`.
- The projection includes runs, events, incidents, messages, active agents, stats, metrics, tool usage, selected detail/record, and freshness metadata.
- Manager load still performs sequential agent/session/workspace/runtime queries.
- Manager send calls `IAgentFrameworkWorkspaceService.SendMessageAsync` directly at line 2520; approval does the same at line 2573. The component contains no orchestrator dependency, so registry capture and completion notification are bypassed.
- It builds a shallow prompt from held data at line 2984, but that data is not transported as a typed tool attachment.

## Producer and consumer inventory

| Artifact | Producers | Consumers / lifecycle |
| --- | --- | --- |
| `ExecutionUpdated` | `AppendExecutionLogAsync`; inner-to-workspace forward; current-profile relay | Floating coordinator, `AgentChatPanel`, contextual workspace windows. UI dispatches with `InvokeAsync`, but producer invocation remains synchronous and exception-coupled. |
| `ExecutionEvent` sink | Execution service after `ExecutionUpdated` | Null sink in the real app; buffered sink in the generic host. Contract remains stringly typed and is not the new operational stream. |
| Completion notification | Created by execution service and published only by orchestrator | Project Structure, Projects, CRM, and Workflow context providers. Process Manager bypass means no completion notification there. |
| Context snapshot | Generic and module-specific context providers publish into `AgentChatContextRegistry` | Orchestrator captures it per send; floating host reads diagnostics. Registry is the approved future typed-attachment aggregation owner. |

## Concurrency conclusions

- Remove/coalesce duplicate I/O before overlapping tasks.
- Never run parallel operations on one `DbContext`.
- Do not parallelize project contributors sharing a mutable assembly context.
- Do not use the current unsynchronized, string-keyed workspace-factory dictionary as an operational event partition store.
- Operational feedback must publish outside durable run-log persistence so a slow/faulty UI cannot change canonical execution.
