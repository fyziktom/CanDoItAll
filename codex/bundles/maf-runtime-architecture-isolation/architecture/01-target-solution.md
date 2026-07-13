# Target Solution

## Architecture Direction

`MafAgentRuntime` should become a thin coordinator around injected runtime services. It should own the public `IAgentRuntime` entry points, high-level orchestration, and compatibility behavior, but it should not directly build every capability, provider client, runtime tool, workspace helper, MCP client, context provider, finalizer path, or fallback dependency.

The target split is not "more partial files." The target is separate classes with typed request/result contracts, constructor-injected dependencies, direct tests, and production registration.

## Proposed Responsibility Boundaries

| Responsibility | Target owner | Reason |
| --- | --- | --- |
| Public runtime entry point | `MafAgentRuntime` | Keep API compatibility and high-level execution orchestration. |
| Runtime build orchestration | `AgentRuntimeBuildCoordinator` or equivalent | Build an executable runtime from agent/provider/model/session inputs without hiding state inside `MafAgentRuntime`. |
| Provider client construction and credential resolution | `IProviderRuntimeClientFactory`, `IProviderCredentialResolver` implementations | Provider behavior should be mockable and testable without full runtime execution. |
| Runtime session construction | `IRuntimeSessionFactory` / existing `MafRuntimeSessionBuilder` evolution | Session mapping and chat history setup are distinct from capability composition. |
| Capability access planning | `IRuntimeCapabilityAccessPlanner` | Already partly modeled; should be directly testable and not nested in runtime. |
| Capability composition | `IRuntimeCapabilityComposer` | Own ordered attachment of memory, context, skills, workspace tools, registered providers, MCP/A2A/catalog tools, and compaction. |
| Runtime mutable state | `RuntimeCapabilityAssembly` or equivalent result record | Replace private mutable nested state with a typed result/collector that can be asserted in tests. |
| Runtime tool provider composition | `IRuntimeToolProviderComposer` | Enumerate, sort, validate, prefilter, materialize, metadata-resolve, and attach provider tools. |
| Built-in tool creation | `IBuiltInToolDriver` | Remove hard-coded tool switch and workspace plugin coupling from nested `ToolCapabilityBuilder`. |
| Workspace runtime tools | `IWorkspaceRuntimeToolDriver` / `IWorkspaceRuntimePluginFactory` | Workspace helpers should be constructed and tested independently with fake file/command/artifact services. |
| MCP capability behavior | `IMcpCapabilityDriver` | MCP list-tools, hosted/local client setup, approvals, and disposal should be testable without full MAF runtime. |
| Context and skill behavior | `IContextCapabilityDriver`, `ISkillCapabilityDriver` | RAG, static context, Mem0, inline/file skill behavior are separate feature drivers. |
| Finalizer behavior | `IFinalizerCoordinator` around existing `MafFinalizerDriver` | Finalizer capture, validation, recovery, and response shaping should not depend on private runtime state. |
| Diagnostics/progress | `IRuntimePreparationDiagnostics` or equivalent | Progress messages, context manifest sources, attachment summaries, and timing should have a single typed path. |
| Performance instrumentation | `IRuntimeCompositionMetrics` | Time local startup stages and provider boundaries without ad hoc stopwatch code in every driver. |

## Key C# Shapes To Introduce During Implementation

Names are proposed; implementation may choose local names while preserving boundaries.

| Shape | Purpose |
| --- | --- |
| `RuntimeBuildRequest` | Agent, provider, model, capabilities, memory, session, context intent, approval mode, workspace scope, cancellation. |
| `RuntimeBuildResult` | Agent instance, provider/model, disposables, capability assembly, finalizer capture, trace recorders. |
| `RuntimeCapabilityCompositionRequest` | Typed input for capability composition independent from `MafAgentRuntime`. |
| `RuntimeCapabilityAssembly` | Immutable or controlled-mutable output containing tools, metadata, context providers, diagnostics, disposables, and effective capabilities. |
| `RuntimeToolProviderCompositionRequest` | Provider composition input with access plan, context intent, agent/provider, approval mode, and tags. |
| `RuntimeToolProviderCompositionResult` | Attached tools, metadata, summaries, diagnostics, and excluded-tool counts. |
| `RuntimeDriverDiagnostic` | Typed warning/error/info record with driver id, category, safe message, and actionable state. |
| `RuntimeCompositionMeasurement` | Stage timing/allocation signal for startup and provider-attachment cost. |
| `MafRuntimeServiceOptions` | Strongly typed options for default/fallback policy, measurement, tool-provider composition limits, and diagnostics. |

## Dependency Policy

- Required runtime collaborators should be constructor-injected and fail fast if missing.
- Legitimate defaults should be registered in `AddCanDoItAllMafRuntime` or narrower `Add{Feature}` methods, not constructed deep inside execution.
- Optional collaborators should be represented as nullable options or no-op implementations with explicit names and tests.
- `IEnumerable<T>` extension points are appropriate for runtime tool providers and context contributors, but provider metadata and ordering must remain deterministic.
- Avoid raw `IServiceProvider.GetService` in extracted runtime drivers unless implementing a documented adapter for legacy compatibility.

## Performance Strategy

- Measure before changing: local runtime build, capability access planning, composition, provider enumeration, tool creation, metadata resolution, filtering, deduplication, session creation, and first external provider boundary.
- Prefer reducing unnecessary work over micro-optimizing syntax: prefilter providers before expensive tool creation where metadata supports it; cache immutable descriptors safely; avoid repeated service fallback construction; reuse typed plans where context keys match.
- Any cache key must include inputs that affect tool availability: agent id/version, capability assignments, access plan, context intent, workspace scope, provider key/model, registered provider version, and approval mode.
- Do not remove LINQ or allocate fewer lists blindly; only tune measured hot paths.

## Testability Strategy

- Add direct unit tests for each extracted collaborator before or during extraction.
- Use fake provider client factories, fake runtime tool providers, fake context contributors, fake workspace services, fake MCP clients, and fake diagnostics sinks.
- Replace private reflection tests with direct collaborator tests where the behavior is moved.
- Keep integration tests that build a real `MafAgentRuntime`, but make them smaller because collaborators can be mocked or replaced.
- Add architecture boundary tests that prevent new nested driver classes or new domain/tool-driver responsibilities from being added back into `MafAgentRuntime`.

## Rejected Approaches

| Approach | Why rejected |
| --- | --- |
| Keep adding partial files | Preserves the same unit of responsibility and testability problem. |
| Rewrite all of MAF in one subbundle | Too risky for runtime behavior, provider integrations, and every agent. |
| Add interfaces for every private method | Creates boilerplate without real ownership boundaries. |
| Leave fallback construction in runtime drivers | Keeps missing dependencies hidden and makes tests less honest. |
| Start with Financial Strategist/MarkItDown fixes | Solves a symptom before the runtime base is stable. |
| Optimize LINQ/list allocations first | Current scan shows architecture composition is the likely cost; measure before micro-optimizing. |

## Production Behavior Artifact Matrix Requirement

Critical implementation subbundles that introduce new runtime contracts, diagnostics, measurements, dependency defaults, state records, or attachment receipts must include a `## Production Behavior Artifact Matrix` in both `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md`.

The matrix must cite:

- production producer;
- production consumer;
- lifecycle path;
- negative proof showing test-only fakes, seeded rows, or unused wrappers do not count.
