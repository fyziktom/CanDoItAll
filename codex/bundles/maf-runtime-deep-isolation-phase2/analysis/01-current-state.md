# Current State

## Summary

The previous phase moved some seams out of `MafAgentRuntime`, but the runtime is still the central aggregation point for unrelated behavior. CodeAnalytics reports `MafAgentRuntime` with 232 source members and a type-cycle finding involving the runtime, `ToolCapabilityBuilder`, `McpCapabilityBuilder`, `RuntimeCapabilityComposition`, and related capability types.

The root cause is not "too many files"; it is that partial files are being used as a substitute for architecture. A partial class can organize text, but it does not create ownership boundaries, injectable seams, or directly testable collaborators. The hidden private nested classes keep the runtime as the only legal construction point.

## MAF Runtime Partial Inventory

| File | Lines | Current Responsibility |
| --- | ---: | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 2204 | public runtime API, run loop, approval flow, streaming execution, finalizer recovery, process-artifact parsing, session persistence, tool-call guarding |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | 1160 | runtime build orchestration, handoff build, tool ownership, finalizer instructions, structured-output guard, policy path mapping, credential wrappers |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | 1024 | capability composition pipeline, config DTOs, compaction decisions, built-in configuration classes |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | 823 | MCP builder, hosted/local MCP construction, secret binding, Playwright launch cache, tool result compaction |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | 924 | workspace file/command/artifact/image/plugin facade plus access enforcement |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | 404 | built-in tool assembly and provider diagnostic tool definitions |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs` | 431 | catalog descriptor construction |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs` | 275 | context builder and context provider implementations |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs` | 220 | skill builder and skill capability activation |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.InputAttachments.cs` | 184 | input attachment preparation, analysis prompt, usage observations |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceSearchSupport.cs` | 190 | workspace/RAG search helper still nested under runtime partial |
| Other `MafAgentRuntime*.cs` files | 861 combined | access policies, runtime tool descriptors, provider health, storage plugin, runtime tool providers |

## Hidden Nested Types

| Type | Location | Why It Is A Problem |
| --- | --- | --- |
| `MafAgentRuntime.ContextCapabilityBuilder` | `Capabilities.Context.cs:13` | constructed with `MafAgentRuntime owner`, hiding context assembly and memory provider behavior behind the outer runtime |
| `MafAgentRuntime.SkillCapabilityBuilder` | `Capabilities.Skills.cs:8` | skill activation cannot be tested without runtime ownership |
| `MafAgentRuntime.McpCapabilityBuilder` | `Capabilities.Mcp.cs:19` | 41 members, mixes hosted MCP, local MCP, secrets, Playwright, compaction, and tool wrapping |
| `MafAgentRuntime.ToolCapabilityBuilder` | `Capabilities.Tools.cs:11`, `Tools.ConfiguredWorkspace.cs:11` | partial nested builder, owns built-in tool assembly and workspace tool exposure |
| `MafAgentRuntime.WorkspaceRuntimePlugin` | `WorkspaceRuntimePlugin.cs:18` | 88 members, combines file tools, command tools, artifact transforms, image analysis, policy enforcement |
| `MafAgentRuntime.RuntimeCapabilityComposition` | `Capabilities.cs:1033` | composition record references nested builders, preserving the dependency cycle |
| `MafAgentRuntime.AgentRuntimeConfiguration` and related DTOs | `Capabilities.cs:983-1194` | configuration shape is private to runtime, making parsing and validation hard to test directly |
| `MafAgentRuntime.PreparedInputAttachments` / `InputAttachmentAnalysis` | `InputAttachments.cs:193` | attachment pipeline state is private and forces runtime-level testing |
| `MafAgentRuntime.RepeatedToolInvocationGuard` | `MafAgentRuntime.cs:2370` | useful policy object is hidden under runtime and not independently injectable |
| `MafAgentRuntime.RequiredFinalizerCapturedException` | `MafAgentRuntime.cs:2364` | finalizer control-flow signal is hidden under runtime execution loop |

## Construction Hotspot

`CreateCapabilityComposition` currently constructs several nested builders directly:

- `new WorkspaceRuntimePlugin(...)`
- `new SkillCapabilityBuilder(this)`
- `new ContextCapabilityBuilder(this)`
- `new McpCapabilityBuilder(this)`
- `new ToolCapabilityBuilder(...)`
- `new RuntimeCapabilityComposition(...)`

This is the dependency knot. If these types stay nested and runtime-owned, every downstream change must keep reaching back into `MafAgentRuntime`.

## Testability Impact

- Tests call `new MafAgentRuntime(...)` to exercise specific capability behavior.
- Tests call `MafAgentRuntime` static helpers for finalizer artifacts, image model selection, input attachments, and tool invocation result parsing.
- Private nested builders cannot be constructed directly.
- Mocking is coarse: integration tests can replace `IAgentRuntime`, but unit tests for actual MAF internals still require full runtime setup.

## CodeAnalytics Evidence

- Snapshot: `snap-20260706154749-275f822a`
- Finding: `MafAgentRuntime` exposes 232 source members.
- Finding: `MafAgentRuntime.McpCapabilityBuilder` exposes 41 source members.
- Finding: `MafAgentRuntime.WorkspaceRuntimePlugin` exposes 88 source members.
- Finding: type cycle detected across runtime/capability builder/composition types.

## Root Cause

The architecture confuses textual separation with responsibility separation. Partial classes make a huge type physically split across files, but all private nested classes still belong to the same conceptual object. This prevents:

- independent construction,
- narrow unit tests,
- clear DI boundaries,
- explicit ownership of configuration parsing,
- replacement with fakes in integration tests,
- meaningful architecture guard tests.
