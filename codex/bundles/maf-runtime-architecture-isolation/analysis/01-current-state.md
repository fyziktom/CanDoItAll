# Current State

## Executive Root Cause

`MafAgentRuntime` is functioning as an orchestration boundary, composition root, provider factory, capability builder, tool-driver host, workspace tool host, MCP driver host, finalizer coordinator, credential resolver, diagnostics collector, and mutable runtime-state owner. Splitting that across partial files reduced physical file size pressure, but it did not create testable architectural isolation.

The core architecture mistake is not that one agent lacks one domain tool. The generic problem is that runtime responsibilities are private, nested, mutable, and service-located inside the runtime. That makes direct unit tests difficult, pushes tests toward reflection or full-runtime construction, and forces future agent features to modify or reason through the same large class family.

## Scope Correction

- Financial Strategist PDF/MarkItDown/tool reachability is deferred.
- Quotation extraction, margin calculation, and project-structure writeback are removed from this bundle.
- This bundle focuses only on MAF runtime architecture, driver isolation, testability, integration mockability, and performance impact.

## Partial-Class Shape

Top line-count pressure points under `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime`:

| File | Lines | Current responsibility pressure |
| --- | ---: | --- |
| `MafAgentRuntime.cs` | 2185 | Main execution path, provider streaming, approvals, response assembly, fallback provider gate creation, finalizer recovery, runtime helpers. |
| `MafAgentRuntime.AgentFactory.cs` | 1709 | Agent/provider build, provider clients, credential resolution, runtime build result, hosted runtime wrapper, nested mutable capability state. |
| `Capabilities/MafAgentRuntime.Capabilities.cs` | 963 | Capability state lifecycle, service fallback creation, builder creation, provider enumeration, access plan attachment. |
| `Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | 924 | Workspace runtime tool implementation nested inside MAF. |
| `Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | 822 | MCP runtime client and tool attachment behavior in a nested builder. |
| `MafFinalizerDriver.cs` | 804 | Finalizer behavior is already partly separated, but still tightly coupled to runtime closure behavior. |
| `Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs` | 451 | Provider attachment, metadata, filtering, approval wrapping, and duplicate checks. |
| `Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | 404 | Built-in tool mapping and plugin tool behavior inside nested `ToolCapabilityBuilder`. |

There are 19 `partial class MafAgentRuntime` declarations in the runtime tree. The class is sealed, which is good for runtime dispatch, but the partial pattern hides a very broad unit of responsibility.

## Coupling Points

### Constructor And Fallbacks

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` constructor state stores raw `IServiceProvider` and resolves runtime gateway dependencies directly. When dependencies are absent, it creates fallback implementations:

- `IMafProviderRuntimeGateway` falls back to `MafProviderRuntimeGateway.CreateFallback(services)`.
- `IMafProviderStreamingDispatchGate` falls back to `CreateFallbackProviderStreamingDispatchGate(services)`.
- `CreateFallbackProviderStreamingDispatchGate` resolves or creates `IAgentProviderFactory` and `ProviderDispatchLaneGate`.

This makes integration setup easy, but it blurs required dependencies, optional dependencies, and test seams.

### Capability Composition

