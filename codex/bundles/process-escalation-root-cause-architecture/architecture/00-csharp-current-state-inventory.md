# C# Current State Inventory

## Source Files Inspected

- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueServices.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageAnalysisPromptNormalizer.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`

## Large Classes And Partial Classes

- `ProcessRuntimeDispatchApplicationService`: about 1735 lines, about 60 members. Responsibilities include dispatch loop, scheduling, claim cleanup, strategy dispatch, branch signal routing, automatic retry prompt mutation, repeated retry suppression, and diagnostics aggregation.
- `ProcessRuntimeProjectionQueryService`: about 1441 lines, about 59 members. Responsibilities include live run queries, run detail, history, active agents, operator actions, current-step enrichment, child-run waits, and metadata enrichment.
- `ProcessRuntimeEvidenceSourceProvider`: about 1367 lines, about 49 members. Responsibilities include DB source queries, runtime state source mapping, assignment mapping, receipt mapping, artifact ledger mapping, projection history mapping, dead-letter mapping, and agent-session observations.
- `ProcessRuntimeEngine`: partial class cluster with result handling, rework, claims, commit helpers, and state transitions. The partial structure is acceptable only as existing runtime split; this bundle must not add new partial files as an architectural escape hatch.
- `AgentFrameworkProcessExecutionAdapter`: partial class cluster in the process module. It is the correct integration boundary, but product-completion validation must not accumulate domain-specific rules.
- MAF runtime files such as `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` are large and relevant for context assembly, but this bundle should touch them only through explicit capability-readiness contracts.

## Constructor Dependency Counts

- `ProcessRuntimeDispatchApplicationService`: 10 constructor parameters, including stores, strategy resolver, catchup service, optional queue, and optional branch signal router.
- `ProcessRuntimeProjectionQueryService`: 6 constructor parameters, including projection store, codec, clock, optional runtime state store, assignment store, and execution observation reader.
- `ProcessRuntimeEvidenceSourceProvider`: 2 constructor parameters, but a high responsibility count through direct DB set mapping.
- `RuntimeCapabilityAccessPolicyBuilder`: static policy builder; no constructor, but owns multiple access-policy decisions.

## Direct Instantiation Points

- `ProcessRuntimeDispatchApplicationService` directly instantiates `ProcessRuntimeScheduler`, `ProcessRuntimeEngine`, and `ProcessStrategyDispatcher` inside `ExecuteReadyAsync`.
- It also creates a default `ProcessRuntimeBranchSignalApplicationService` when no override is supplied.
- These direct instantiations make isolated classification/readiness tests harder because tests must drive the full dispatch loop or inject only high-level collaborators.

## Provider, Tool, Driver, And Runtime Responsibilities

- Runtime responsibilities currently include state transitions, result receipts, artifact ledger events, and dispatch claim handling.
- Application responsibilities currently include dispatch orchestration, automatic retry instruction mutation, timeout handling, repeated retry suppression, and branch signal routing.
- Process driver abstractions exist, but driver-owned recovery and typed capability readiness are incomplete.
- MAF capability composition can deny tools based on settings and allowed operations, but process launch/readiness lacks a unified diagnostic model for missing/denied capabilities.
- Process templates encode .NET and screenshot details in prose. Those details belong to domain templates/drivers, not generic runtime.

## Current Tests

- Unit tests cover capability scope materialization and tool policy behavior, including workspace tool denial and some Calculator/Tetris fixtures.
- Integration tests cover project-structure agent launch variables, .NET scaffold contracts, product completion required tool receipts, and process API behavior.
- Component tests cover process workspace shell blocked-action rendering.
- Playwright tests cover project-structure artifact browser and screenshot evidence flows.

## Missing Tests

- No focused unit tests prove blocked strategy diagnostics are persisted and projected with actionable categories.
- No launch/readiness tests prove that missing MCP, denied tool, or suppressed skill is detected before dispatch.
- No tests prove manager recovery chooses driver-specific recovery only after typed classification.
- No architecture tests prevent .NET/Blazor/Calculator/Tetris/screenshot/Playwright domain strings from entering generic runtime/dispatcher layers.
- No replay test isolates the latest escalation category from the reverted patch contamination.
- No management-only process test proves development skills/tools are suppressed while the agent keeps them globally.

## Risk Notes

- Adding more validation to `AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs` is high risk because it can leak domain behavior into a generic adapter boundary.
- Adding more prompt text to process templates may change model behavior without improving deterministic capability enforcement.
- Adding fallback without typed classification would hide root causes and make future E2E failures harder to diagnose.
- New abstractions should be small and evidence-driven. Avoid broad service layers that only forward calls.
