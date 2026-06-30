# Source Artifacts

## Repository Evidence

| Surface | Source reference | Why it matters |
| --- | --- | --- |
| Templates root | `repo://Templates/README.md:6` | `Templates/` is already the approved file-driven home for agents, processes, workflows, and future sibling template sets. |
| Agent capability assignment | `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/skills.json:2` | Agent templates reference capability keys but do not define the capabilities themselves. |
| MAF capability composition | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:56` | MAF currently orchestrates skill, tool, MCP, catalog, runtime provider, and compaction attachment in one composition path. |
| MAF config DTOs | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:1079` | Skill/MCP/tool configuration types are nested in MAF instead of dedicated capability contracts. |
| Hardcoded tool mapping | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs:45` | Tool capabilities are resolved through string switches and `AIFunctionFactory` calls in MAF. |
| Skill file/inline/registered paths | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs:39` | Skills are resolved from MAF-owned config including file roots, inline text, and reflection-based registered services. |
| MCP runtime attach | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs:36` | MCP allowed tools, local command policy, secret bindings, hosted tools, and HTTP/stdio clients live in MAF. |
| Existing runtime provider seam | `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs:5` | There is already a tool provider abstraction, but it is not a full template/call contract for external tools. |
| Hardcoded seed catalog | `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:43` | Stable capability IDs and default skills/tools/MCPs are created in code. |
| Embedded seed assets | `repo://src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj:10` | Seed assets are embedded from persistence instead of being loaded from `Templates/Capabilities`. |
| Seed asset manifest | `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/manifest.json:1` | Skill roots and inline skill assets are hidden under persistence seed resources. |
| Capability proof | `repo://src/CanDoItAll.AgentFramework.Core/Capabilities/CapabilityProofService.cs:15` | Proof rules and built-in tool keys are hardcoded in core. |
| Tool policy registry | `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs:5` | Runtime tool names and policy groupings are static code constants. |
| UI setup wizard | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs:50` | UI setup currently supports MCP and Skill creation, not Tool creation. |
| Capability API | `repo://src/CanDoItAll.Web/Api/AgentsApi.cs:242` | API supports generic save/delete/verify but no explicit live test/list-tools setup endpoint. |
| Existing tests | `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs:77` | Seed and capability behavior already has integration coverage that must be migrated and expanded. |

## External Standards And Compatibility Sources

| Source | Reference | Planning impact |
| --- | --- | --- |
| MCP tool specification | `https://modelcontextprotocol.io/specification/draft/server/tools` | MCP tools have unique names, `tools/list`, `tools/call`, input/output schemas, deterministic listing guidance, and tool-name character/length guidance. |
| OpenAI function calling guide | `https://developers.openai.com/api/docs/guides/function-calling` | Tool definitions are function-like, include `name`, `description`, JSON Schema `parameters`, and can use strict schema mode. |
| OpenAI Codex skills guide | `https://developers.openai.com/codex/skills` | Skills are directories with required `SKILL.md`, `name`, and `description`; progressive disclosure and concise descriptions affect activation. |
| Anthropic tool-use guide | `https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview` | Client tools execute in the application, server tools execute remotely, strict mode can guarantee schema conformance, and tool tokens/descriptions matter. |

## Non-Repository Artifacts

- Workbook required by the user: `outputs/skill-tool-mcp-isolation-template-migration/skill-tool-mcp-isolation-plan.xlsx`.
- Bundle validation command: `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --profile initiative --stage prepared --repo-root . codex/bundles/skill-tool-mcp-isolation-template-migration`.
