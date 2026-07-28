# Reusable floating agent chats

Status: Implemented with the explicit follow-ups below; original decision accepted
2026-07-16 and current preload/activity addendum documented 2026-07-27.

The current typed activity, preparation, provider-snapshot, and module-runtime-snapshot
contract is maintained in
[Agent execution activity and runtime snapshots](agent-execution-activity-and-runtime-snapshots.md).

## Context

The Project Structure canvas has a useful contextual agent catalog and chat, but the implementation is mounted inside `CanvasWorkbench.OverlayContent`. It is consequently bounded by the Canvas tab and is destroyed when the user switches to Gantt or navigates to another module. The same component also combines catalog filtering, access resolution, window state, durable chat sessions, run execution, approvals, attachments, history export, voice, runtime diagnostics, and module-specific prompt construction in one 1,550-line Razor file.

The required behavior has three independent axes:

- one floating host must survive component, tab, and routed-module changes;
- the same durable conversation must receive the context of the module that is active for each new turn;
- hidden conversations may remain active for a bounded time without retaining unsafe runtime resources.

The original runtime already had a per-turn `IAgentContextContributor` seam but did not have a UI-context registry, active-chat lifecycle, typed startup-activity stream, or safe preparation snapshots. Those application seams now exist. A durable `ChatSessionRecord` is still only the transcript/session identity, and each send still constructs and disposes a fresh MAF `RuntimeBuildResult` containing the agent, context providers, tool providers, approval wrappers, MCP/A2A clients, and other disposables.

The architecture mapping used CodeAnalytics snapshots `snap-20260716185835-0c0d9e98`, `snap-20260716185934-932fd70e`, `snap-20260716190015-48a36a28`, and `snap-20260716190043-94e27f4d`. The affected project graph has no direct project-reference cycle. `CanDoItAll.Modules.AgentFramework` already references Workbench and CRM-HR, so this integration must not add a feature-module reference back to that implementation module. The Processes module has a broader pre-existing Agent Framework module dependency; its context adapter uses only the shared Core/Components contracts and does not deepen that dependency.

## Decision

Implement one circuit-scoped floating-chat coordinator and one global host.

Feature modules integrate through small contracts in `CanDoItAll.AgentFramework.Core`. The implementation and settings persistence remain owned by `CanDoItAll.Modules.AgentFramework`. The global Razor host is composed once by `CanDoItAll.Web` above routed content. Shared transcript/composer presentation continues to use `ChatWorkspacePanel` from `CanDoItAll.AgentFramework.Components`.

The global host uses OverlayLib's `OverlayWindow`, not CanvasLib's `CanvasFloatingWindow`. Canvas windows intentionally clamp to a `.cw-stage-surface` and participate in a canvas-local stacking context. `OverlayWindow` is the package-supported primitive for page and application overlays and owns drag, resize, minimize, focus, and stacking behavior.

The coordinator is scoped to the Blazor circuit and active database-profile service graph. It must not be singleton. A singleton could leak active chats, context, drafts, or profile-specific agent identities across users and organizations.

## Lifetimes

Four identities remain explicit:

| Lifetime | Identity | Owner | Retained data |
| --- | --- | --- | --- |
| Durable conversation | `ChatSessionId` | Agent workspace persistence | Transcript, title, run references, approval state |
| Active floating chat | `AgentChatHandleId` | Circuit-scoped coordinator | Agent/session IDs, visibility, activity time, lightweight UI state |
| Execution | `ExecutionRunId` | Agent runtime/persistence | One run, logs, receipts, metrics, cancellation state |
| Activity operation | `AgentExecutionActivityStreamId` | Process-local typed activity coordinator | Bounded sequenced feedback for one operation |
| Prepared catalog entry | `AgentId` | Circuit-scoped `AgentChatPreparationPool` | Bounded immutable agent-definition metadata only |
| Execution preparation | Database profile + workspace + agent + version | Scoped `AgentExecutionPreparationCache` | Immutable agent/provider/capability/memory blueprint |
| Provider projection | Database profile generation + provider revision | Singleton canonical provider snapshot | Immutable provider configuration; never resolved credentials |

Keeping a chat active retains only the lightweight handle and durable session identity. It does not retain a `RuntimeBuildResult`, `AIAgent`, scoped service, credential, tool delegate, MCP client, attachment, voice buffer, or module component.

