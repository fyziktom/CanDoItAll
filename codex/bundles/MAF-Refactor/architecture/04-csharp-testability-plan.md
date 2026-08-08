# C# testability plan

## Characterization tests before extraction

### Floating context

1. Atomic publication exposes either the old or new whole snapshot, never a mix.
2. Strict capture rejects route/navigation mismatch.
3. Loading/failed access state rejects execution.
4. An admitted turn retains its original attachment object and digest after registry update.
5. Approval continuation uses the original transient context.
6. Missing original transient context fails closed.
7. Project Structure Canvas and Gantt publish distinct view facts.

### Runtime

1. Provider retry and temperature fallback behavior remains unchanged.
2. Required finalizer capture and repair behavior remains unchanged.
3. Session serialization and approval replay behavior remains unchanged.
4. Runtime build disposal order remains unchanged.
5. Tool invocation policy and receipt correlation remain unchanged.

## Isolated unit tests to add

### Context model

- `Classify_SameProjectCanvasToGantt_ReturnsViewChanged`
- `Classify_ProjectXToProjectY_ReturnsSourceEntityChanged`
- `Resolve_FollowCurrentSurface_AdoptsCurrentObservation`
- `Resolve_Detached_DoesNotComposeUiContext`
- `Compose_TransitionHeader_IsApplicationGeneratedAndBounded`

### Authority

- `Resolve_ObservationRequestsUnauthorizedProject_Denies`
- `Resolve_ReadOnlyAgent_DoesNotGrantMutation`
- `Resolve_ProfileGenerationChanged_FailsClosed`
- `Resolve_SameProjectViewChange_ProducesEquivalentScopeIdentity`
- `Resolve_ProjectChange_ProducesNewAuthorityFingerprint`

### Workspace services

- `Create_ProjectScope_AllServicesShareScopeIdentity`
- `Create_OrganizationScope_AllServicesShareScopeIdentity`
- `Create_MismatchedServiceIdentity_Throws`
- `Create_Dispose_ReleasesOwnedServicesOnce`

### Runtime ports

- execution adapter tests without diagnostics/admin services,
- diagnostics tests without execution coordinator,
- continuation tests with persisted application approval decisions,
- unknown/unsupported adapter state tests.

### Process recovery

- recovery from exact current-run primary artifact succeeds,
- historical artifact is rejected,
- wrong path is rejected,
- status-only Blocked is rejected,
- recovered result still passes normal process completion gates.

### Lightweight LLM, workflow transform, and future ordinary conversation

- direct text invocation with a fake provider runtime/driver,
- ordered system/user/assistant message mapping,
- valid JSON response-format/schema transform,
- invalid schema result remains workflow-owned evidence,
- provider failure and usage evidence,
- cancellation/deadline propagation,
- streaming sequence and exactly one terminal usage source where supported,
- no agent, session, tools, memory, handoff, approvals, finalizer, workspace services, or context contributors,
- payload project ID/path cannot grant scope or authority,
- provider runtime/driver is invoked exactly once,
- future ordinary-conversation service owns transcript above the stateless port and does not construct an agent.

## Negative architecture tests

Add source/project assertions that fail if:

- MAF references `Modules.*`,
- MAF contains process-step symbols,
- Runtime/Core gets a new MAF SDK dependency,
- a new partial `MafAgentRuntime` file appears,
- a new `IServiceProvider` field appears in runtime classes,
- broad `IAgentRuntime` gains a new production caller,
- global tool collisions are silently deduplicated,
- workflow LLM invoker calls full agent execution runtime,
- `Llm.Abstractions` references MAF, agent/session contracts, UI/modules, workspace authority, or provider SDKs,
- lightweight LLM implementation creates a parallel credential/HTTP/retry/usage stack,
- ordinary LLM conversation constructs an `AgentDefinition` or MAF session,
- Project Structure UI fragments contain durable tool protocol instructions after migration.

## Composition smoke tests

- host resolves all narrow runtime ports,
- context capture service resolves authority and produces a request,
- Workbench registers Project Structure context contributors,
- Processes registers process recovery/provider-selection policies,
- Security implementation satisfies the extracted secret resolver contract,
- MAF adapter and workflow adapter can be registered independently,
- scope-bound workspace factory creates a complete bundle,
- provider-backed LLM adapter resolves through the existing provider runtime/driver graph without creating agent services,
- workflow LLM adapter and future ordinary-conversation contract can register without MAF agent execution.

## Practical scenario matrix

| Scenario | Expected observation | Expected authority | Expected run behavior |
|---|---|---|---|
| Canvas -> Gantt before send | Gantt | same project after revalidation | new turn includes ViewChanged |
| Canvas -> Gantt during run | run stays Canvas; registry becomes Gantt | run authority unchanged | next turn gets Gantt |
| Project X -> Project Y | Project Y | newly resolved Project Y | new turn only |
| Project X approval while viewing Y | original Project X turn | original Project X authority | continuation cannot retarget |
| context loading | none admitted | none | fail closed |
| context attachment expired | capture rejected or refreshed | none until valid | no stale execution |
| detached chat | no UI observation | ordinary chat authority | transcript-only turn |
