# C# Pattern Selection Records

## PSR-1: Adapter Shell Plus Extracted Application Services

### Context

`AgentFrameworkProcessExecutionAdapter` is a large partial-class cluster. Many responsibilities are hidden behind private methods and shared state.

### Forces

- Extension growth: high.
- Multiple implementations: medium for runtime-owned execution and driver policies.
- Construction complexity: medium.
- External SDK isolation: MAF execution service must remain in module integration.
- Runtime selection: medium.
- Testability: high.
- Dependency direction: must prevent runtime knowing concrete module/domain implementation.

### Selected Pattern

Facade, but only as a thin compatibility shell around top-level services.

### Rejected Alternatives

- Simpler class: insufficient because there are multiple independent responsibilities.
- Partial class: explicitly rejected by policy.
- Switch statement: would keep domain decisions in the adapter.
- Service locator: rejected.
- Direct construction: rejected except for simple immutable value objects.

### New Types And Projects

| Type | Project | Responsibility |
|---|---|---|
| `AgentFrameworkProcessExecutionAdapter` | `Modules.Processes` | Thin orchestration facade only. |
| `IProcessAgentRunInvoker` | `Modules.Processes` | MAF execution-run invocation and detail loading. |
| `IProcessOutcomeResultConverter` | `Modules.Processes` | Convert final accepted outcome to adapter result. |
| `IManagedOutcomeArtifactMaterializer` | `Modules.Processes` | Managed artifact write/append/readback behavior. |

### Test Plan

| Test | Behavior proven |
|---|---|
| Adapter delegates completion evaluation to pipeline | Adapter no longer owns gate logic. |
| Result converter unit test without adapter | Result conversion is independently testable. |
| Materializer unit test with fake workspace files | Artifact behavior does not require MAF execution. |

### Proof This Is Not Fake Separation

Implementation must delete or shrink the corresponding adapter partial methods and add source assertions that moved behavior is no longer present in adapter files.

## PSR-2: Chain Of Responsibility For Completion Gates

### Context

Completion gates need to evaluate multiple concerns and aggregate issues. Earlier behavior could short-circuit and hide root causes.

### Forces

- Extension growth: high.
- Multiple implementations: high.
- Construction complexity: low.
- External SDK isolation: low.
- Runtime selection: low.
- Testability: high.
- Dependency direction: gates must be generic or driver-owned.

### Selected Pattern

Chain of Responsibility for ordered completion gates.

### Rejected Alternatives

- Simpler class: would likely become another monolith.
- Partial class: rejected.
- Switch statement: would centralize issue codes and future domain cases.
- Service locator: rejected.
- Direct construction: acceptable only in composition.

### New Types And Projects

| Type | Project | Responsibility |
|---|---|---|
| `IProcessCompletionGate` | `Processes.Runtime` or `Drivers.Abstractions` | One completion validation responsibility. |
| `ProcessCompletionGatePipeline` | `Processes.Runtime` or `Modules.Processes` transition | Aggregates and orders issues. |
| `RequiredToolReceiptCompletionGate` | Same as pipeline | Required receipt evaluation. |
| `ManagedArtifactCompletionGate` | `Modules.Processes` if MAF-specific | Managed artifact evidence checks. |
| `ProductReadbackCompletionGate` | `Processes.Runtime` if generic file abstraction exists | Product path/content checks through abstractions. |

### Test Plan

| Test | Behavior proven |
|---|---|
| Empty product output plus missing script receipt returns both issues | No first-issue short-circuit. |
| Branch-inapplicable receipt is skipped | Branch-aware rule application. |
| Duplicate product/process receipt rule is deduped | No duplicate diagnostics. |
| Unsafe issue orders before safe issue | Stable primary issue selection. |

### Proof This Is Not Fake Separation

Each gate must be directly unit-tested without adapter construction. The adapter must depend on the pipeline, not on individual gate helper methods.

## PSR-3: Strategy/Factory For Domain Driver Policies

### Context

.NET runtime-owned setup, .NET lifecycle receipt classification, and software-delivery recovery advice are domain-specific but currently reachable from generic adapter/MAF core code.

### Forces

- Extension growth: high.
- Multiple implementations: high.
- Construction complexity: medium.
- External SDK isolation: medium.
- Runtime selection: high.
- Testability: high.
- Dependency direction: runtime must depend on abstractions, not domain implementation.

### Selected Pattern

Strategy selected through a Factory Method or catalog. The existing process driver catalog should be extended rather than bypassed.

### Rejected Alternatives

