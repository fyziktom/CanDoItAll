# Current State

## Branch And Diff Scope

- Branch: `processes-refactor-2`.
- HEAD: `6775de820 phase1`.
- Baseline: `development`.
- Diff size: 124 files changed, 17,083 insertions, 1,467 deletions.
- Primary hotspot: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` is 7,115 lines and gained 4,197 insertions.
- Primary test hotspot: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs` gained 3,621 insertions.

## Existing Useful Boundaries

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs` already defines `IProcessExecutionAdapter`, adapter descriptors, adapter requests, adapter results, diagnostics, and adapter kinds.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs` already defines strategy factories, strategy execution context, result envelopes, diagnostics, manager signals, and outcomes.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs` already supports driver package facets, recovery providers, resupply providers, manager facets, and template fragment providers. This is the right place to extend or wrap driver-owned prompt/evidence/step-dispatch behavior.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs` defines the current workflow adapter descriptor and strategy descriptor.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs` already performs generic immutable plan/strategy binding validation and invokes `IProcessStrategyFactory`. This is a useful generic dispatch hook, but it is too narrow for prompt/evidence/driver recovery policy.
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` already separates `GenericProcessStepBriefBuilder` from AgentFramework-specific prompt behavior.
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs` already uses an `IProjectStructureProcessLaunchVariableContributor` extension point, but the current .NET contributor is still highly domain-specific and large.
- Static preparation scans found no project-reference dependency from `src/Processes/*` to MAF or AgentFramework projects. This is a hard invariant to preserve.

## Responsibility Mixing Findings

| Finding | Source | Impact |
| --- | --- | --- |
| One integration file owns launch driver catalog resolution, executor resolution, step assignment repair, AgentFramework prompt text, adapter execution, subprocess coordination, managed artifact materialization, product completion validation, tool receipt parsing, path grounding, execution observation readers, telemetry readers, claim recovery, recovery observers, cancellation observers, and background recovery workers. | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` | Hard to test in isolation, hard to replace per driver/model, and risky to evolve beyond app-building flows. |
| AgentFramework prompt fragments are embedded as string-building methods in the module-specific brief builder. | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`; `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` | Prompt changes cannot be mocked or varied per model/provider without editing one large class. |
| Adapter execution mixes subprocess launch/reuse, agent invocation, structured output validation, managed artifact recovery, product mutation proof, receipt history loading, grounding, transient retry classification, manager signal creation, and result conversion. | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` | Behavior may work, but implementation is too broad to maintain and makes failure classification fragile. |
| Completion-evidence policy and prompt composition are not driver-owned yet. | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` | Swapping execution models or providers requires editing the integration file instead of replacing a driver strategy. |
| Runtime step execution dispatch is only partially driver-shaped. | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs`; `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` | Generic scheduling and claims are properly generic, but driver-specific step execution policy still lives in the AgentFramework adapter and module integration file. |
| Software-delivery and .NET completion requirements flow through generic launch variables and adapter validation. | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`; `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`; `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs` | Generic enterprise processes risk inheriting software-specific assumptions. |
| `ProcessRuntimeDispatchApplicationService` constructs `ProcessRuntimeBranchSignalApplicationService` directly and still contains old private branch-signal methods that are not referenced by the main dispatch flow. | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`; `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeBranchSignalApplicationService.cs` | Dispatcher responsibilities are blurred and stale duplicated code can drift from the extracted branch router. |
| Tests are comprehensive but concentrated in large adapter tests and prompt tests. | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`; `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` | Refactoring needs test migration, not just production file movement, or the next changes will be unsafe. |

## Domain Flexibility Gaps

- Current generic prompt tests explicitly cover non-software scenarios such as business market sizing, claims quality review, data analysis, and campaign planning, but the concrete registered builder is the AgentFramework-specific builder in `ProcessesModuleServiceCollectionExtensions`.
- The .NET launch contributor contains software-delivery process keys, .NET template rules, script generation, product completion maps, runtime command writeback rules, and screenshot writeback rules in one partial class.
- Product completion validation uses runtime launch variables to carry path, receipt, and file-content rules. That is useful, but the parser and enforcement should be a replaceable completion policy, not private methods on the AgentFramework adapter.

## Preparation Decision

- This is an `initiative` bundle because the work spans runtime, application services, driver abstractions, modules, Workbench project-structure integration, templates, and tests.
- Implementation should start with driver port boundaries and dependency-direction proof, then refactor behavior behind driver-owned seams, then split tests and prove both software-delivery and non-software process flows.