`CreateCapabilityStateCoreAsync` in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` performs the full orchestration chain:

- deserialize agent configuration;
- resolve workspace tool access;
- build runtime access plan;
- create composition;
- attach memory;
- attach context contributors;
- attach skills;
- attach configured workspace tools;
- attach registered runtime tool providers;
- attach A2A tools;
- attach catalog capabilities;
- attach compaction;
- deduplicate tools.

`CreateCapabilityComposition` constructs workspace services through `services.GetService(...) ?? new ...`, creates `WorkspaceRuntimePlugin`, `StorageRuntimePlugin`, `SkillCapabilityBuilder`, `ContextCapabilityBuilder`, `McpCapabilityBuilder`, `ToolCapabilityBuilder`, enumerates providers, sorts providers, and validates duplicate keys.

This is the main seam to extract first.

### Tool Provider Attachment

`AttachRegisteredRuntimeToolProvidersAsync` currently iterates registered providers, asks every selected provider to create tools, resolves metadata, filters by capability access, wraps approvals, checks duplication, updates mutable runtime state, and emits progress.

Performance implication: provider tool creation can happen before filtering has reduced the surface enough. The bundle should measure this, then split planning/filtering from materialization where safe.

Testability implication: tests must currently construct `MafAgentRuntime`, register fake providers, and use private reflection helpers to call capability-state internals.

### Nested Drivers And Helpers

The runtime contains nested driver-like classes:

- `SkillCapabilityBuilder`
- `ContextCapabilityBuilder`
- `McpCapabilityBuilder`
- `ToolCapabilityBuilder`
- `WorkspaceRuntimePlugin`
- `StorageRuntimePlugin`
- `RuntimeCapabilityState`
- `RuntimeCapabilityComposition`
- `RuntimeToolProviderRegistration`

These are real responsibilities but not independently injectable or directly testable as collaborators.

## Existing Testability Signals

Current tests already expose the cost of missing seams:

- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` repeatedly constructs full `MafAgentRuntime` and reaches private methods with reflection.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs` reaches private runtime methods with `BindingFlags.NonPublic`.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs` reaches runtime nested/private types.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs` reaches nested/private attachment types and helpers.

The target architecture should let these tests move toward direct tests of collaborators such as `RuntimeCapabilityComposer`, `RuntimeToolProviderComposer`, `McpCapabilityDriver`, `WorkspaceRuntimeToolDriver`, `AgentRuntimeBuildCoordinator`, and `FinalizerCoordinator`.

## Focused Performance Scan

Scope scanned:

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime`

### Scan Execution Checklist

| Recipe | Hit count | Interpretation |
| --- | ---: | --- |
| `.IndexOf("...")` without `StringComparison` | 0 | No finding. |
| `.Substring(` | 1 | Inspect only if the path is proven hot. |
| `.StartsWith` / `.EndsWith` literal without `StringComparison` | 25 | Review for correctness/perf while touching files; do not lead the refactor with this. |
| `.Contains("...")` literal without `StringComparison` | 24 | Heuristic includes possible false positives; inspect when in touched hot paths. |
| `.ToLower()` / `.ToUpper()` | 0 | No finding. |
| Chained `.Replace()` 3+ | 0 | No finding. |
| `params` signatures | 4 | Low priority unless a measured hot path invokes them frequently. |
| LINQ `Select/Where/Cast/Take/Aggregate` | 152 | Potential allocation/composition pressure in startup paths; measure before rewriting. |
| `new List<` | 33 | Expected in composition; measure capability startup before optimizing. |
| `new Dictionary<` | 16 | Expected in policy/descriptor paths; measure before optimizing. |
| `async void` | 0 | No finding. |
| `.Result` / `.Wait(` | 0 | No sync-over-async finding in the scanned runtime tree. |
| `RegexOptions.Compiled` | 0 | No compiled-regex startup issue in this scope. |
| `new Regex(` | 0 | No uncached regex construction in this scope. |
| `new JsonSerializerOptions` | 2 | Measure/hoist if these are on repeated build paths. |
| Unsealed non-abstract/non-static classes | 0 | Positive: runtime classes in scope are sealed. |
| Sealed classes | 39 | Positive structural pattern. |

### Performance Interpretation

The likely startup cost is architectural composition work, not a single obvious critical .NET anti-pattern:

- service-location and fallback construction during runtime setup;
- capability composition rebuilding mutable state for each runtime build;
- provider enumeration and descriptor sorting;
- runtime tool providers creating tools before access filtering;
- MCP/list-tools and workspace/plugin setup hidden inside the same capability stage;
- LINQ/materialization and list/dictionary allocation in startup paths.

SB07 must collect before/after measurements for local runtime composition, provider attachment, descriptor creation, and first external provider boundary. It must not optimize by broad LINQ removal without evidence.

## Root-Cause Statement

`MafAgentRuntime` lacks a durable internal architecture where each driver or strategy is a separately injected, typed, directly testable collaborator. The partial class files are organized by topic, but ownership still collapses into one large runtime type. The repaired bundle fixes the planning problem by staging extraction of generic runtime seams before any agent-specific domain case is resumed.
