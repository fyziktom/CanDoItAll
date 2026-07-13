# Source Artifacts

## Skills Used

- `csharp-modular-refactoring`
- `csharp-testability-contracts`
- `csharp-dependency-graph-audit`
- `csharp-factory-builder-composition`
- `csharp-provider-tool-plugin-isolation`
- `csharp-design-pattern-selection`
- `csharp-architecture-governor`
- `csharp-architecture-review-gate`
- `candoitall-bundle-workflow`
- `candoitall-bundle-preparation`
- `candoitall-csharp-architecture-bundle-guard`
- `candoitall-codeanalytics-mcp`

## Repository Evidence

| Artifact | Evidence |
| --- | --- |
| CodeAnalytics snapshot | `snap-20260706180906-6ece4834` for `CanDoItAll.slnx`, scoped to project `CanDoItAll.AgentFramework.Maf` and namespace `CanDoItAll.AgentFramework.Maf`. |
| CodeAnalytics type search | Found `MafAgentRuntime`, `MafRuntimeAgentFactory`, `RuntimeCapabilityComposer`, and `WorkspaceRuntimePlugin` as concrete type hotspots. |
| CodeAnalytics hotspots | `RuntimeCapabilityComposer` 106 members, `WorkspaceRuntimePlugin` 93, `MafAgentRuntime` 59, `MafRuntimeAgentFactory` 31. |
| CodeAnalytics findings | `COMPLEXITY-001` large files and `COMPLEXITY-002` large types in MAF runtime/capability/workspace files. |
| Local line counts | `MafAgentRuntime.cs` 1779, `RuntimeCapabilityComposer.cs` 972, `WorkspaceRuntimePlugin.cs` 922, `MafRuntimeAgentFactory.cs` 886, `McpCapabilityBuilder.cs` 841, `MafFinalizerDriver.cs` 804. |
| Direct partial scan | `RuntimeCapabilityComposer` remains partial across access, policy, descriptors, runtime-tool providers, and main composer files. `MafAgentRuntime` no longer has partial files. |
| Direct composition scan | `MafAgentRuntime` constructs `RuntimeCapabilityComposer` and `MafRuntimeAgentFactory`; module/hosting code directly constructs `MafAgentRuntime`. |
| Existing tests | `MafRuntimeArchitectureServicesTests` guards against `MafAgentRuntime` partials and nested collaborators but does not yet block `RuntimeCapabilityComposer` partials or prove thin-runtime delegation. |

## Microsoft Learn Grounding

- [.NET dependency injection guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines#recommendations): avoid service locator, avoid hard-coded dependencies, keep DI factories fast and synchronous, validate scopes.
- [.NET unit testing best practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices#best-practices): avoid infrastructure dependencies in unit tests, use explicit dependencies, and keep unit tests separate from integration tests.

## Source Files To Treat As Primary Inputs

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.CatalogDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`
