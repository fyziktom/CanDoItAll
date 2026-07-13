# Scope Inventory

## Branch Hotspots

| Area | Source | Current observation | Planned owner |
| --- | --- | --- | --- |
| Runtime integration mega-file | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` | 7,115 lines; owns multiple unrelated classes and private helper clusters. | SB01-SB04, SB06 |
| AgentFramework adapter tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs` | 3,600+ added lines; behavior concentrated around monolithic adapter API. | SB02, SB04, SB07 |
| Prompt tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` | Already covers generic non-software prompts and AgentFramework prompt content. | SB03, SB07 |
| Launch application service | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | Creates assignments, enriches launch variables, invokes step brief builder, initializes runtime state. | SB01, SB03 |
| Dispatch application service | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` | Main dispatch loop plus claim lifecycle, timeout, retry, branch routing, stale duplicated branch methods. | SB06 |
| Branch signal service | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeBranchSignalApplicationService.cs` | Extracted branch skip/unblock propagation service. | SB06 |
| Driver abstractions | `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs` and `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs` | Existing adapter and strategy seam. | SB01 |
| Driver package/facets | `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs` | Existing driver package supports strategy factories, recovery providers, resupply providers, manager facets, and template fragments; should be extended or wrapped for prompt/evidence/step-dispatch ports. | SB01 |
| Standard driver descriptors | `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs` | Current workflow adapter descriptor and strategy descriptor. | SB01 |
| Strategy dispatcher | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs` | Correct generic binding validation and strategy invocation seam, but not enough for driver-owned prompt/evidence/step dispatch policy. | SB01, SB06 |
| Workbench launch variables | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs` | .NET/software-delivery launch enrichment, script generation, completion policies in one contributor. | SB05 |
| Workbench process nodes | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs` | Project-structure process start and subprocess launch orchestration. | SB05 |
| Runtime launch variables | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs` | Carries product completion, parent/child, step kind, and subprocess definition variables. | SB04, SB05 |
| Process templates | `repo://Templates/Processes` | Software-delivery, .NET setup, runtime command, screenshot writeback templates carry domain behavior. | SB05, SB07 |

## Responsibility Extraction Map

| Responsibility | Current location | Target shape |
| --- | --- | --- |
| Process driver catalog and strategy resolver | `ProcessRuntimeIntegrationServices.cs` | Dedicated driver registration/resolution classes. Generic Processes define ports; MAF-owned driver implementation registers from below. |
| Driver-owned step execution dispatch | `ProcessRuntimeDispatchApplicationService.cs`, `ProcessStrategyDispatcher.cs`, and `AgentFrameworkProcessExecutionAdapter` | Generic dispatcher owns claims/scheduling/lifecycle; selected driver owns provider/tool execution, prompt, evidence, recovery policy, and result conversion. |
| Agent role/executor resolution | `ProcessRuntimeIntegrationServices.cs` | Focused resolver services with direct readiness tests. |
| Prompt fragment assembly | `AgentFrameworkProcessStepBriefBuilder` in `ProcessRuntimeIntegrationServices.cs` | Driver-owned prompt composition port with generic fallback and MAF/AgentFramework implementation below Processes boundary. |
| Agent invocation and structured result parsing | `AgentFrameworkProcessExecutionAdapter` | MAF-owned process driver orchestration plus injectable services for invocation, validation, and conversion. |
| Subprocess launch/reuse/defer/complete | `AgentFrameworkProcessExecutionAdapter` and Workbench coordinator | Focused subprocess lifecycle service plus Workbench coordinator implementation. |
| Product completion policy | `AgentFrameworkProcessExecutionAdapter` private methods | Driver-owned typed policy service for required paths, receipts, file content checks, product root inspection, and mutation proof. |
| Managed artifact materialization | `AgentFrameworkProcessExecutionAdapter` private methods | Driver-owned materializer service with file service dependency. |
| Grounded reference validation | `AgentFrameworkProcessExecutionAdapter` private methods | Driver-owned sanitizer/grounding service with tests. |
| Branch signal routing | Dispatcher plus branch signal service | Single injected branch signal service; no duplicate private implementation in dispatcher. |
| Claim recovery and cancellation | `ProcessRuntimeIntegrationServices.cs` | Separate recovery/cancellation services and hosted worker files. |

## Test Scope

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`
