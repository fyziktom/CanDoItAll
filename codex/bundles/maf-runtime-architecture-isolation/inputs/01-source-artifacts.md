# Source Artifacts

## User-Supplied Artifacts

| Artifact | Status | Notes |
| --- | --- | --- |
| Superseding scope correction | Preserved in `inputs/00-original-request.md` | Focus strictly on generic `MafAgentRuntime` architecture refactor and testability/performance base. |
| Prior Financial Strategist scenario | Deferred | Useful future regression case only; not part of this bundle. |

## Repo Sources Inspected During Bundle Repair

| Source | Why it matters |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | Primary runtime orchestration, constructor service-location/fallbacks, session run path, approval handling, and response assembly. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | Provider client construction, runtime build result, hosted runtime wrapper, credential resolution, nested mutable capability state. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | Capability-state lifecycle, composition creation, service fallback construction, builder creation, provider enumeration. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs` | Runtime tool provider attachment, metadata resolution, filtering, approval wrapping, duplicate checks. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | Built-in tool creation and plugin capability behavior inside nested `ToolCapabilityBuilder`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | MCP driver behavior inside nested `McpCapabilityBuilder`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs` | Context provider construction inside nested `ContextCapabilityBuilder`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | Workspace tool implementation nested under runtime. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | Existing tests exercise private runtime behavior through full `MafAgentRuntime` construction and reflection helpers. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs` | Existing context tests reach private runtime methods by reflection. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs` | Existing finalizer tests depend on runtime nested/private types and reflection. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs` | Existing attachment tests reach nested/private runtime types and methods. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs` | Existing provider health tests instantiate the full runtime. |

## Microsoft Learn Sources Retained

| Source | Use in this bundle |
| --- | --- |
| `https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/service-registration` | Group related runtime services with `Add{Feature}` registration extensions and keep extension points explicit. |
| `https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines` | Prefer constructor injection, small services, explicit dependencies, and avoid service locator when constructor injection works. |
| `https://learn.microsoft.com/dotnet/core/extensions/options` | Use strongly typed options for runtime limits, feature policies, and measurement settings. |
| `https://learn.microsoft.com/dotnet/core/extensions/options-library-authors` | Library registration patterns and validation for option-bearing runtime components. |
