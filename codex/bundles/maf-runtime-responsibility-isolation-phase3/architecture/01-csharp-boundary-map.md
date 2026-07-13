# C# Boundary Map

## Target Projects

| Project | Role in this phase |
| --- | --- |
| `CanDoItAll.AgentFramework.Maf` | Owns MAF runtime orchestration, Microsoft Agent Framework adapters, capability composition implementation, provider/runtime drivers, and workspace tool implementation until proven otherwise. |
| `CanDoItAll.AgentFramework.Core` | Remains public runtime contracts and agent/domain models owner. Do not move MAF-specific implementation into Core. |
| `CanDoItAll.AgentFramework.Models` | Remains model/record owner for existing agent/runtime records. New implementation-only records should stay in MAF unless they become public contracts. |
| `CanDoItAll.AgentFramework.Tooling` and tools projects | Remain tool abstractions/implementations. Workspace tool-family extraction should not move unrelated runtime logic there unless SB07 proves extension lifecycle requires it. |
| `CanDoItAll.AgentFramework.Capabilities.Abstractions` and Access | Remain capability contract/access-policy owners. New MAF-specific implementation classes should not be placed there. |

## Target Top-Level Types

| Type | Responsibility |
| --- | --- |
| `IMafRuntimeTurnCoordinator` / `MafRuntimeTurnCoordinator` | Normalize run inputs/options, prepare attachments, build runtime, create prompt/approval messages, call executor, attach input usage. |
| `IMafRuntimeTurnExecutor` / `MafRuntimeTurnExecutor` | Provider streaming loop, tool-call progress, repeated-tool guard, background continuation decision, response assembly. |
| `IMafFinalizerRepairCoordinator` / `MafFinalizerRepairCoordinator` | Missing required finalizer repair, typed JSON fallback, provider-failure finalizer preservation. |
| `IMafRuntimeSessionPersistenceDriver` / `MafRuntimeSessionPersistenceDriver` | Session serialization, bounded timeout, scrub request-scoped payloads, pending approval persistence decision. |
| `IMafApprovalContinuationDriver` / `MafApprovalContinuationDriver` | Cache, map, rehydrate, and build approval continuation messages. |
| `IMafRuntimeBuildCoordinator` / `MafRuntimeBuildCoordinator` | Orchestrate runtime build from agent/provider/capabilities/memory/options. |
| `IMafHandoffRuntimeBuilder` / `MafHandoffRuntimeBuilder` | Build handoff runtime agents and validate handoff metadata. |
| `IMafToolPolicyInstrumentor` / `MafToolPolicyInstrumentor` | Wrap tools, ownership metadata, trace recorder, finalizer tool capture. |
| `IMafScriptPolicyInspectionService` / `MafScriptPolicyInspectionService` | Inspect script content and side-effect policy with explicit file/path dependencies. |
| `IRuntimeCapabilityAccessPlanner` / `RuntimeCapabilityAccessPlanner` | Build capability access plan and policies. |
| `IRuntimeCapabilityDescriptorCatalog` / `RuntimeCapabilityDescriptorCatalog` | Map catalog/configured runtime capabilities to descriptors. |
| `IRuntimeCapabilityAttachmentOrchestrator` / `RuntimeCapabilityAttachmentOrchestrator` | Attach workspace, storage, context, skills, runtime providers, A2A, catalog capabilities, and compaction through smaller contributors. |
| `IWorkspaceToolAccessPolicyService` / `WorkspaceToolAccessPolicyService` | Normalize workspace paths, external aliases, delete protections, current-run artifact recovery policy. |
| `WorkspaceFileToolSet`, `WorkspaceCommandToolSet`, `WorkspaceScriptToolSet`, `WorkspaceArtifactToolSet`, `WorkspaceImageAnalysisToolSet` | Cohesive workspace tool families exposed through plugin/catalog registration. |

## Contracts Vs Implementations

- Interfaces are justified only where DI, tests, or runtime selection need them.
- Internal sealed classes are preferred for concrete owners.
- Request/result records should be strongly typed and internal unless they already represent public agent runtime contracts.
- Public `IAgentRuntime` remains unchanged unless a subbundle proves a compatibility-safe extension is needed.

## Composition Root Responsibilities

- Register extracted runtime collaborators in `AddMafRuntimeArchitectureServices`.
- Keep direct `new MafAgentRuntime(...)` at module/hosting boundaries only until SB07 introduces a narrow runtime factory if needed.
- Do not let `MafAgentRuntime` internally discover major collaborators through `IServiceProvider` once they are registered.

## Old Class Responsibilities

| Old type | Keep | Remove |
| --- | --- | --- |
| `MafAgentRuntime` | Public `IAgentRuntime` method signatures and simple delegation. | Turn loop, finalizer repair, session persistence, approval cache mapping, direct composer/factory construction. |
| `MafRuntimeAgentFactory` | Thin hosted-agent compatibility facade if still useful. | Handoff internals, script policy, instrumentation, finalizer tool factory, broad build orchestration. |
| `RuntimeCapabilityComposer` | Thin compatibility facade if needed during migration. | Partial-class access/descriptor/attachment responsibilities. |
| `WorkspaceRuntimePlugin` | Optional adapter that exposes tool methods while delegating to tool sets during migration. | Direct ownership of every tool family and policy helper. |

## Temporary Bridges And Removal Plan

- Temporary facades may be kept for source compatibility during a subbundle, but each subbundle must state which old methods delegate and which methods are deleted.
- A bridge is invalid if it keeps duplicate logic or if tests only exercise the bridge.
- Final closure must either remove bridges or record exact remaining bridge responsibilities with a follow-up bundle.