Stopping a floating chat removes its active handle and clears transient UI state. It does not delete durable conversation history. Deletion or archival, if added later, must remain a separate explicit action.

## Responsibility and dependency map

| Responsibility | Owner |
| --- | --- |
| Strongly typed chat/context/settings records | `CanDoItAll.AgentFramework.Models` or pure Core contracts |
| Launcher, context-registry, and lifecycle contracts | `CanDoItAll.AgentFramework.Core` |
| Scoped coordinator and persisted settings adapter | `CanDoItAll.Modules.AgentFramework` |
| Transcript/composer presentation | `CanDoItAll.AgentFramework.Components` |
| Global catalog/chat `OverlayWindow` host | `CanDoItAll.Modules.AgentFramework`, composed by Web |
| Project Structure context construction and access projection | `CanDoItAll.Modules.Workbench` |
| CRM-HR, Processes, and future context construction | their owning modules |
| Runtime authorization | existing server-side tool and capability policies |
| Catalog preparation metadata | `CanDoItAll.Modules.AgentFramework`, never live runtime objects |
| Versioned execution preparation | `CanDoItAll.AgentFramework.Core`, scoped and validated at use time |
| Canonical provider runtime projection | `CanDoItAll.Modules.AgentFramework`, singleton but database-profile-generation fenced |
| Full runtime construction | MAF runtime, fresh for every execution |

```text
Workbench / CRM-HR / Processes
        |
        +--> IAgentChatLauncher
        +--> IAgentChatContextRegistry
                    |
                    v
       scoped FloatingAgentChatCoordinator
                    |
          +---------+----------+
          |                    |
          v                    v
 global OverlayWindow     immutable context snapshot
          |                    |
          v                    v
 AgentChatPanel ------> immediate operation handle + typed activity reader
                                  |
                                  v
                        per-turn execution orchestrator
                                  |
                    +-------------+-------------+
                    |                           |
                    v                           v
          immutable module snapshot    versioned preparation
                    |                           |
                    +-------------+-------------+
                                  |
                                  v
                       fresh RuntimeBuildResult
```

No new feature-module dependency on `CanDoItAll.Modules.AgentFramework` is introduced by floating-chat context integration. Web is the composition root and may reference both feature modules and the global host. The pre-existing Processes-to-AgentFramework dependency remains recorded technical debt outside this refactor.

## Module context contract

Context is pushed into a registry as immutable values; the registry does not retain a Razor component, domain entity, or callback delegate.

A page activates an `AgentChatContextScope` and receives a disposable registration. The scope identifies the module/surface, provides a user-facing label, and contains an optional workspace-scope descriptor. The routed page is the single registration owner. A nested component never receives a registry scope ID and never registers itself directly; it reports a bounded, immutable, strongly typed selection or load-state value through an awaited component callback. The page validates that value against its current canonical entity and generation, then composes the surface and any stable, deterministically ordered fragments. This keeps component lifetimes out of Core and makes stale child results fail closed.

Each scope snapshot contains:

- stable module, surface, and context IDs;
- a display label for the active location;
- ordered context fragments whose text is the unavoidable external LLM protocol;
- a bounded set of agent-access projections for catalog filtering and UI explanation;
- a monotonically increasing version and capture timestamp;
- optional sanitized trace metadata, never secrets or complete domain objects.

Access resolution is explicit and fail-closed. Both scopes and reusable surface descriptors default to `AllowListed`. A scope reports `Ready`, `Loading`, or `Failed`; only `Ready` can contribute context. `AllowListed` scopes require an agent projection, while every `Unrestricted` module builder must opt in deliberately and is reserved for bounded, reviewed, sanitized surfaces that have no canonical per-agent projection. Neither mode grants runtime tool access.

Semantic deep-link parameters are requirements, not selection hints. When a route names a project, workflow, run, process definition, account, opportunity, interaction, application, party, resource, or other canonical entity, the owning application boundary must resolve that exact identity and validate all expressed ownership relationships before publishing `Ready`. An explicit missing, conflicting, unsupported, or filtered-out identity publishes `Failed`; it must never fall back to the first catalog row, a default editor, or a generic new-entity context. Routed pages keep a bounded failed/loading scope mounted even when their main surface is unavailable so prompt capture cannot silently degrade to context-free execution. A later explicit user selection may replace the routed selection through the page's normal typed state transition.