- Simpler class: would still leak .NET.
- Partial class: rejected.
- Switch statement on step keys/tool names in adapter: rejected.
- Service locator: rejected.
- Direct construction of .NET executor in adapter: rejected.

### New Types And Projects

| Type | Project | Responsibility |
|---|---|---|
| `IProcessRuntimeOwnedStepExecutor` | `Processes.Drivers.Abstractions` | Optional deterministic execution for a step. |
| `IProcessRuntimeOwnedStepExecutorFactory` or catalog | `Processes.Drivers.Abstractions`/`Runtime` | Select executor from driver metadata. |
| `DotNetSolutionSetupRuntimeOwnedStepExecutor` | Domain driver/module implementation | .NET setup execution. |
| `IProcessToolLifecycleFactExtractor` | Tool/driver abstraction | Extract lifecycle facts from receipts. |
| `DotNetRuntimeLifecycleFactExtractor` | Domain/tool implementation | .NET run/stop startup facts. |

### Test Plan

| Test | Behavior proven |
|---|---|
| Unknown runtime-owned executor returns no match without fallback execution | No silent fallback. |
| DotNet executor is selected by driver metadata | Domain behavior is driver-owned. |
| Adding fake executor does not edit adapter | Extension seam works. |
| Generic receipt writer uses extractor collection | No hardcoded .NET lifecycle method. |

### Proof This Is Not Fake Separation

Source assertions must show generic adapter/runtime/MAF core does not contain `.NET` step/tool-specific lifecycle decisions except allowed tool catalog constants.

## PSR-4: Adapter Pattern For MAF Execution Boundary

### Context

MAF execution models and process runtime contracts must remain separated. The adapter currently interleaves MAF invocation, process gate semantics, and artifact behavior.

### Forces

- Extension growth: medium.
- Multiple implementations: possible.
- Construction complexity: medium.
- External SDK isolation: high.
- Runtime selection: low.
- Testability: high.
- Dependency direction: process runtime should not reference MAF module implementation.

### Selected Pattern

Adapter for MAF execution invocation and receipt loading.

### Rejected Alternatives

- Keep MAF calls in main adapter: preserves monolith.
- Push MAF types into runtime: violates boundary.
- Service locator: rejected.

### New Types And Projects

| Type | Project | Responsibility |
|---|---|---|
| `IProcessAgentRunInvoker` | `Modules.Processes` | Wrap MAF execution run request/detail operations. |
| `ProcessAgentRunInvocationResult` | `Modules.Processes` | Minimal process-facing data needed after MAF execution. |

### Test Plan

| Test | Behavior proven |
|---|---|
| Adapter handles invalid structured output without gate pipeline | Boundary between MAF output and process gates. |
| Gate pipeline receives normalized receipt records only | Gate tests do not need MAF service. |

### Proof This Is Not Fake Separation

Gate and materializer tests use fake invoker output records, not live MAF workspace services.

## PSR-5: Builder For Typed Template/Tool Plan Metadata

### Context

GPTPro found that deterministic tool plans and launch variables are too often prompt-only prose. Templates need typed plan/receipt metadata.

### Forces

- Extension growth: high.
- Multiple implementations: medium.
- Construction complexity: high.
- External SDK isolation: low.
- Runtime selection: medium.
- Testability: high.
- Dependency direction: template schema must feed runtime through contracts.

### Selected Pattern

Builder for typed process execution contracts and tool-plan expectations.

### Rejected Alternatives

- More prompt text: rejected by GPTPro root-cause analysis.
- Ad hoc string parsing in adapter: rejected.
- Direct JSON string inspection in runtime: rejected.

### New Types And Projects

| Type | Project | Responsibility |
|---|---|---|
| `ProcessStepToolPlanBuilder` | `Processes.Builder` or `Processes.Templates` | Build typed tool-plan expectations from template schema. |
| `ProcessStepToolPlan` | `Processes.Contracts` | Stable typed plan contract if shared. |
| `RequiredToolReceiptExpectation` | `Processes.Contracts` | Stable typed receipt expectation. |

### Test Plan

| Test | Behavior proven |
|---|---|
| Template with branch-specific receipts builds typed expectations | No prompt-only receipt semantics. |
| Unresolved tool-critical placeholder fails load/validation | Tool paths are grounded before agent execution. |
| Legacy string receipt rule maps to typed compatibility expectation | Migration does not break old templates. |

### Proof This Is Not Fake Separation

Adapter no longer parses template strings for domain-specific tool plans.

