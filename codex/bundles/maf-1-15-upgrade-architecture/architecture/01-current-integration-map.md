# Current Integration Map

```mermaid
flowchart TB
    Host[AgentFrameworkServiceCollectionExtensions]
    RuntimeSingleton[MafAgentRuntime singleton facade]
    Prep[Immutable preparation cache / revisioned blueprints]
    ProviderRegistry[Provider profiles and snapshots]
    Workspace[Workspace file, path, command, image and artifact services]
    Checkpoint[WorkflowBackedAgentExecutionCheckpointBridge]
    A2A[A2A hosting]

    Host --> RuntimeSingleton
    Host --> Prep
    Host --> ProviderRegistry
    Host --> Workspace
    Host --> Checkpoint
    Host --> A2A

    RuntimeSingleton --> Run[Per-execution runtime build]
    Prep --> Run
    ProviderRegistry --> Run
    Workspace --> Run

    Run --> AgentFactory[MafRuntimeAgentFactory]
    Run --> SessionBuilder[MafRuntimeSessionBuilder]
    Run --> SessionPersistence[MafRuntimeSessionPersistenceDriver]
    Run --> ApprovalDriver[MafApprovalContinuationDriver]
    Run --> Streaming[Provider streaming runner]
    Run --> ResponseAssembler[MafRuntimeResponseAssembler]

    AgentFactory --> Agent[ChatClientAgent / workflow-hosted agent]
    AgentFactory --> Tools[Per-run tools and context providers]
    AgentFactory --> Policy[CanDoItAll tool policy middleware]
    AgentFactory --> Handoff[MafHandoffWorkflowFactory]
    Handoff --> DepthGuard[HandoffDepthGuardAgent]

    SessionBuilder --> MafSession[MAF AgentSession]
    ApprovalDriver --> AppPending[Custom PendingToolApprovalRecord]
    ApprovalDriver --> ProcessCache[Process-local raw request cache]
    SessionPersistence --> Serialized[Opaque serialized MAF session JSON]

    Streaming --> Updates[AgentResponseUpdate stream]
    Updates --> Activity[Activity/progress callbacks]
    Updates --> Merge[MEAI ToAgentResponse]
    Merge --> ApprovalExtract[Pending approval extraction]
    Merge --> Finalizer[Finalizer validation/repair]
    Merge --> Usage[Usage normalization]

    Serialized --> AppStore[Sandbox/application session store]
    AppPending --> AppStore
```

## Key Boundary Rules

- The singleton runtime is a facade, not a mutable agent/session singleton.
- Preparation cache stores immutable definitions and snapshots.
- Runtime builds own mutable agents, sessions, tools, context, provider state, and disposables.
- Custom workspace services are the canonical file/tool boundary.
- The application transcript and compatibility record are separate from opaque MAF session state.
- Workflow activity and authoritative final output are currently projected from the same stream, which is the main 1.15 design pressure.
