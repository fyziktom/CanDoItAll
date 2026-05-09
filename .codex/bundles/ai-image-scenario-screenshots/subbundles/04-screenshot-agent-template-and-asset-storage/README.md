# screenshot-agent-template-and-asset-storage

## Status

- `Completed`

## Objective

Seed agent templates for app screenshot capture, screenshot review/storage, and the storage-tool permissions required to create project-structure image asset nodes from process outputs.

## Success Criteria

- App screenshot capture agent template can run .NET and JavaScript apps, use workspace command tools, and use Playwright MCP.
- Screenshot review/storage agent template can read screenshots, review them, and write image assets through file storage/project structure.
- Agent templates expose project-structure and process access settings strongly enough to write outputs to the owning project.
- Capability verification/readback proves the agent templates have the expected tools.

## Covered Inputs

- R8, R9, R2, R3.
- Raw note `N005`.

## Prerequisites

- Subbundle 01 closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\app-screenshot-capture-template.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\screenshot-review-storage-template.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workspace\MafAgentRuntime.StorageRuntimePlugin.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`

## Deliverables

- Screenshot capture agent template instructions.
- Screenshot review/storage agent template instructions.
- Agent seed entries or template entries with Playwright MCP, workspace commands, storage, process, and project-structure access.
- Capability/readback proof.

## Dependency Impact

- Subbundle 05 depends on these templates to execute the process with the right tools.
- Subbundle 06 depends on the same storage and image-provider access pattern.

## Validation Depth

- `Critical agent-tool foundation`

## Implementation Steps

1. Inspect existing agent seed templates and capability assignments.
2. Add a capture-agent template that can start .NET/Vite apps, wait for reachable URLs, run Playwright MCP, and produce screenshot artifacts.
3. Add a review/storage-agent template that validates image readability and stores accepted screenshots as project-structure image assets.
4. Assign only the necessary project/process/storage/workspace/Playwright/image capabilities.
5. Add seed asset manifest entries and instruction assets.
6. Read back the agents through API/catalog and verify capabilities.
7. Update the execution report.

## Scope Exceptions

- Do not execute the Scenario 01 process in this phase.
- Do not generate improved layouts in this phase.

## Do Not Do

- Do not give blanket external access beyond the scenario target roots needed by processes.
- Do not let screenshot review silently pass blank or failed screenshots.
- Do not store screenshots as plain markdown links when image asset nodes are required.

## Acceptance Checklist

- [x] Capture agent template has Playwright MCP and app-run capabilities.
- [x] Review/storage agent has project-structure asset/storage write access.
- [x] Image-provider preference metadata is available for future layout generation.
- [x] Agent catalog/API readback is recorded.

## Completion Evidence

- `C:\repositories\CanDoItAll\.codex\bundles\ai-image-scenario-screenshots\evidence\screenshot-agent-template-readback.json`
- `C:\repositories\CanDoItAll\.codex\bundles\ai-image-scenario-screenshots\evidence\screenshot-agent-template-editor-readback.json`
- `dotnet build src\CanDoItAll.AgentFramework.Persistence\CanDoItAll.AgentFramework.Persistence.csproj --no-restore`
- `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore /p:BuildProjectReferences=false --filter "FullyQualifiedName~Organization_workspace_seeds_screenshot_agent_templates_with_required_access|FullyQualifiedName~CreateCapabilityState_attaches_internal_project_structure_tools_by_default_when_workspace_services_are_available"`
- `GET /api/agents?includeTemplates=true`
- `GET /api/agents/{agentId}`

## Proof Required

- Agent catalog/API readback.
- Capability verification where available.
- Targeted tests/build for seed catalog normalization if changed.

## Browser Validation Logging

- N/A until runtime proof. The agent instructions must require browser proof logging in subbundle 05.

## Progression Gate

- Subbundle 05 may start only after capture and review/storage agent templates are readable.
- Those templates must have the required Playwright, command, project/process, and storage/image access.

## Suggested Agent Prompt

```text
Implement only the screenshot-agent-template-and-asset-storage subbundle.
Seed screenshot capture and screenshot review/storage agent templates with bounded tool access. Use typed image-provider metadata from subbundle 01. Verify catalog/API readback and update the execution report.
```
