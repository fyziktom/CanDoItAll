# Current State

## Summary

- CanDoItAll already has a layered agent framework: Models define agents/providers/capabilities, Core owns catalog/execution contracts, Maf adapts MAF runtime, Hosting wires services, Modules.AgentFramework exposes UI/module features, and Modules.Processes drives governed process automation.
- Current MAF references are `1.0.0` for `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows`. Official NuGet reports `1.3.0` for these packages as of 2026-05-02.
- A2A is not currently a first-class CanDoItAll concept. `AgentPermissionsPolicy.CanAskOtherAgents` exists, but there is no corresponding A2A registry, remote agent wrapper, hosted endpoint, or handoff runtime contract in `IAgentRuntime` or `IAgentFrameworkWorkspaceService`.
- MAF 1.3 A2A docs and local clone show two useful paths: direct A2A agents via `A2ACardResolver.GetAIAgentAsync()`/`AgentCard.AsAIAgent()`, and hosting through `builder.AddA2AServer(...)`, `MapA2AHttpJson(...)`, `MapA2AJsonRpc(...)`, and `MapWellKnownAgentCard(...)`.
- MAF workflow docs/samples show handoff orchestration through `AgentWorkflowBuilder.CreateHandoffBuilderWith(...)`, `.WithHandoffs(...)`, `.EnableReturnToPrevious()`, and `Workflow.AsAIAgent(...)`.
- The default OpenAI model is centralized mostly through `ManagedSeedProviderFallbacks.OpenAiDefaultModel`, currently `gpt-5-mini`, but `OpenAiProviderAdapter.DefaultModel` and several tests/UI smoke paths still contain direct `gpt-5-mini` literals.
- Process automation already has strong prompt rules, finalizer enforcement, artifact expectation validation, and integration tests for three-agent artifact handoff. The gap is runtime cooperation and role/tool/context policy, not absence of artifact records.
- MAF runtime compaction is currently skipped for governed process automation and auto-approved non-interactive execution. Interactive compaction defaults are `SlidingWindowTurns = 8`, `TruncationTokenLimit = 12000`, and `ToolCompactionMessageThreshold = 10`.
- Workspace build/test/run tools are only attached when `AgentWorkspaceToolAccessSettings.CanWriteFiles` is true. Read/list/search/stat tools require read/write or grounded external aliases. This is safe, but role seeds must grant the correct profile to dev/QA agents.

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260502224603-ca161729`.
- Scoped projects: `CanDoItAll.AgentFramework.Core`, `CanDoItAll.AgentFramework.Hosting`, `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Models`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Processes`.
- Important dependencies: Core depends on Models; Maf depends on Core, Models, and Modules.Processes; Hosting depends on Core and Maf; Modules.AgentFramework depends on Core, Hosting, Maf, Models, and Processes.
- Architecture warning: snapshot found existing cycles in Modules.AgentFramework hosting/root module and some runtime/process type groups. Do not expand those cycles while adding A2A/handoff.