The active scope is captured once at send initiation. Agent ID, session ID, scope version, ordered fragments, typed invocation attachments, attachment paths, and refresh target form one immutable send command before asynchronous runtime work proceeds. Navigation after capture cannot change the in-flight turn. The next turn captures the new active module, allowing one conversation to move safely from Project Structure to CRM-HR or Processes. Project Structure and Processes can attach bounded runtime snapshots copied from their already-ready UI/projection state; those snapshots carry profile generation, fingerprints, freshness, and coverage and never grant mutation authority.

Scope and fragment registrations are disposable. Disposal removes their immutable values immediately. A page transition therefore cannot leave an old project or partner selected as an implicit fallback. With no active context, the chat remains usable but sends no module context.

The durable user prompt is stored unchanged apart from trimming. Module context is carried separately as bounded `AgentRuntimeTransientContext`, encoded as JSON inside an explicit untrusted-data boundary, and injected for one run through a User-role `AIContextProvider`. Framework-managed history excludes that provider message, so the context is not copied into durable/provider-native history. The transient value is retained in a bounded in-memory registry only while an approval is pending; approval continuation after a process restart fails explicitly rather than resuming without the exact captured context.

Module context is not an authorization grant. Catalog access projections improve discoverability, but every tool invocation must continue to reload and enforce canonical agent, capability, project, process, and business authorization at the server boundary. The optional workspace descriptor is a typed trusted projection used by existing server-side authorization, never reconstructed from context text.

Successful mutation-capable runs publish a typed completion notification containing the original scope/source, agent, session, and run identities. A sanitized scope that cannot project per-agent mutation access may opt into the separate `AgentChatContextCompletionRefreshMode.OnSuccessfulRun` policy; this policy only requests a canonical UI reload and grants no read, mutate, workspace, or tool authority. Module providers subscribe through disposable lifetimes and reload their own canonical state; the runtime never stores a page callback or component reference. Matching subscribers run concurrently and isolate failures, but the orchestrator awaits the publication boundary so canonical module refresh completes before the current chat panel hydrates the completed transcript. Refresh eligibility is notification routing, not authorization, and must never be represented as read/mutate context permissions.

## Current user-position propagation extension

Status: Accepted for implementation, 2026-07-17

The first implementation proves that a conversation can move between Project Structure, Projects, and CRM. It does not yet describe the user's position consistently across the shell and feature modules. The architectural scan in CodeAnalytics snapshot `snap-20260717142824-88cfa7be` found the context registry in Core with a high fan-in and confirmed that the requested feature modules can depend on Core without a reverse dependency from Core to a module. Source inspection found these distinct responsibilities:

| Responsibility | Current owner | Decision |
| --- | --- | --- |
| Open tabs and active tab | `WorkbenchStateService` | Publish one sanitized workspace-position value through a disposable ambient lease. |
| Active routed page and module subview | Owning routed component | Publish a typed surface position; do not infer semantic subviews from the URL. |
| Selected project, party, workflow, process, schedule, resource, or agent | Owning page/component | Publish explicit allowlisted entity references and facts; never reflect component properties or serialize domain entities. |
| Agent access for the active surface | Existing module context builder | Remains in `AgentChatContextScope`; the position model is not authorization. |
| Per-prompt consistency | `AgentChatExecutionOrchestrator` and context registry | Capture one immutable workspace + surface snapshot before runtime execution. |
| Refresh feedback | Floating `AgentChatPanel` | Show a distinct context-refresh stage before runtime `Preparing`/`Running`. |
| External-model payload | Core context composer | Serialize the typed positions and existing sanitized fragments only when the prompt is sent. |

The extension adds two bounded, immutable values:

- `AgentChatWorkspacePosition` describes the active workbench tab: tab identity, title, route, tab kind, and optional safe project labels.
- `AgentChatSurfacePosition` describes the active module, surface, subview, primary selection, additional selections, and a bounded set of explicitly safe facts.

String values in these records are the unavoidable open extension protocol for future modules, but constructors enforce length/count limits and module builders own constants and allowlists. IDs and labels are copied values. The registry never receives a domain entity, `ComponentBase`, service provider, or reflection-based property bag.

The registry gains one ambient workspace-position lease in addition to the existing active-scope and fragment leases. Ambient position and active scope are copied under the same registry lock and share the same monotonically increasing version. Disposing or replacing either registration removes its value immediately. Navigation events can briefly leave an old surface beside a new workspace route (or the inverse), so strict prompt capture compares their normalized routes and fails explicitly instead of submitting that mismatched pair. A successful prompt therefore receives one matching immutable workspace/surface snapshot.

