# Scope Inventory

## Production Code

| Area | Files | Planned Change |
| --- | --- | --- |
| Shared git wrapper | `repo://src/CanDoItAll.Git/GitCommandContracts.cs`, `repo://src/CanDoItAll.Git/GitRepositoryClient.cs`, `repo://src/CanDoItAll.Git/GitRepositoryPath.cs` | Add reusable specs, validation, diff modes, and unstage support. |
| Workspace commands | `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceProcessContracts.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs` | Add new git tool methods and plans backed by `CanDoItAll.Git`. |
| Runtime tool composition | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs` | Expose and attach new git tool functions. |
| Tool policy | `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`, `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`, `repo://src/CanDoItAll.AgentFramework.Models/Agents/Access/AgentWorkspaceToolAccessModels.cs` | Add constants, classifications, and permission mapping. |
| Project references | `repo://src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj` | Reference `CanDoItAll.Git`. |

## Templates And Skills

| Area | Files | Planned Change |
| --- | --- | --- |
| Tool capabilities | `repo://Templates/Capabilities/tools.json` | Add new git tool descriptors. |
| Inline skill capability | `repo://Templates/Capabilities/skills.json` | Add `git-standard-operations` skill entry. |
| Skill instructions | `repo://Templates/Capabilities/skills/instructions/git-standard-operations.md` | New agent guidance for standard git workflows. |
| Default agent assignments | `repo://Templates/Agents/**/skills.json` | Add skill and new tools to relevant delivery/architecture agents. |

## Tests

| Area | Files | Planned Change |
| --- | --- | --- |
| Wrapper | `repo://tests/CanDoItAll.Tests.Unit/ProcessTemplateGitFoundationTests.cs` | Add command spec, path, branch, revision, unstage tests. |
| Workspace commands | `repo://tests/CanDoItAll.Tests.Unit/WorkspaceCommandExecutionServiceTests.cs` | Add plan tests for git mutation and read commands. |
| Access policy | `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs` | Add read/mutation git permission assertions. |
| Capability templates | `repo://tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs` | Update expected catalog and assert skill/tool materialization. |
| Runtime composition | `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | Assert software-development agents get new git tools and read-only contexts do not get mutations. |
