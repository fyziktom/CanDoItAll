# C# Pattern Selection Records

## PSR-01 Thin Facade For `MafAgentRuntime`

### Context

`MafAgentRuntime` must remain the public `IAgentRuntime` implementation but cannot keep owning execution internals.

### Forces

- extension growth: new turn behavior currently changes the runtime.
- construction complexity: runtime currently constructs collaborators.
- testability: unit tests need to target turn behavior without full runtime construction.
- dependency direction: public callers should depend on `IAgentRuntime`; internal runtime behavior depends on focused collaborators.

### Selected Pattern

Facade, strictly thin. The facade delegates and owns no business behavior.

### Rejected Alternatives

- Partial class: repeats the original smell.
- Inheritance: not needed; composition is clearer.
- Service locator: hides dependencies and hurts tests.

### New Types And Projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `IMafRuntimeTurnCoordinator` | `CanDoItAll.AgentFramework.Maf` | Turn orchestration contract for runtime facade. |
| `MafRuntimeTurnCoordinator` | `CanDoItAll.AgentFramework.Maf` | Runtime turn orchestration. |
| `IMafApprovalContinuationDriver` | `CanDoItAll.AgentFramework.Maf` | Approval continuation contract. |

### Test Plan

| Test | Behavior proven |
| --- | --- |
| `MafAgentRuntime_RunAsync_DelegatesToTurnCoordinator` | Runtime is a thin facade. |
| `MafRuntimeTurnCoordinator_RunAsync_PreparesBuildAndExecutesTurn` | Coordinator owns orchestration without runtime. |

## PSR-02 Drivers For Turn Execution, Finalizer Repair, Session Persistence, And Approval Continuation

### Context

`ExecuteRunAsync` and surrounding helpers mix provider streaming, finalizer repair, session persistence, approval handling, and response assembly.

### Forces

- multiple algorithms: finalizer repair and persistence decisions vary by run mode.
- external SDK isolation: `AIAgent` and `AgentSession` should be behind narrow seams where needed.
- testability: provider streaming and failure paths need fake updates.

### Selected Pattern

Driver plus Strategy where behavior varies by runtime state.

### Rejected Alternatives

- One new `MafRuntimeExecutionManager`: too broad.
- Static helper methods: still hard to fake and extend.
- Full SDK wrapper everywhere: too much abstraction.

### New Types And Projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `MafRuntimeTurnExecutor` | `CanDoItAll.AgentFramework.Maf` | Streaming loop and response assembly. |
| `MafFinalizerRepairCoordinator` | `CanDoItAll.AgentFramework.Maf` | Required finalizer repair/fallback. |
| `MafRuntimeSessionPersistenceDriver` | `CanDoItAll.AgentFramework.Maf` | Session serialization and scrubbing. |
| `MafApprovalContinuationDriver` | `CanDoItAll.AgentFramework.Maf` | Approval cache/rehydration. |

### Test Plan

| Test | Behavior proven |
| --- | --- |
| `Execute_ProviderEmitsApproval_PersistsPendingApproval` | Approval response path remains compatible. |
| `Persist_RequestScopedAttachments_SkipsOrScrubsSessionState` | Request-scoped data is not persisted. |
| `Repair_MissingRequiredFinalizer_RunsBoundedRepair` | Finalizer repair moved and remains active. |

## PSR-03 Builders And Factories For Runtime Construction

### Context

`MafRuntimeAgentFactory` constructs runtime agents and also owns policy and instrumentation.

### Forces

- construction complexity: runtime build has many required inputs.
- runtime selection: normal vs handoff build.
- testability: build decisions need fake capability composer/provider factory.

### Selected Pattern

Builder/coordinator for runtime build, factory for hosted-agent creation, strategy for handoff build.

### Rejected Alternatives

- Keep `MafRuntimeAgentFactory` large: fails user request.
- Put construction into DI registration lambdas: composition root would own behavior.
- Inject `IServiceProvider` everywhere: service locator.