`CaptureAsync` is an awaitable application boundary but performs no database or network work in this phase. UI events push only small typed position values; JSON/model text is built lazily at prompt time. This is deliberately preferred to retaining async component callbacks. If a later module proves that canonical enrichment requires I/O, that work must be a bounded application service keyed by the captured scope/version with cancellation and stale-generation rejection, not a callback to a disposed Razor component.

The agent does not call a “current position” tool. Context preflight is deterministic host behavior and completes before an execution run is created. A model-initiated tool would add latency, consume tokens, and permit the model to omit or delay the lookup.

### Dependency and component boundary

```text
Web composition root
  +-- WorkspaceAgentChatContextProvider -> WorkbenchStateService
  +-- FloatingAgentChatHost             -> IAgentChatContextRegistry

Feature module page
  +-- reusable AgentChatContextSurfaceProvider (AgentFramework.Components)
  +-- module-owned sanitized position builder
          |
          v
AgentFramework.Core contracts and scoped registry
          |
          v
per-prompt orchestrator -> immutable invocation -> fresh runtime
```

`AgentFramework.Components` depends on Core. Feature modules may depend on the shared Components package, but never on `CanDoItAll.Modules.AgentFramework`. Core does not reference Workbench, Projects, CRM-HR, Processes, Scheduler, Resources, or their domain types. Web remains the only composition root that mounts both the workspace publisher and global floating host.

### Pattern selection record

Selected:

- **Observer plus disposable lease** for `WorkbenchStateService.Changed` to ambient workspace-position updates.
- **Immutable value snapshot** for atomic prompt capture and navigation safety.
- **Reusable headless Razor component** for active-surface registration and cleanup without duplicating lifecycle code in every page.
- **Module-owned adapter/builder** for privacy allowlisting and semantic selection mapping.

Rejected:

- raw click/event logging, because clicks do not identify semantic selections and create noisy or sensitive history;
- URL-only context, because internal tabs and selected rows generally do not update routes;
- a central enum/switch over every module, because it closes the extension point and couples Core to feature modules;
- reflection over component fields/parameters, because it is unbounded and can leak credentials, rates, notes, prompts, or personal data;
- lazy callbacks retained by the registry, because navigation can leave callbacks targeting disposed components;
- model-initiated position lookup, because context is required before the model run exists.

### Acceptance and phased validation

1. Core contract tests prove workspace/surface replacement, disposal, cancellation, count/length bounds, atomic versioning, and that position-only context contributes to a run.
2. Shared-component tests prove activation, in-place updates, source replacement, and disposal without a routed page or database.
3. Module tests prove sanitized position builders for Projects, Project Structure, CRM-HR, Agents, Workflows, Processes, Scheduler, and Resources, including negative privacy assertions.
4. Dependency tests prove the new context path does not add a Core-to-feature or AgentFramework-to-feature reference, and composition tests prove the Web scope resolves one registry used by workspace, module, and floating host. Existing unrelated module dependencies are recorded rather than hidden by this feature.
5. Browser tests keep one chat open while moving across module tabs and internal subtabs. Each next prompt must persist the expected `SourceKind`, `SourceId`, contributor list, and a changed context digest; selected names/IDs are also checked through a context-reporting prompt where provider execution is available.
6. Failure tests prove that navigation/disposal never reuses the previous module context and that an unavailable context blocks the prompt explicitly instead of running with stale data.

Implementation proceeds foundation first, then existing adapters, then new module coverage, then browser proof. Each phase must pass before the next module group is integrated.

## Active-chat lifecycle

The coordinator exposes separate launcher and lifecycle views:

- show or hide the global catalog;
- start a new durable session for an agent;
- open an existing session as an active handle;
- activate one handle while preserving other hidden handles;
- mark a handle busy/idle and update its session after a send;
- keep a closed handle active or stop it;
- prune expired hidden, idle handles;
- expose an immutable ordered snapshot and one state-changed event for the host.

Multiple active sessions for the same agent are allowed. Handle identity is not agent identity.

Each visible floating panel must acquire a coordinator operation lease before send or approval continuation. The lease spans context capture, runtime execution, completion notification, and transcript hydration. Runtime terminal events therefore cannot mark a handle idle while post-run refresh is still in progress, and a hidden/reopened panel cannot start an overlapping turn. A panel that reopens during an external in-flight operation observes the matching terminal execution event and reloads the durable session once, so assistant output is not stranded in the disposed panel that initiated the turn.

