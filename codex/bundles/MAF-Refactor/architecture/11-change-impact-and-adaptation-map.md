# Change impact and adaptation map

## Purpose

This map identifies the call chains and operational surfaces that must adapt to the target architecture. A compiler-driven rename is not sufficient: several paths own persistence, approval, cleanup, usage, process evidence, or UI lifecycle.

## Impact matrix

| Area | Current entry/owners | Required adaptation | Primary risk | Safe proof |
|---|---|---|---|---|
| Floating chat host | `FloatingAgentChatHost.razor.cs`, `FloatingAgentChatCoordinator.cs`, `AgentChatPanel.razor.cs` | consume per-chat affinity and current/next-turn context state | cross-chat contamination and stale events | multi-window lifecycle/component tests |
| Live UI context | `AgentChatContextRegistry.cs`, surface providers | publish observation only; preserve atomic revisions/navigation fence | UI scope interpreted as authority | forged scope and rapid-navigation tests |
| Turn creation | `AgentChatExecutionOrchestrator.cs`, `AgentChatContextInvocationFactory.cs` | capture immutable observation, resolve authority, then admit/persist | profile changes and premature affinity commit | generation race and failed-admission tests |
| Transient context | `AgentRunTransientContextRegistry.cs`, `AgentRuntimeTransientContext` | become turn-context lease/reference, retain exact original through continuation | restart/unavailable lease and context retargeting | digest/restart/Project X->Y approval tests |
| Execution metadata | `ExecutionInvocationMetadata.cs` | split generic execution projection from process-owned metadata providers | process leaks remain in generic Core | source guards and typed metadata round-trip |
| Execution coordinator | `AgentFrameworkWorkspaceExecutionService*.cs` | depend on narrow execution/continuation ports; preserve orchestration/persistence | usage/finalizer/cancellation/persistence drift | differential fake-port and integration fixtures |
| Workspace construction | `AgentFrameworkWorkspaceFactory.cs`, Hosting/Module DI | one composition root and owned scope-bound service factory | mixed manual+DI graph and lifetime changes | composition/disposal tests |
| Workspace services | file, command, artifact, process host, path, document/image, receipts | all created from one `WorkspaceExecutionScope` | cross-project access or false denial | identity assertion through real tool calls |
| Capability composition | `RuntimeCapabilityComposer.cs`, tool provider composer/builders | typed contributions, no service location, collision fail-fast | order-dependent tool/approval behavior | golden tool manifest and duplicate negative test |
| Runtime tool providers | Workbench, Scheduler, Memory, AgentFramework providers | continue via SDK-neutral descriptors/invokers when planned | SDK leakage and registration drift | provider direct tests + composition smoke |
| MAF execution | `MafAgentRuntime.cs`, factory, update pump, session/finalizer drivers | implement narrow ports and SDK mapping only | streaming/resource/session behavior regression | fault injection and lifecycle tests |
| Provider runtime | runtime pool, dispatch lanes, provider drivers/gateway | become reusable foundation for lightweight LLM calls | duplicate credentials/retry/usage logic | direct driver/pool tests |
| Provider diagnostics | health/test/model administration callers | separate ports from execution | full agent graph still constructed | source/performance assertions |
| Approval | Core run transitions, UI/API, MAF continuation driver | per-proposal decisions and pending-set fingerprint | approve-all, duplicate continuation, wrong run | concurrency and stable-ID tests |
| Runtime state | run/session models, MAF session builder/persistence | versioned adapter envelope + legacy reader | stranded waiting runs or silent replay | persisted fixture migration/restart tests |
| Handoff/A2A | runtime handoff options, hosted agent factory, A2A tool factory | migrate deliberately to hosted/agent ports | broad facade survives through special callers | caller scan and integration tests |
| Process dispatch | Processes integration, generic execution metadata/provider selection | move process policy into Processes-owned strategies | wrong provider/finalizer behavior | process matrix and source guards |
| Process recovery | MAF recovery service, completion coordinator/gates | Processes-owned recovery using ordinary completion path | stale artifact or gate bypass | current-run trace and exact-gate tests |
| Workflow LLM | `MafWorkflowLlmComponentInvoker.cs` | use lightweight LLM port | hidden agent context/tools and payload authority | parity + no-agent source tests |
| Future ordinary LLM chat | new application conversation boundary | transcript above stateless LLM port | rebuilding an agent with disabled tools | contract/unit tests and dependency guard |
| Public APIs | agent API projections and API integration test hosts | preserve opaque public IDs; hide envelopes/authority/attachments | sensitive/runtime state disclosure | public projection integration tests |
| Blazor completion refresh | context notification hub/providers | refresh originating source without changing run context | stale refresh or update loop | component/source correlation tests |
| Database profile switch | runtime accessor/generation, observation attachments | invalidate observation/authority and prevent cross-profile continuation | old profile services/data reused | profile-switch concurrency tests |
| Mocks/harnesses | `ProcessMockAgentRuntime`, `ScenarioHarnessAgentRuntime`, API host | implement only owned ports/decorators | mock becomes broad runtime V2 | architecture and isolated tests |
| Observability | activity/logs/receipts | correlate stage identities without raw sensitive payloads | impossible diagnosis or data leak | telemetry schema review |

## Broad runtime caller families

Before SB09 closes, enumerate at least:

- `AgentFrameworkWorkspaceExecutionService` and workspace facade callers;
- Hosting and Module service registration;
- `AgentFrameworkWorkspaceFactory` manual construction;
- provider diagnostics and model administration;
- workflow LLM adapter;
- process mock and scenario harness decorators;
- hosted/A2A and handoff paths;
- SchedulerPlanner registration;
- API integration test hosts and composition mirrors;
- unit/integration tests that mirror production composition.

## Cutover principle

Each row above requires:

1. characterization;
2. new owner/contract;
3. compatibility adapter where persisted callers require it;
4. one selected production path;
5. focused and negative tests;
6. telemetry/correlation;
7. rollback action;
8. deletion proof.
