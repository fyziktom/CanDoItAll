# Scope Inventory

## Generated Artifacts

- `bundle://inventories/api-docs-skills-gap-map.xlsx`: primary route/gap/DTO/docs/skills/tool parity workbook.
- `bundle://inventories/api-docs-skills-gap-map-summary.png`: rendered workbook summary proof.
- `bundle://inventories/api-docs-skills-gap-map-inspect.json`: artifact-tool inspect output for the summary sheet.
- `.codex/tmp/api-docs-skills-gap-map/build-gap-map.mjs`: workbook generator.

## API Source Files

- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`: 57 agents routes.
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`: 37 workflows routes.
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`: 58 processes routes.
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`: 51 project-structure routes.
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs`: 38 legacy Cognitive Memory routes and 38 v1 aliases.
- `repo://src/CanDoItAll.Web/Api/ProjectsApi.cs`: 10 projects routes.
- `repo://src/CanDoItAll.Web/Api/PluginsApi.cs`: 20 plugins routes.

## DTO And Provider Source Files

- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`
- Provider capability and pricing model files under `repo://src/CanDoItAll.AgentFramework.Models` and provider services.

## Tooling Source Files

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`: 23 process tools.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs`: 28 project-structure tools.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`

## Docs And Skills

- `repo://docs/api-control-plane.md`
- `repo://docs/cognitive-memory/operations/api.md`
- `repo://docs/process-agent-operator-runbook.md`
- `repo://docs/agent-runtime-hardening-verification.md`
- `repo://codex/skills/candoitall-api-agents/SKILL.md`
- `repo://codex/skills/candoitall-api-workflows/SKILL.md`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-api-project-structure/SKILL.md`
- `repo://codex/skills/candoitall-api-cognitive-memory/SKILL.md`