Closing a visible chat opens an explicit decision dialog:

- **Keep active** hides it and starts its inactivity retention window.
- **Stop** removes the active handle and transient draft/voice state while leaving history intact.
- **Cancel** leaves the window open.

Visible or busy chats never expire. Hidden idle chats expire after the configured retention period. Cleanup uses one coordinator-level schedule driven by `TimeProvider`, not one timer per chat. Expiry is also checked opportunistically on coordinator operations so delayed timers do not preserve stale handles indefinitely.

The first release does not claim that Stop cancels a running model call. Current cancellation is keyed to process runs, not interactive execution/chat identity. Until run/session cancellation is added and tested, the UI disables Stop while the handle is busy and reports the reason explicitly.

## Settings

Persist a dedicated typed floating-chat settings document under the versioned key `floating-agent-chats.v1`, using the existing keyed settings table without coupling it to `WorkflowSettings` serialization:

- hidden active-chat retention minutes;
- maximum active chat handles per circuit;
- maximum prepared agents/resources;
- adaptive preparation enabled;
- prepared-resource idle retention minutes.

Normalization enforces bounded positive values. The UI explains that an active handle is lightweight and that prepared-resource capacity is a separate runtime optimization.

`MaximumPreparedAgents` defaults to zero. A nonzero value affects only the circuit-scoped `AgentChatPreparationPool`, which warms bounded, invalidatable active-agent definition metadata. It is separate from the scoped execution-preparation cache and the singleton canonical provider snapshot, and it must not cause the UI layer to retain live runtime builds.

## Prepared-agent stock decision

Pooling the current `RuntimeBuildResult` or `AIAgent` is rejected. Those objects capture turn-specific context intent, runtime session key, tool and approval policy, attachments, credentials/configuration, scoped contributors, MCP/A2A clients, handoff participants, and async disposables. Reusing them can cross-contaminate Project Structure and CRM-HR context, preserve revoked authorization, race concurrent turns, or leak processes and credentials.

The implemented `AgentChatPreparationPool` remains intentionally metadata-only. It caches a bounded set of immutable active `AgentDefinition` values, adapts ordering from usage counts when enabled, has idle eviction, serializes refresh, and invalidates on reference-data changes.

Actual execution preparation is handled by the separate scoped `AgentExecutionPreparationCache`. It single-flights immutable blueprints keyed by database profile, workspace, and agent and versions them by catalog revision, database-profile generation, and provider-configuration fingerprint. The durable run-admission boundary validates the blueprint again before use. Stale or superseded entries fail or refresh explicitly; they do not silently fall back to a mismatched agent/provider.

`CanonicalProviderRuntimeProfileSnapshotService` is an immutable singleton projection initialized after database readiness. It carries database identity/generation and provider concurrency revisions, probes the canonical revision at use time, and fails closed if the revision cannot be verified. Resolved secret values are prepared separately for one execution dispatch and cleared with that scope.

These mechanisms remove repeated safe local loading without pooling live agents. Context, tools, approval wrappers, attachments, session state, credential scope, provider call, and async runtime disposables remain per execution. A full-runtime pool still requires thread-safety proof and benchmark evidence before this decision can change.

## Concurrency and failure closure

The coordinator serializes its own in-memory mutations and emits immutable snapshots after releasing its lock. It never invokes event subscribers while holding the lock.

One active handle permits at most one send at a time. The split file store now admits a chat-backed run through `BeginChatBackedRunAsync`, which checks the blocking run and creates the new session/run projection under one workspace admission lock. Typed pending commit journals recover chat-backed creation, generic creation, and existing-run updates after an interrupted multi-file commit. Cross-host coordination is still outside the process-local lock and must not be inferred from this guarantee.

Context registration updates use a generation/version. A send captures a complete snapshot atomically. No send reads mutable page state after its first await.

Errors are explicit:

- invalid or expired handles fail rather than silently creating another session;
- unavailable agents fail rather than selecting a different agent;
- context registration misuse fails rather than attaching to an arbitrary scope;
- settings validation reports the invalid bound;
- preparation capacity, invalidation, churn, stale-use, provider-snapshot-not-ready, and provider-snapshot-fault states fail explicitly; no silent stale-data fallback is permitted.

