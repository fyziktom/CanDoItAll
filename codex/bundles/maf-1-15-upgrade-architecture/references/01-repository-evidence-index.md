# Repository Evidence Index

Pinned branch head: `59f558bc866d39d438b53f5f743dd5e87c2a6253`

Confidence labels:

- `Confirmed` — directly inspected.
- `Inference` — conclusion depends on an implementation not fully inspected.
- `Discovery required` — mandatory local grep/build/test.

| Area | Repository path / symbol | Evidence | Finding | Confidence |
|---|---|---|---|---|
| MAF package train | `src/MAF/MicrosoftAgentFramework.Packages.props` | component-owned stable and preview properties | Three direct MAF package owners import one release train without repository-wide CPM | Confirmed |
| Main packages | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`, package references | stable 1.13; A2A preview; MEAI 10.8 | Mixed stable/preview package train repeated as literals | Confirmed |
| Workflow packages | `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | stable core/workflows 1.13 | Must align through shared stable property | Confirmed |
| Hosting packages | `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | Hosting.A2A 1.13 preview | Must align to matching 1.15 preview | Confirmed |
| Host composition | `AgentFrameworkServiceCollectionExtensions.AddAgentFrameworkCore` | workspace services, preparation cache, checkpoint bridge, A2A, runtime | A2A active; custom file services canonical; runtime facade singleton | Confirmed |
| Runtime lifecycle | `MafAgentRuntime` | runtime build creation/use/disposal | mutable runtime graph is per execution | Confirmed |
| Agent options | `MafRuntimeAgentFactory` | `ChatClientAgentOptions` construction | no inspected explicit opt-out from default middleware | Confirmed for construction; provider-factory behavior is inference |
| Custom tool policy | `MafRuntimeAgentFactory` middleware | tool classification, external targets, scripts, audit | application governance remains required | Confirmed |
| Handoff creation | `MafHandoffWorkflowFactory.Build` | handoff builder and `AsAIAgent(includeWorkflowOutputsInResponse: true)` | workflow-hosted agent uses explicit outputs | Confirmed |
| Handoff wrapper | `HandoffDepthGuardAgent.RunCoreAsync` | streaming collection + `ToAgentResponse()` | bypasses inner non-streaming terminal projection | Confirmed |
| Main output merge | `MafAgentRuntime` and `MafRuntimeResponseAssembler` | streaming updates merged by MEAI | full runtime requires separate terminal-output validation | Confirmed |
| Pending approval cache | `MafApprovalContinuationDriver.pendingApprovals` | process-local concurrent dictionary | cache is not restart authority | Confirmed |
| Approval rehydration | `MafApprovalContinuationDriver.RehydratePendingApprovals` | reconstructs function/MCP request in the pre-upgrade path | native 1.15 serialized binding state must be authoritative; legacy reconstruction must be rejected | Confirmed |
| Approval decisions | `CreateApprovalInputMessages(..., bool approved)` | same bool used for the complete pending set | bind atomically to the exact current server-held snapshot and reject snapshot changes | Confirmed |
| Approval ID fallback | pending record mapping | request/call ID fallback to GUID | unsafe under exact binding; remove | Confirmed |
| Session restore | `MafRuntimeSessionBuilder.BuildSessionAsync` | deserialize opaque MAF state; governed isolation | cross-version fixture required | Confirmed |
| History mode | `MafRuntimeSessionBuilder` | provider/framework history selection and conversation ID detection | application policy remains | Confirmed |
| Approval replay hook | `ShouldReplayTranscriptAfterApproval` | always false | dead hook candidate | Confirmed |
| Session persistence | `MafRuntimeSessionPersistenceDriver.TrySerializeSessionAsync` | five-second bound; catch-all null | retain bound, improve diagnostics | Confirmed |
| Attachment scrub | `RequestScopedSessionContentScrubber` call | scrub after serialize | must preserve new state-bag binding data | Confirmed call; implementation discovery required |
| Usage/finalizer | `MafRuntimeResponseAssembler` | usage grouping and required finalizer | keep governance, test merge semantics | Confirmed |
| Workspace resolution | `MafRuntimeDependencyResolver.ResolveWorkspaceServices` | custom workspace services and fallbacks | not Harness FileAccess | Confirmed |
| FileTools integration | `src/Integration/CanDoItAll.FileTools.Integration/*.csproj` | CanDoItAll FileTools packages, no MAF Harness ref | no confirmed direct Harness impact | Confirmed |
| Preparation architecture | `.codex/bundles/agent-preload-activity-stream-architecture/architecture/01-target-solution.md` | forbids pooling live agents/tools/sessions/approvals | upgrade must preserve immutable blueprint boundary | Confirmed |
| Provider factory | implementations of `IMafProviderAgentFactory` | not all paths directly inspected | must prove effective default middleware | Discovery required |
| Snapshotter | `MafAgentResponseSnapshotter` | symbol used from runtime | transformations may affect ordering/IDs | Discovery required |
| Streaming runner | provider streaming runner | invoked from runtime | must prove active `AgentRunContext.Session` | Discovery required |
| Checkpoint bridge | `WorkflowBackedAgentExecutionCheckpointBridge` | registered in host | native MAF checkpoint relevance unknown | Discovery required |
| Optional APIs | Harness/AGUI/declarative/compaction/etc. | no confirmed targeted-path usage | full grep required | Discovery required |

## Source URLs

Use branch-pinned URLs while implementing, for example:

- `https://github.com/fyziktom/CanDoItAll/blob/agents-loading-refactor/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `https://github.com/fyziktom/CanDoItAll/blob/agents-loading-refactor/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `https://github.com/fyziktom/CanDoItAll/blob/agents-loading-refactor/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafApprovalContinuationDriver.cs`
- `https://github.com/fyziktom/CanDoItAll/blob/agents-loading-refactor/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `https://github.com/fyziktom/CanDoItAll/blob/agents-loading-refactor/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionPersistenceDriver.cs`
- `https://github.com/fyziktom/CanDoItAll/blob/agents-loading-refactor/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafHandoffWorkflowFactory.cs`

Codex must replace branch URLs with commit-pinned links in the final execution report.
