# C# Boundary Map

## Target Ownership

| Boundary | Owns | Must not own |
|---|---|---|
| `CanDoItAll.Processes.Contracts` | Stable DTOs/records/enums shared across runtime, templates, projections, modules, and drivers. Examples: receipt expectation records, completion issue codes, route result records if needed cross-project. | Concrete MAF types, file system access, driver implementations, .NET setup logic. |
| `CanDoItAll.Processes.Drivers.Abstractions` | Driver extension interfaces and descriptors. Examples: domain receipt classifier, lifecycle fact extractor, runtime-owned step executor contract, recovery advice provider contract, tool-plan policy contract. | Concrete .NET implementation, module DI, runtime engine internals. |
| `CanDoItAll.Processes.Runtime` | Generic runtime state transitions, recovery classification, branch signal application, generic completion route application, generic receipt matching if decoupled from MAF models, retry budgets. | MAF workspace execution, .NET/Blazor/Tetris/Calculator logic, concrete tool command builders. |
| `CanDoItAll.Processes.Templates` | Template schema, template loading, typed execution contracts, branch/receipt/plan metadata validation. | Runtime execution decisions, MAF adapter implementation. |
| `CanDoItAll.Processes.Builder` | Process definition building and typed template-to-definition translation. | Runtime execution, domain-specific tool execution. |
| `CanDoItAll.Modules.Processes` | Composition root for processes module, MAF adapter shell, concrete integration services, EF-backed stores, module UI services. | Generic process contracts used by runtime or drivers, hardcoded generic runtime domain policy. |
| `CanDoItAll.Processes.Drivers.Standard` | Generic standard driver descriptors/factories and non-domain standard driver components. | .NET/software-delivery-specific behavior unless the driver is explicitly a standard .NET driver and remains behind driver abstractions. |
| `CanDoItAll.AgentFramework.Core` | Generic agent framework, workspace command execution, tool protocol/catalog behavior, generic receipt writing. | Process-domain or .NET lifecycle policy. It can emit raw receipt facts, but domain classification must be pluggable. |

## Proposed New/Refactored Top-Level Types

Names are target guidance. Implementers may adjust names if the responsibility remains explicit and testable.

| Type | Project | Responsibility |
|---|---|---|
| `IProcessCompletionGateEvaluator` | `Processes.Drivers.Abstractions` or `Processes.Runtime` | Evaluate completion gates and aggregate issues without adapter state. |
| `ProcessCompletionGatePipeline` | `Processes.Runtime` or `Modules.Processes` during transition | Ordered gate pipeline. |
| `IProcessCompletionGate` | Same as evaluator | One gate per responsibility: grounded refs, managed artifact, receipt, product path, content/readback, blocker contradiction, acceptance criteria. |
| `IRequiredToolReceiptMatcher` | `Processes.Drivers.Abstractions` or `Processes.Runtime` | Match typed receipt expectations to observed receipts. |
| `IProcessReceiptExpectationResolver` | `Processes.Templates`/`Runtime` boundary | Resolve legacy and structured receipt metadata into typed expectations with branch applicability. |
| `IProcessCompletionIssueRouter` | `Processes.Runtime` | Convert safe branch-routable issues into route results using metadata. |
| `IManagedOutcomeArtifactMaterializer` | `Modules.Processes` initially | Materialize and accept MAF-managed outcome artifacts. |
| `IProcessOutcomeResultConverter` | `Modules.Processes` | Convert accepted outcome plus diagnostics/evidence to `ProcessExecutionAdapterResult`. |
| `ISubprocessRunStateResolver` | `Processes.Runtime` or `Modules.Processes` depending on store dependencies | Produce typed child run state including blocked child diagnostics. |
| `IParentSubprocessArtifactBridge` | Existing module contract, refined | Ledger-first parent bridge using typed child state. |
| `IProcessRecoveryClassifier` | Existing runtime concept, refined | Decide retry/route/manager using diagnostic metadata, attempt budget, and fingerprints. |
| `IProcessStepRecoveryInstructionBuilder` | Existing application/module concept, refined | Build diagnostic-specific repair packets. Domain-specific advice comes from providers. |
| `IProcessRuntimeOwnedStepExecutor` | `Processes.Drivers.Abstractions` | Generic contract for deterministic runtime-owned step execution. |
| `DotNetSolutionSetupRuntimeOwnedStepExecutor` | Domain implementation project/module | .NET setup executor implementation behind driver contract. |
| `IToolReceiptLifecycleFactExtractor` | `AgentFramework.Core` abstraction or tool abstraction project | Extract generic lifecycle facts from command receipts without hardcoding process-domain behavior. |
| `DotNetRuntimeLifecycleFactExtractor` | Tool/domain implementation | Extract startup receipt and loopback URL facts for dotnet run/stop receipts. |

## Adapter Target Role

`AgentFrameworkProcessExecutionAdapter` should keep only:

- Load assignment.
- Delegate subprocess pre-execution decision.
- Delegate runtime-owned driver execution decision.
- Invoke MAF agent execution when appropriate.
- Delegate output validation/materialization/gates/result conversion.
- Return `ProcessExecutionAdapterResult`.

It must not own:

- Receipt matching algorithms.
- Product/domain receipt names.
- .NET setup logic.
- Branch-specific completion routes.
- Managed artifact content generation beyond delegating to a materializer.
- Child run root-cause resolution.
- Recovery packet text construction.

## Domain Driver Ownership

Domain drivers may own:

- .NET tool-plan definitions.
- .NET lifecycle fact extraction.
- Software-delivery branch route metadata.
- Template fragments and typed plan metadata.
- Domain recovery advice.
- Acceptance criteria extraction for .NET apps.

Domain drivers must expose:

- Typed contracts.
- Registration through driver package/catalog.
- Tests proving the generic runtime can consume the driver without referencing the concrete implementation.