Logs and traces include handle, agent, session, context source, and context version IDs. They exclude prompt text, secrets, attachment content, partner contact details, and credential material.

## Pattern selection record

Selected patterns:

- **Scoped coordinator/state store** for one circuit-wide source of active-chat truth.
- **Disposable registration/lease** for module context lifetime without retaining component references.
- **Immutable snapshot** for per-turn context consistency across asynchronous navigation.
- **Application host** in the Web composition root for cross-route rendering.
- **Versioned immutable preparation cache** for safe reusable agent/provider/capability/memory data.
- **Fresh per-turn runtime** for context-, policy-, credential-, and client-bearing objects.

Rejected patterns:

- a singleton chat service, because it crosses user/profile boundaries;
- a closed workspace enum with central switch statements, because every new module would modify Agent Framework code;
- service location from Razor or runtime code, because it hides dependencies and lifetime errors;
- one floating host per module, because chat state and event subscriptions would duplicate and diverge;
- storing component callbacks or domain entities in the coordinator, because navigation would leave stale references;
- one timer per active chat, because resource use grows with handle count;
- retaining a runtime build as the meaning of an active chat, because it retains unsafe per-turn resources;
- moving the global host into `CanDoItAll.AppComponents`, because that package is generic shell infrastructure and must not acquire an Agent Framework dependency;
- expanding the legacy Razor partial/monolith, because it would preserve the current false boundary.

## Performance assessment

The initial scan covered Agent Framework Core, MAF, Persistence, Providers, Voice, the Agent Framework module, and related API/UI paths.

Observed hotspots relevant to this design:

- chat session listing performs a summary query followed by one read per session in the split file store;
- execution progress repeatedly loads and saves run detail during streaming;
- the global `ExecutionUpdated` event causes every mounted chat coordinator to filter and clone log lists;
- contextual history applies `Take(25)` after loading sessions;
- history export performs sequential session/run/detail reads;
- provider/runtime pools and per-key dispatch semaphores currently have no universal idle-capacity eviction;
- voice paths can retain encoded audio/chunk arrays and are not canceled by the current close lifecycle;
- one runtime-build path creates `JsonSerializerOptions` per build;
- a credential resolver uses synchronous blocking through `Task.Run(...).GetAwaiter().GetResult()`;
- three manually constructed `HttpClient` instances were confirmed in the audited scope.

The new coordinator therefore uses bounded collections, starts its single cleanup schedule only while hidden handles or prepared entries exist, and emits immutable active-handle events. It does not cache full transcripts or runtime logs. Render-hot host and session filters are cached; context access lookup is built once per immutable scope; matching completion subscribers run concurrently and isolate failures. The history dialog consumes indexed session summaries and loads a full session only when it is opened. The focused panel reads detail for only its selected run instead of scanning every run in the conversation.

Focused chat hydration uses an optional combined projection-query capability. The split file store reads and parses the chat index once to obtain the selected agent's session and run summaries, then reuses that immutable projection to resolve the latest run with explicit timestamp ordering. Stores that implement only the original chat-query contract remain source compatible and use the established individual queries. No decoded multi-megabyte chat index is retained in circuit memory, avoiding stale cross-process state and unbounded retry/cache growth.

Voice recorder/playback state is owner-scoped. Agent changes, permission loss, voice-off, unavailable workspace, and panel disposal rotate the owner and cancel in-flight transcription/synthesis. Generation fencing prevents a late voice result from reaching another agent. Browser code releases a stream when `MediaRecorder` construction fails and enforces a five-minute client-side recording watchdog, including when the Blazor circuit disconnects. The per-runtime logging serializer options are now static.

The second-pass feature scan found no sync-over-async, `Task.Run`, culture-sensitive comparison, unbounded static collection, or manually-created `HttpClient` in the new paths. The older bulk `ListChatSessionsAsync` API still has an N+1 split-store implementation for its remaining callers, and execution progress still persists run detail frequently; neither is used as justification to pool unsafe runtime state.

Performance changes are accepted only with before/after measurements. Network/model latency must be reported separately from local runtime composition so a warm-resource change is not credited for unrelated provider variance.

## Phased implementation and closure gates

### Phase 0: characterization and architecture

- Preserve focused tests for Project Structure prompt/access behavior.
- Record project dependencies and the runtime-build lifecycle.
- Accept this decision before adding services.

Gate: no new project cycle, no new partial-class boundary, and no live-runtime pooling.

