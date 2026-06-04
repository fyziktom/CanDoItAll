# Source Artifacts

| Artifact | Location | Notes |
| --- | --- | --- |
| Original request | `bundle://inputs/00-original-request.md` | Raw source prompt with architect notes. |
| Current token response contract | `repo://src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs` | `AgentRuntimeResponse` currently carries input/output/tool calls only. |
| Current MAF usage mapping | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | Maps `response.Usage.InputTokenCount` and `OutputTokenCount`, but not `CachedInputTokenCount`. |
| Current metric persistence | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Persists `AgentRunMetric` and currently adds prompt estimates on normal execution. |
| Current pricing model | `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` | Pricing already supports cached input tokens if metrics provide them. |
| Current process cost sync | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs` | Sums execution metric costs into `ProcessRun.ActualCost`. |
| Current live analytics builder | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | Builds live metrics, history metric points, tool usage, and money totals. |
| Current live graph UI | `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` | Existing chart patterns to reuse. |
| Current process workspace UI | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` | Selected process and run detail tabs live here. |
