# Codeanalytics And Performance Review

## Evidence Scope

- Codeanalytics snapshot: `snap-20260628122504-1aa0230f`
- Solution scope: `CanDoItAll.slnx`
- Scoped projects: `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Core`, `CanDoItAll.AgentFramework.Persistence`, `CanDoItAll.AgentFramework.Tooling`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Workbench`, `CanDoItAll.Web`, `CanDoItAll.Tools.Documents`
- Scoped code scan paths: MAF runtime/capabilities, core capabilities/tool policy, persistence seeds, tooling project, agent tool providers, capability UI components, `AgentsApi`, and document tools.

## Codeanalytics Findings

| Finding | Evidence | Bundle response |
| --- | --- | --- |
| MAF is still the capability composition hub. | Snapshot project inventory shows `CanDoItAll.AgentFramework.Maf` references Core, Tooling, and Tools.Documents, and is referenced by `CanDoItAll.Modules.AgentFramework`. | SB08 keeps MAF as adapter only and blocks reconnect until SB01-SB07 proof exists. |
| MAF capability runtime has a type cycle. | Snapshot dependency facts report a type cycle in `CanDoItAll.AgentFramework.Maf` involving six MAF runtime/capability types. | SB09 requires cycle review and blocks UI/API work if reconnection moves the cycle instead of reducing it. |
| AgentFramework module graph has a module cycle. | Snapshot dependency facts report a module-level cycle inside `CanDoItAll.Modules.AgentFramework`. | SB10/SB11 proof must avoid adding setup/UI dependencies that worsen the cycle. |
| MCP builder is a large MAF-owned runtime type. | Focused context resolves `MafAgentRuntime.McpCapabilityBuilder` in `MafAgentRuntime.Capabilities.Mcp.cs`, line 13 through line 739, with hosted MCP creation at line 331. | SB04 moves lifecycle/list-tools setup into MCP services; SB08 leaves MAF only as adapter. |
| Existing tool seam is too narrow. | Symbol search resolves `IAgentRuntimeToolProvider` at `src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`, line 5; it returns runtime tools but does not cover templates, setup tests, external calls, or structured diagnostics. | SB02 builds a fuller tool abstraction and compatibility bridge instead of overloading the old seam. |
| Tool policy is centralized but hardcoded. | Symbol search resolves `ToolContractCatalog` at `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`, line 3. | SB01/SB02 require typed constants/generated registry and policy parity proof before cleanup. |
| Capability governance is split across required checks, process operation strings, and MAF attach-time filters. | Code review identified `AgentCapabilityRequirementEvaluator`, `ProcessToolOperationAuthorizer`, `AgentRuntimeContextIntent.AllowedOperations`, and `MafAgentRuntime.Capabilities` attach helpers as separate decision points. Codeanalytics service registration search for `Capability` surfaced only `ICapabilityProofService`, so the access decision is not yet an explicit reusable DI service. | SB01 defines typed access contracts; SB05 hardens the evaluator; SB06/SB07 compile templates and current operation rules into policy; SB08/SB09 force MAF to consume `EffectiveCapabilitySet`; SB10/SB11 prove UI and process/workflow restrictions. |

## Pass 1: Initial Performance Review

The proposed isolation is directionally correct for maintainability and performance, but only if it does not replace one monolithic MAF capability path with three new monoliths. The current code has large orchestration files and high fan-in around capability composition. The bundle therefore needs checkpoints before templates consume the new services and before MAF reconnects.

Primary risks:

- Repeated descriptor/template parsing during runtime composition instead of one validated catalog materialization.
- LINQ-heavy filtering/materialization inside hot capability attachment and tool dispatch paths.
- Per-call JSON options/context construction for template, setup-test, or external tool payloads.
- External process/MCP setup reading unbounded stdout/stderr or protocol payloads into memory.
- Generic catch/rethrow losing context needed for setup repair.
- MAF adapter retaining old private DTOs and long-file orchestration under new method names.
- Capability restriction rules being reimplemented separately in MAF, process execution, workflow execution, and UI preview.
- Per-call policy parsing or raw string selector comparisons in runtime attach or tool dispatch paths.

Required design response:

- Load and validate templates once per seed/setup/runtime boundary, then pass typed descriptors.
- Use ordinal keyed dictionaries/lookups for capability keys and runtime names.
- Cache JSON serialization options or source-generated contexts where DTO shapes are stable.
- Bound external process stdout/stderr and HTTP/MCP response excerpts.
- Make cancellation and cleanup part of the invoker/lifecycle contracts.
- Split adapters by capability kind and keep private helper methods small.
- Compile access policies once into typed selectors and evaluate against common exposure descriptors.
- Treat denial as a restriction of the assigned candidate set, not as a grant mechanism.

## Pass 2: Deep Pattern Scan

Exact scoped scan results:

| Scan | Count | Interpretation |
| --- | ---: | --- |
| `async void` | 0 | Good baseline. Preserve this in new setup/test services. |
| `Task.Result` or `.Wait` candidates | 8 | Manually inspect before touching adjacent async code. New code must stay fully async. |
| `.IndexOf("literal")` without comparison | 0 | No immediate scoped issue. |
| `.StartsWith` or `.EndsWith` literal without comparison | 0 | No immediate scoped issue. |
| `.Contains("literal")` candidates | 0 | No immediate scoped issue. |
| `.Substring(` candidates | 1 | Low risk, but new parsers should prefer span or structured JSON parsing. |
| `.ToLower()` or `.ToUpper()` without culture | 0 | Good baseline. |
| triple chained `.Replace` | 0 | Good baseline. |
| `params` signatures | 14 | Review only if new hot-path helpers add allocation-heavy calls. |
| `static readonly Dictionary<` | 0 | No existing static dictionary issue in scoped files. |
| `static readonly FrozenDictionary<` | 0 | Static registries can consider frozen lookups when read-heavy and stable. |
| `new List<` candidates | 63 | New materializers should pre-size and avoid repeated allocation in loops. |
| `new Dictionary<` candidates | 45 | New registries should use ordinal comparers and avoid per-call static data creation. |
| LINQ chain candidates | 832 | Most are not necessarily hot path, but capability attachment/dispatch code must be manually reviewed. |
| `RegexOptions.Compiled` | 0 | No compiled regex startup risk in scoped files. |
| `GeneratedRegex` | 1 | Good baseline if static regex grows. |
| `new Regex(` | 0 | No scoped regex construction issue. |
| `new HttpClient(` | 0 | Preserve this; external HTTP tools should use factory-managed clients. |
| `new JsonSerializerOptions` | 7 | New code must cache options/source-gen contexts, especially in template/setup result flows. |
| `JsonSerializer.Serialize/Deserialize` | 58 | New template/setup DTOs need explicit serializer policy and tests. |
| `File`/`Directory` I/O usage | 57 | New template and skill loaders must use clear path validation and async I/O where useful. |
| `ProcessStartInfo`/process start candidates | 0 | External tool/MCP launch will introduce this; SB02/SB04 must bound it heavily. |
| unsealed concrete class candidates | 11 | Seal new leaf services unless test/proxy constraints require otherwise. |
| sealed classes | 100 | Existing code already uses sealed classes widely. |

Large file pressure in scoped areas:

| File | Lines | Concern |
| --- | ---: | --- |
| `src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs` | 3186 | UI edits must not grow this pattern; setup UI should use focused services/components. |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 2418 | MAF is already too large; reconnect must shrink responsibility. |
| `src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | 2269 | Tool migration must split domain helpers if wrapping this provider. |
| `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` | 2225 | Policy parity tests should avoid adding more hardcoded branches here. |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | 1674 | Adapter work must not add more capability composition here. |
| `src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` | 1131 | SB06/SB07 must replace seed construction with template materialization and parity tests. |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | 1047 | SB08 should reduce active capability logic in this file. |

## Architectural Verdict

The proposed solution is correct only with the added checkpoint gates. Without SB05, SB07, and SB09, the implementation can pass happy-path tests while preserving the original failure mode: hardcoded capability behavior distributed through large files and generic runtime errors.

The bundle must require:

- Structured diagnostics before concrete implementations.
- Dedicated isolated implementation projects before template materialization.
- A typed capability access policy/effective-set layer before MAF reconnection.
- Hardening before MAF reconnection.
- Runtime performance and cycle review before UI/API work.
- Regression proof across unit, integration, component, and Playwright layers before cleanup.
