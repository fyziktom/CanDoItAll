# Current State

## Accounting Pipeline

- `src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs` defines `AgentRuntimeResponse` with input tokens, output tokens, tool calls, session state, and approvals. It does not currently expose cached input tokens.
- `src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs` already defines `AgentRunMetric.CachedInputTokens` and `CostUsd`, so the persistent metric model can store cached-token usage without a schema-shaped model change.
- `src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` already prices uncached input, cached input, and output tokens separately. The pricing gap is upstream data propagation, not the core calculator.
- A local probe against `Microsoft.Agents.AI 1.8.0` and `Microsoft.Extensions.AI.Abstractions 10.5.1` confirmed `Microsoft.Extensions.AI.UsageDetails` includes `CachedInputTokenCount` alongside `InputTokenCount` and `OutputTokenCount`.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` maps `InputTokenCount` and `OutputTokenCount` into `AgentRuntimeResponse`, but currently drops `CachedInputTokenCount`.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs` aggregates auto-approved response usage as input/output/tool-call totals only.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` persists run metrics. The normal successful-run path adds the local user prompt estimate to provider-reported input tokens, which can distort statistics and prices. Failure paths use local estimates when provider usage is unavailable.

## Process Cost And Graph Pipeline

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs` sums known `AgentRunMetric` costs with `ProviderPricingCalculator.TryResolveMetricCost` and writes rounded process actual cost. This should become correct once metrics contain accurate provider usage.
- `src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` builds live process stats, history metric points, tool usage, and analytics totals from process executions and persisted execution-run details.
- Live process stats currently aggregate input and output tokens only. Cached input tokens are not surfaced in the live statistics pipeline.
- `src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` already renders chart series for context, duration/tool calls, money, and tool usage. It is the reference UI for process and run graph tabs.

## UI Surface

- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` and related partials own selected process details and selected run details.
- Existing process workspace tabs should be extended instead of adding a parallel page or standalone dashboard.
- The requested all-runs graph view must avoid eager historical loading; the tab can render controls immediately, but the data query should not run until the user explicitly clicks the all-runs graph button.
