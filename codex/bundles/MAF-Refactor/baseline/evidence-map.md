# Static evidence map

This bundle was prepared from a static GitHub review. CodeAnalytics was not available in the preparation environment. Claude Code must refresh the evidence with CodeAnalytics when available.

| Concern | Current source |
|---|---|
| Live floating context registry | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextRegistry.cs` |
| Turn invocation mapping | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs` |
| Floating send orchestration | `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs` |
| Floating chat host | `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor.cs` |
| Floating chat coordinator | `src/Modules/CanDoItAll.Modules.AgentFramework/Services/FloatingAgentChatCoordinator.cs` |
| Context and active-chat models | `src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/FloatingAgentChatModels.cs` |
| Position/navigation models | `src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/AgentChatPositionModels.cs` |
| Transient context lease | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentRunTransientContextRegistry.cs` |
| Execution metadata | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` |
| Execution coordinator | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` |
| Project Structure context publisher | `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureAgentChatContextProvider.razor` |
| Project Structure context builder | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentChatContextBuilder.cs` |
| Project Structure page/view selection | `src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` |
| Gantt view implementation | `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureGanttPanel.razor.cs` |
| Broad runtime contract | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs` |
| MAF runtime facade | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` |
| MAF runtime build factory | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs` |
| Capability composition | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs` |
| Workspace dependency resolver | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeDependencyResolver.cs` |
| MAF session builder | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs` |
| MAF session persistence | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionPersistenceDriver.cs` |
| Approval continuation | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafApprovalContinuationDriver.cs` |
| Process artifact recovery leak | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/ProcessArtifactRecoveryService.cs` |
| MAF workflow LLM invocation | `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowLlmComponentInvoker.cs` |
| MAF project references | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` |
| Processes integration | `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepExecutor.cs` |
| Floating context tests | `tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs` |
| MAF architecture tests | `tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs` |
| MAF workflow isolation tests | `tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowAdapterIsolationTests.cs` |

## Confirmed strengths

- Module context publication is atomic and revisioned.
- Navigation identity and route matching protect against stale UI position.
- The send path captures a strict snapshot.
- The transient payload is bound to the run by digest.
- The original transient payload is retained through an approval continuation.
- A continuation fails if the original payload lease is unavailable.
- Project Structure already publishes distinct Canvas, Gantt, Calendar, and Manager Summary view labels.
- Tool invocation traces, receipts, finalizers, and usage observations are strongly represented.

## Confirmed architectural gaps

- The live context registry represents only the current application surface; it does not own per-conversation affinity.
- The same transient record carries model content, workspace scope, and typed attachments.
- UI-published scope/access is used too close to execution authority.
- Gantt publishes only coarse view context through the parent; it does not contribute its projection state.
- Project Structure UI fragments mix visible facts with durable tool/protocol instructions.
- `IAgentRuntime` combines execution, continuation, diagnostics, and provider model administration.
- MAF runtime classes retain `IServiceProvider` and create fallback implementations.
- Workspace services can be sourced from one scope while plugins receive another effective scope.
- `CanDoItAll.AgentFramework.Maf` references product modules.
- Process artifact recovery and process status semantics live inside the MAF adapter.
- Workflow LLM nodes construct temporary agents and use the full agent runtime.

## Revision 2 additional evidence targets

- Provider-neutral chat driver contracts: `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`.
- Provider chat request/result protocol: `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderRequestContracts.cs` and driver protocol files.
- Provider runtime pool/handle/dispatch: MAF provider runtime gateway and provider runtime infrastructure.
- Provider health/model administration callers and registrations.
- Broad runtime callers in Core, Hosting, AgentFramework module, SchedulerPlanner, workflow adapter, mocks/harnesses, API test host, and tests.
- Public API projection tests that prevent runtime-state disclosure.
- Fault/lifecycle tests for provider runtime, MAF disposal, process leases, and approval continuation.

Claude Code must refresh this map from the current branch using CodeAnalytics plus direct source search; this static list is not exhaustive.
