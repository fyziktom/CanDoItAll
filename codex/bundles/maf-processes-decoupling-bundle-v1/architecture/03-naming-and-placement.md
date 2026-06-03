# Naming And Placement Guidance

## New Project

Preferred name:

```text
CanDoItAll.AgentFramework.Tooling
```

Rationale:

- `Core` currently has no `Microsoft.Extensions.AI` dependency.
- `Models` should stay model-only.
- MAF should depend on a small provider-neutral tool contract rather than product modules.
- Future `ProjectStructureAgentRuntimeToolProvider`, `ImageGenerationAgentRuntimeToolProvider`, Office providers, and driver adapters can use the same seam.

## New Files

Suggested files:

```text
src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj
src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs
src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs
src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderPurpose.cs
src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderDescriptor.cs  (optional if Codex keeps descriptors simple)
src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs
src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Models.cs (optional split)
tests/CanDoItAll.Tests.Unit/AgentRuntimeToolProviderArchitectureTests.cs
tests/CanDoItAll.Tests.Unit/ProcessAgentRuntimeToolProviderParityTests.cs
```

## Avoid

Do not name the abstraction `IProcessHelperDriver` in this bundle. That belongs to later process driver work and would confuse runtime tool composition with domain helper capability.
