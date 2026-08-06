# Validation matrix

Test names are proposed behavior names. Claude Code may place them in the most suitable existing test class or create a focused class.

## Floating context and authority

| Scenario | Expected result | Test layer |
|---|---|---|
| Project X Canvas snapshot captured | Context source, view, version, epoch, authority fingerprint recorded | Unit |
| Canvas -> Gantt before next Send | Next turn has `ViewChanged`; same context epoch | Unit/component |
| Canvas run in progress while UI switches to Gantt | Running turn retains Canvas reference and digest | Unit/integration |
| Canvas run waits for approval, UI switches to Gantt | Continuation retains Canvas observation and authority | Unit/integration |
| Project X -> Project Y | New epoch and new canonical authority resolution | Unit/component |
| Project switch with read-only authority | Mutation tools are absent/denied | Integration |
| Forged UI mutation permission | Canonical resolver wins; no elevation | Negative unit |
| Forged/mismatched workspace scope | Admission fails before runtime construction | Negative unit |
| Detached chat | No UI observation, no context-derived workspace authority | Unit/component |
| Navigation only | No provider/runtime execution | Component |
| Gantt projection loading | Turn receives explicit partial/loading observation or a deliberate not-ready result; never stale Canvas facts | Component |
| Gantt projection ready | Bounded task/dependency/warning/date-range facts supplied | Unit/component |
| Gantt selection changes | `SelectionChanged` in same epoch | Component |
| Old context completion arrives after navigation | Refresh targets original source safely; current unrelated source is not overwritten | Unit/component |

## Scope and construction

| Scenario | Expected result | Test layer |
|---|---|---|
| Project authority builds workspace services | All services expose the same Project scope identity | Unit |
| Organization and Project bundles | No shared mutable scope-bound service | Unit |
| MCP/browser artifact path | Uses the same scope identity as file/receipt services | Integration |
| Missing required scope service | Factory fails fast; no fallback `new` path | Negative unit |
| Manual workspace factory path | Uses the same typed factory as DI path | Composition |
| Service locator scan | No runtime/core `IServiceProvider` field | Source assertion |

## Runtime ports and MAF adapter

| Scenario | Expected result | Test layer |
|---|---|---|
| Agent execution | `IAgentExecutionRuntime` path preserves response, usage, traces, finalizer | Unit/integration |
| Approval continuation | `IAgentContinuationRuntime` path maps stable IDs | Unit/integration |
| Provider health/test | Diagnostics port only | Unit |
| Provider model administration | Administration port only | Unit |
| Hosted agent | Factory owns lifetime and disposal | Unit |
| MAF runtime state serialization | Produces versioned envelope | Unit |
| Incompatible envelope | Explicit migration or failure | Negative unit |
| Provider/model/toolset change while pending | Continuation compatibility policy decides explicitly | Negative/integration |
| Cleanup failure | Primary execution failure remains authoritative | Unit |

## Dependency and process ownership

| Scenario | Expected result | Test layer |
|---|---|---|
| MAF csproj scan | No `Modules.*` reference | Source assertion |
| MAF source scan | No `using CanDoItAll.Modules.*` | Source assertion |
| MAF process scan | No process outcome/status/path/source-kind semantics | Source assertion |
| Process artifact recovery | Direct Processes policy unit tests | Unit |
| Provider selection for process | Processes policy chooses candidate; generic runtime is unaware of source string | Unit |
| Recovered process result | Ordinary completion coordinator and gates run | Integration |
| Process recovery rejection | Old/stale/wrong artifact evidence fails closed | Negative integration |

## Lightweight LLM, workflow transform, and future ordinary-chat boundary

| Scenario | Expected result | Test layer |
|---|---|---|
| Text transform | Direct LLM port returns text and usage | Unit |
| JSON schema transform | Schema response and validation preserved | Unit |
| Payload contains Project ID | No workspace authority or context contributor is acquired | Negative unit |
| Provider failure | Workflow usage observation preserved | Unit |
| Cancellation | Propagated without fabricated usage success | Unit |
| Full-agent path scan | No temporary agent/session construction | Source assertion |
| Provider-runtime reuse | Existing provider runtime/driver called once; no parallel credential/HTTP/retry stack | Unit/composition/source |
| Ordered multi-turn messages | Repository-owned order and roles map exactly to provider request | Unit |
| Streaming terminal update | Monotonic sequence, one terminal update, one usage owner | Unit/integration |
| Future ordinary conversation contract | Transcript/store depends on stateless port; port remains persistence-free | Architecture/unit |
| Ordinary chat source scan | No `AgentDefinition`, agent session, MAF state, tools, or ambient product context | Source assertion |

## Suggested commands

Run the narrowest relevant commands in each subbundle, then the full set at checkpoints:

```powershell
dotnet build CanDoItAll.slnx --configuration Release

dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build

dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --no-build

dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build
```

Use `--filter FullyQualifiedName~...` during subbundles. Record exact commands and results in the proof manifest.

## Revision 2 stabilization and lightweight LLM matrix

| Scenario | Expected result | Layer |
|---|---|---|
| Lightweight single-turn | provider driver called once; no agent/capability/session graph | unit/composition |
| Lightweight ordered messages | system/user/assistant order preserved | unit |
| Lightweight streaming | monotonic updates and one terminal usage source | unit/integration |
| Lightweight payload contains project ID/path | no authority/workspace/context acquisition | negative unit/source |
| Future ordinary-chat contract | transcript owner delegates stateless invocation | unit/architecture |
| Broad runtime caller scan | no new production references; facade removal readiness | source/dependency |
| Dual-path fault | exactly one side-effecting path observed | integration/telemetry |
| Provider failure after cutover | sanitized failure, usage/cleanup preserved | fault integration |
| Persistence failure | primary failure and terminal-state behavior preserved | fault integration |
| Profile switch during capture | operation fails/retries safely without cross-profile state | concurrency |
| Restart with legacy pending approval | explicit compatible resume or explicit incompatibility | persistence integration |
| Process recovery | ordinary completion gates/receipts once | process integration |
| Public API projection | no envelope/authority/attachment payload | API integration |
| Manual Canvas/Gantt acceptance | next-turn transition, active-run immutability | rebuilt UI |
