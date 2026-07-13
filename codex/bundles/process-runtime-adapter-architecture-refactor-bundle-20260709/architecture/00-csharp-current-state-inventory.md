# C# Current State Inventory

## Scope

This inventory covers:

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration`
- `src/Processes/CanDoItAll.Processes.Runtime`
- `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions`
- `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard`
- `src/Processes/CanDoItAll.Processes.Templates`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands`

CodeAnalytics snapshot: `snap-20260709171252-c371d5d2`.

## Adapter Partial Cluster

Current adapter partial files and observed responsibilities:

| File | Primary responsibility currently hidden in adapter |
|---|---|
| `AgentFrameworkProcessExecutionAdapter.cs` | Main orchestration, assignment loading, executor validation, agent execution, preflight, .NET setup entry point, gate invocation, result flow. |
| `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` | Result conversion, branch outcome normalization, diagnostics, manager signals, generic summaries, helper parsing. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs` | Product/process receipt enforcement, .NET receipt recognition, script guidance, receipt summary formatting. |
| `AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs` | Managed artifact materialization, content building, artifact append/acceptance, receipt creation. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionParsing.cs` | Parsing product completion requirements and structured checks. |
| `AgentFrameworkProcessExecutionAdapter.Grounding.cs` | Grounded reference validation and readback checks. |
| `AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs` | Completion issue aggregation, routing result creation, runtime gate findings summaries. |
| `AgentFrameworkProcessExecutionAdapter.Subprocess.cs` | Subprocess launch/defer/complete behavior. |
| `AgentFrameworkProcessExecutionAdapter.SubprocessState.cs` | Child run state and subprocess status handling. |
| `AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs` | Tool receipt/evidence heuristics for managed artifacts and product mutation. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs` | Product output paths and required product file path checks. |
| `AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs` | Runtime-owned .NET setup execution path. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionRetryPolicy.cs` | Retry issues for product mutation and completion gates. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs` | Product state/blocker contradiction checks. |
| `AgentFrameworkProcessExecutionAdapter.Metadata.cs` | Launch context metadata and mutation/proof flags. |
| `AgentFrameworkProcessExecutionAdapter.RecoveryPolicy.cs` | Retryable transient execution issue creation. |
| `AgentFrameworkProcessExecutionAdapter.AcceptanceCriteria.cs` | Acceptance criteria parsing/checking. |
| `AgentFrameworkProcessExecutionAdapter.CompletionGates.cs` | Gate delegate construction and evaluator property. |
| `AgentFrameworkProcessExecutionAdapter.Results.cs` | Shared result helpers. |
| `AgentFrameworkProcessExecutionAdapter.Types.cs` | Local records/types supporting the partial cluster. |

Inventory verdict:

- The file split is a naming split only. It does not provide compile-time ownership, reusable contracts, or isolated test seams.
- The cluster must shrink by moving responsibilities to top-level services.
- New partial files are forbidden by this bundle.

## Existing Extracted Types

| Type/File | Current status | Required next action |
|---|---|---|
| `ProcessCompletionGateEvaluator` | A real class exists, but it is internal and module-local, with local issue/context records. | Move or wrap behind a process-runtime/driver contract if used outside adapter. Split gates into named top-level gate classes where behavior is independently testable. |
| `ProcessRequiredToolReceiptGate` | Internal static gate exists and uses typed `ProcessRequiredToolReceipt`. | Promote to a service/contract seam if runtime, adapter, and driver policies need it. Add branch applicability and product/process dedupe tests. |
| `DotNetSolutionSetupRuntimeExecutor` | Real domain-specific executor exists in module integration. | Move behind domain driver policy/executor contract. Adapter must not know this concrete .NET executor. |
| `DotNetSolutionSetupToolPlanGuard` | Real .NET-specific guard exists. | Keep .NET knowledge in a driver/domain implementation. Generic runtime sees typed plan/guard result only. |
| `ParentSubprocessArtifactBridge` | Subprocess bridge exists but is module integration. | Extract typed child state resolver and ledger-first artifact bridge contract. |
| `ProcessRuntimeToolPreflightService` | Name-level/runtime preflight exists. | Extend through typed plan expectations and argument-level validation behind generic contracts. |

## Generic Runtime/MAF Domain Leak Inventory

| Location | Leak | Required target |
|---|---|---|
| `WorkspaceCommandReceiptWriter.IsDotNetRuntimeLifecycleTool` | MAF core receipt writer special-cases `workspace_dotnet_run` and `workspace_dotnet_stop`. | Replace with registered lifecycle fact extractor/classifier. .NET implementation lives in a tool/domain package or process driver registration. |
| `AgentFrameworkProcessExecutionAdapter` constructor | Direct optional dependency on `IDotNetSolutionSetupRuntimeExecutor`. | Depend on generic `IProcessRuntimeOwnedStepExecutor` or driver-owned executor catalog selected by assignment/driver metadata. |
| `AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs` | Adapter owns .NET setup flow. | Move flow into .NET/software-delivery process driver implementation. |
| `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs` | Adapter recognizes `workspace_dotnet_new`, `workspace_pwsh_run_script`, and .NET template requirements. | Convert to typed receipt expectations and domain classifier policies. Generic matcher compares typed expectations to receipts. |
| `AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs` | Adapter classifies .NET build/test/new/Pwsh receipts for evidence. | Move tool-family classification behind receipt/evidence classifier seam. |

## Current Project Boundary Facts

From CodeAnalytics snapshot:

- `CanDoItAll.Modules.Processes` references `AgentFramework.Core`, `Processes.Builder`, `Processes.Drivers.Abstractions`, `Processes.Drivers.Standard`, `Processes.Runtime`, and `Processes.Templates`.
- `CanDoItAll.Processes.Runtime` references `Processes.Builder`, `Processes.Contracts`, and `Processes.Drivers.Abstractions`.
- `CanDoItAll.Processes.Drivers.Standard` references only `Processes.Drivers.Abstractions`.
- `CanDoItAll.Processes.Drivers.Abstractions` references `Processes.Contracts`.
- No scoped project cycle was detected.

## Inventory Decision

The refactor should not start by adding a new project. First, define narrow contracts in existing appropriate projects:

- Cross-layer stable records: `CanDoItAll.Processes.Contracts`.
- Driver extension interfaces: `CanDoItAll.Processes.Drivers.Abstractions`.
- Generic runtime policies/evaluators: `CanDoItAll.Processes.Runtime` if they do not depend on MAF types.
- MAF integration adapters and concrete bridge/wiring: `CanDoItAll.Modules.Processes`.
- .NET/software-delivery implementations: initially `CanDoItAll.Modules.Processes` or a driver implementation project, but exposed only through driver contracts.

Only introduce a new project if SB02 proves that existing project references cannot express the boundary without a cycle.

