# Repository Snapshot and Inspection Scope

## Pinned Snapshot

- Repository: `https://github.com/fyziktom/CanDoItAll`
- Branch: `agents-loading-refactor`
- Inspected head SHA: `59f558bc866d39d438b53f5f743dd5e87c2a6253`
- Snapshot date: `2026-07-27`

## Directly Inspected Integration Files

- `Directory.Build.props`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafApprovalContinuationDriver.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionPersistenceDriver.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeResponseAssembler.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeDependencyResolver.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafHandoffWorkflowFactory.cs`
- `src/Integration/CanDoItAll.FileTools.Integration/CanDoItAll.FileTools.Integration.csproj`
- selected architecture/proof files under `.codex/bundles/agent-preload-activity-stream-architecture`

## Confirmed Current Package Baseline

### Stable MAF packages

- `Microsoft.Agents.AI` — `1.13.0`
- `Microsoft.Agents.AI.OpenAI` — `1.13.0`
- `Microsoft.Agents.AI.Workflows` — `1.13.0`

### Preview MAF packages

- `Microsoft.Agents.AI.A2A` — `1.13.0-preview.260703.1`
- `Microsoft.Agents.AI.Hosting.A2A` — `1.13.0-preview.260703.1`

### Important adjacent direct dependencies

- `Microsoft.Extensions.AI` — `10.8.0`
- `Microsoft.Extensions.AI.OpenAI` — `10.8.0`
- `OpenAI` — `2.12.0`
- `Azure.AI.OpenAI` — `2.9.0-beta.1`
- `ModelContextProtocol` — `1.1.0`
- `OllamaSharp` — `5.4.25`

## Confirmed Architecture Characteristics

- `MafAgentRuntime` is registered as a singleton host service, but it creates and disposes a runtime build per execution.
- Runtime builds own their agent, tools, context providers, MCP/runtime-tool resources, approval state, and disposables.
- Handoff builds recursively create participant runtime builds and dispose them as a group.
- Agent preparation/preload caches immutable definitions/snapshots rather than live agents.
- Workspace file, command, image, document, and artifact services are CanDoItAll services registered in the host.
- A2A hosting is always added by the common host composition.
- Sessions may be framework-managed or provider-managed and may be serialized as opaque MAF state.
- Governed process steps intentionally use isolated sessions unless continuing a pending approval.
- Pending approvals are cached in-process and also mapped to a custom persistent record.
- The main runtime is streaming-first and independently converts collected updates to an `AgentResponse`.

## Inspection Limitation and Required Gate

The analysis used targeted GitHub source inspection at the pinned branch head. A complete local branch grep, restore, build, test run, package graph, and persisted fixture capture were not available during bundle preparation.

Therefore:

- findings are tagged as `Confirmed`, `High-confidence inference`, or `Discovery required`;
- SB01 must run the included discovery script against the actual working tree;
- SB01 must capture 1.13 fixtures before any package edit;
- Codex must update the evidence index for repository drift or newly discovered integration points.
