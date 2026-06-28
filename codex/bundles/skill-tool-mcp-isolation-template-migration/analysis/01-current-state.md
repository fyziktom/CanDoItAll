# Current State

## Summary

The user is right: skills, tools, and MCPs are currently not isolated enough. The runtime seams exist, but the actual definitions, configuration DTOs, proof rules, seed data, and UI setup behavior are spread across MAF, Core, Persistence, Web API, and Blazor components. The migration must be staged because workflows and process execution depend on those hardcoded tool names and policy classifications.

## MAF Runtime Coupling

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:56` creates the capability state and calls `AttachSkillsAsync`, `AttachConfiguredWorkspaceToolsAsync`, `AttachRegisteredRuntimeToolProvidersAsync`, `AttachCatalogCapabilitiesAsync`, and compaction attachment in one runtime path.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:94` creates concrete builders for skills, context, MCP, and tools inside MAF.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:1079` keeps `SkillCapabilityConfiguration`, `McpCapabilityConfiguration`, and `BuiltInToolConfiguration` as private nested MAF records. That blocks reuse by template loading, UI setup validation, and isolated tests.

## Tool Coupling

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs:45` maps capability keys to runtime tools with string switches.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs:122` has a second path for configured workspace tools, creating another place where tool exposure can drift.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs:5` and `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs:153` hardcode tool contract names, classifications, side effects, approval defaults, and process operation requirements.
- `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs:5` is a useful existing seam, but it only returns `AITool` instances and metadata. It does not define template descriptors, external process/http invocation, schema validation, dry-run testing, or capability catalog materialization.
- Existing provider implementations live in feature modules, for example `repo://src/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs:12` and `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs:12`, so tool ownership is not concentrated in a dedicated tools implementation project.

## Skill Coupling

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs:39` resolves file skill roots and external root allowlists from MAF config.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs:105` supports inline skills directly.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs:144` resolves registered skill service types through strings and reflection.
- OpenAI Codex skill guidance expects a skill directory with `SKILL.md` plus required `name` and `description`; current seed assets do not consistently expose that as first-class templates.

## MCP Coupling

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs:36` filters runtime MCP tools through `allowedTools`.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs:421` owns local stdio startup details including working directory and environment variable resolution.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs:497` enforces local command policy, while `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs:516` rejects raw environment variables and headers.
- The current proof path can validate structure but does not provide a dedicated setup-time test-start/list-tools flow outside runtime attachment.

## Template And Seeding Split

- `repo://Templates/README.md:6` already defines `Templates/` as the home for file-driven agent, process, and workflow packs.
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/skills.json:2` assigns capability keys in agent templates, but those capability definitions are not stored beside agent templates.
- `repo://src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj:10` embeds `SeedAssets/**`.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:43` creates stable IDs for every default skill, tool, MCP, RAG, memory, and context capability in code.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:339` creates tool capability catalog items in code, including descriptions and approval flags.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:1032` and `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:1121` materialize file skills, inline skills, and tools from hardcoded helpers.

## UI/API Setup State

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor:76` exposes "New skill"; the adjacent button exposes "New MCP server", but there is no "New tool".
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs:50` allows only `CapabilityKind.Skill` and `CapabilityKind.McpServer`; other kinds are coerced to MCP.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityConfigurationEditorSupport.cs:43` and `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityConfigurationEditorSupport.cs:122` have typed write paths for MCP and Skill only.
- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs:242` exposes generic capability list/editor/save/delete/verify endpoints but no explicit external-tool test, MCP start test, or list-tools inspection endpoint.

## Test State

- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs:77` covers seeded capability lists and seed behavior.
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:14` covers execution capability filtering.
- `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs:38` covers runtime provider ordering and composition.
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs:2231` checks that the static registry covers known catalog tools.
- Current tests should be preserved but must move toward isolated loaders, call invokers, template validation, and setup test services.