### New Types And Projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `MafRuntimeBuildCoordinator` | `CanDoItAll.AgentFramework.Maf` | Main runtime build workflow. |
| `MafHandoffRuntimeBuilder` | `CanDoItAll.AgentFramework.Maf` | Handoff-specific runtime build. |
| `MafHostedAgentFactory` | `CanDoItAll.AgentFramework.Maf` | Hosted agent creation. |
| `MafToolPolicyInstrumentor` | `CanDoItAll.AgentFramework.Maf` | Tool wrapping and ownership metadata. |
| `MafScriptPolicyInspectionService` | `CanDoItAll.AgentFramework.Maf` | Script policy inspection. |

## PSR-04 Catalog Providers For Capability And Workspace Tool Contributions

### Context

New capability or workspace tool additions should be contributed through catalogs/tool sets, not by editing the old runtime/composer/plugin.

### Forces

- extension growth: tool families will grow.
- typed metadata: descriptors and access rules must stay with implementation.
- testability: each family needs isolated metadata/execution tests.

### Selected Pattern

Catalog provider plus tool-set classes. Use a facade only temporarily for method compatibility.

### Rejected Alternatives

- Keep one `WorkspaceRuntimePlugin`: remains a god object.
- One generic workspace tool helper: hides different side effects and policy needs.
- Stringly typed descriptor maps only: hard to refactor safely.

### New Types And Projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `RuntimeCapabilityAccessPlanner` | `CanDoItAll.AgentFramework.Maf` | Access policy planning. |
| `RuntimeCapabilityDescriptorCatalog` | `CanDoItAll.AgentFramework.Maf` | Descriptor mapping. |
| `RuntimeCapabilityAttachmentOrchestrator` | `CanDoItAll.AgentFramework.Maf` | Attachment pipeline. |
| `WorkspaceFileToolSet` | `CanDoItAll.AgentFramework.Maf` | File/search/stat tools. |
| `WorkspaceCommandToolSet` | `CanDoItAll.AgentFramework.Maf` | Git/dotnet/command tools. |
| `WorkspaceScriptToolSet` | `CanDoItAll.AgentFramework.Maf` | Script execution and side-effect manifest. |
| `WorkspaceArtifactToolSet` | `CanDoItAll.AgentFramework.Maf` | Document/spreadsheet/artifact tools. |
| `WorkspaceImageAnalysisToolSet` | `CanDoItAll.AgentFramework.Maf` | Image inspection/analysis. |

## Implementation Update - 2026-07-06

Implemented in this pass:

- Driver: `MafApprovalContinuationDriver` for pending approval cache, mapping, and compatibility rehydration.
- Driver: `MafRuntimeSessionPersistenceDriver` for session serialization skip/scrub/timeout policy.
- Assembler: `MafRuntimeResponseAssembler` for response/usage/failure assembly helpers.
- Service: `MafScriptPolicyInspectionService` for script content and side-effect policy inspection.
- Catalog/planner: `RuntimeCapabilityAccessPlanner`, `RuntimeCapabilityAccessPolicyBuilder`, `RuntimeCapabilityDescriptorCatalog`, `RuntimeConfiguredWorkspaceToolDescriptorCatalog`, `RuntimeRegisteredToolProviderAttacher`, `RuntimeStorageToolNames`, and `RuntimeToolProcessIntentPolicy`.
- Tool set: `ConfiguredWorkspaceToolSet` replacing `ToolCapabilityBuilder` partial ownership for configured workspace tools.
- Resolver: `WorkspaceImageAnalysisModelResolver` replacing image-analysis model selection inside `WorkspaceRuntimePlugin`.

Not fully implemented in this pass:

- `MafRuntimeTurnCoordinator` and `MafRuntimeTurnExecutor`.
- `MafRuntimeBuildCoordinator`, `MafHandoffRuntimeBuilder`, `MafHostedAgentFactory`, and `MafToolPolicyInstrumentor`.
- Full workspace file/command/script/artifact/image tool-family extraction.
- Full attachment orchestration split from `RuntimeCapabilityComposer`.