### Phase 1: typed context and active lifecycle — implemented

- Add contracts, settings normalization, scoped coordinator, immutable context registrations, and `TimeProvider`-based expiry.
- Add unit tests for registration/update/disposal, context replacement, multiple contributors, invalid leases, new/open/keep/stop, busy protection, maximum handles, and deterministic expiry.
- Add a DI composition smoke test.

Gate: the services are testable without rendering Razor or using a database; negative cases are covered.

### Phase 2: global host and Project Structure migration — implemented

- Compose one `OverlayWindow` host above routed module content.
- Add catalog tabs for Agents and Active chats.
- Reuse `AgentChatPanel`/`ChatWorkspacePanel` with exact preferred session identity and immutable context capture per send.
- Implement keep/stop close confirmation.
- Replace the Canvas-local Project Structure window with launcher and context registration.
- Remove the legacy contextual window and its closed workspace enum once its final consumer is migrated.

Gate: Canvas to Gantt preserves the host and session; no feature module references the Agent Framework module; the old monolith shrinks or is deleted.

### Phase 3: settings and additional modules — implemented

- Add the Agent Framework floating-chat settings panel and persisted defaults.
- Add one second module context adapter, CRM-HR, to prove the contract is open for extension.
- Prove consecutive turns in one session receive Project Structure then CRM-HR context without retaining the previous selection.

Gate: backward-compatible settings deserialization, cross-module integration proof, and no domain entity retained by the coordinator.

### Phase 4: runtime correctness — activity/admission persistence implemented; interactive cancellation deferred

Implemented now: exact transient-context retention across approval continuation, provider-native pending-session restoration for contextual approval resumes, fail-closed restart behavior, framework-managed history for new contextual turns, typed completion notifications, coordinator operation leases with per-handle send gating after remount, immediate typed activity handles, profile-fenced activity readers, atomic process-local chat-run admission, pending commit-journal recovery, terminal-event transcript refresh for reopened panels, and the indexed summary history path.

- Add interactive cancellation by execution/session identity.
- Add a cross-host chat-session admission strategy if multiple app hosts are allowed to share one file workspace; the current lock is process-local.
- Make runtime-build disposal failure-tolerant and session-serialization timeout ownership explicit.

Gate: double-send, cancel, disposal-failure, and approval-resume tests pass across persistence modes.

### Phase 5: measured preparation — immutable preparation implemented; live runtime pooling rejected

Implemented now: circuit-scoped catalog metadata preparation, scoped single-flight immutable execution blueprints, singleton provider configuration snapshots with revision/profile fences, one-dispatch credential scopes, activity timing for reused/refreshed preparation, and focused invalidation/concurrency tests.

- Continue separating local preparation measurements from provider/network latency.
- Pool an additional context-free provider transport only after its own bounded lifetime, invalidation, concurrency, and disposal proof.
- Do not pool `RuntimeBuildResult`, `AIAgent`, credentials, turn context, attachments, or tool/client graphs.

Gate: measured local improvement, no stale-context/authorization reuse, bounded memory/process counts, and clean disposal. `MaximumPreparedAgents` remains a metadata-pool setting, not permission to retain live runtime state.

### Final validation

- targeted Unit and bUnit suites after each phase;
- Agent Framework Core, Components, Workbench, Agent Framework module, and Web builds;
- integration tests for durable session versus active handle and context changes;
- deterministic browser proof for Project Structure Canvas/Gantt, navigation, keep/reopen, and stop before rollout;
- two-pass performance scan with exact finding counts and manual review;
- refreshed CodeAnalytics snapshot and architecture-review gate.

## Known rollout risks

- A currently running Web process can lock build outputs; validation must use an isolated output path or intentionally stop the development host with user approval.
- Browser-level MediaRecorder behavior and the full cross-route Canvas/Gantt/CRM journey still require a deterministic smoke test before rollout.
- CRM currently has no canonical per-agent access projection. Its adapter is explicitly `Unrestricted`, exposes only sanitized identifiers/display labels/typed status/source/role data, and grants no workspace or tool authorization.
- Automatic Project Structure and CRM refresh after a successful mutation uses typed completion notifications and disposable subscriptions; additional modules must follow the same pattern.
- Active-handle expiry cannot be represented as durable-session deletion.
- Context text is external-model input and must remain bounded, sanitized, and visibly sourced even though its identifiers and lifecycle are strongly typed.
