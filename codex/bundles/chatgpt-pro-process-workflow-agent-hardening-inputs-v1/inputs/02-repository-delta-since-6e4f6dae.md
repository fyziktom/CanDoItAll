# Repository Delta Since `6e4f6dae9a4b654fde4243a421d72add4074d8cf`

## Overall Diff

Command basis:

```text
git diff --shortstat 6e4f6dae9a4b654fde4243a421d72add4074d8cf..HEAD
```

Overall result:

- 578 files changed
- 35,449 insertions
- 2,259 deletions

This includes historical bundle/proof files. The hardening focus should be on the process, workflow, agent, template, skill, tool, and MCP surfaces below.

## Directory-Level Diff Summary

Focused directories:

| Area | Diff summary |
| --- | --- |
| `Templates/Processes` | 73 files changed, 2,365 insertions, 1,014 deletions |
| `Templates/Agents` | 5 files changed, 14 insertions, 5 deletions |
| `codex/skills` | 5 files changed, 470 insertions, 4 deletions |
| `docs` | 11 files changed, 195 insertions, 11 deletions |
| `src/CanDoItAll.AgentFramework.Core` | 10 files changed, 226 insertions, 68 deletions |
| `src/CanDoItAll.AgentFramework.Maf` | 4 files changed, 62 insertions, 10 deletions |
| `src/CanDoItAll.AgentFramework.Models` | 8 files changed, 480 insertions, 12 deletions |
| `src/CanDoItAll.AgentFramework.Persistence` | 2 files changed, 158 insertions, 13 deletions |
| `src/CanDoItAll.Modules.AgentFramework` | 24 files changed, 3,219 insertions, 97 deletions |
| `src/CanDoItAll.Modules.Processes` | 29 files changed, 1,443 insertions, 441 deletions |
| `src/CanDoItAll.Modules.Workspace` | 8 files changed, 280 insertions, 9 deletions |
| `src/plugins` | 5 files changed, 20 insertions, 7 deletions |
| `tests/CanDoItAll.Tests.Integration` | 12 files changed, 985 insertions, 11 deletions |
| `tests/CanDoItAll.Tests.Unit` | 4 files changed, 305 insertions, 2 deletions |
| `tests/CanDoItAll.Tests.Components` | 8 files changed, 215 insertions, 5 deletions |

## Process Template Changes

The largest template movement is in `Templates/Processes`.

Major changes:

- `Templates/Processes/processes/software-delivery/definition.json`
  - 516 insertions, 135 deletions.
  - Current file size: 2,496 lines.
  - This is the central template behind the successful Tetris-style run.

- `Templates/Processes/processes/software-delivery/definition.md`
  - 98 insertions, 236 deletions.

- New template: `dotnet-architecture-design-review`
  - `definition.json`: 473 new lines.
  - Includes definition markdown, Mermaid diagrams, compatibility report, and step sidecars.

- New template: `dotnet-runtime-command-writeback`
  - `definition.json`: 314 new lines.
  - Adds explicit runtime-command project-structure writeback path.

- New template: `dotnet-ui-screenshot-writeback`
  - `definition.json`: 431 new lines.
  - Adds explicit UI screenshot/writeback flow.

- Blazor process templates were touched:
  - `blazor-app-delivery`
  - `blazor-app-repair-fix`
  - `blazor-backend-feature`
  - `blazor-frontend-feature`
  - `blazor-fullstack-feature`

- `software-delivery` gained or changed step sidecars around:
  - runtime command recording
  - UI screenshot capture
  - release rollout
  - post-release learning
  - QA validation/recheck
  - security review
  - repair path handling

Input interpretation:

The current process template set moved in a good direction: it made operation contracts, product-root boundaries, runtime proof, screenshot evidence, and writeback more explicit. The hardening work should not assume the move was wrong. It should inspect whether the implementation has become too spread across JSON templates, sidecar markdown, runtime policy, seed catalog, UI editor, dispatcher validation, and agent instructions.

## Agent Template Changes

Changed files:

- `Templates/Agents/manifest.json`
- `Templates/Agents/teams/delivery-platform/members/delivery-manager/instructions.md`
- `Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/instructions.md`
- `Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/instructions.md`
- `Templates/Agents/teams/visual-automation-templates/members/screenshot-review-storage-agent/instructions.md`

The diff is small by line count, but the instructions are semantically dense. They now emphasize:

- grounded product roots
- `external-target/...` alias discipline
- no sibling test roots when a product root is grounded
- no stale prior-run evidence
- explicit validation order
- `workspace_dotnet_run` keep-alive/lifetime guidance
- current-run browser proof
- avoiding fake package/framework/runtime/browser/test-tool shims

Input interpretation:

The later hardening bundle should treat agent instructions, tool policy, and runtime dispatcher prompts as one behavioral system. If they drift, agents will follow the most recent or most prominent prose rather than the canonical runtime rule.

## Skill Changes

Changed skills:

- `codex/skills/candoitall-api-agents/SKILL.md`
- `codex/skills/candoitall-api-cognitive-memory/SKILL.md`
- `codex/skills/candoitall-api-processes/SKILL.md`
- `codex/skills/candoitall-api-project-structure/SKILL.md`
- `codex/skills/candoitall-api-workflows/SKILL.md`

The skill delta documents the move away from removed MCP assumptions and toward HTTP API usage. It also records important governance facts:

- process enum values are numeric over HTTP unless converters are used
- process step operation contracts are typed but often represented by numeric enum values in API payloads
- process tools are fewer than the HTTP API route surface
- workflow control is HTTP API based
- DurableTask/AzureFunctions backend requests must not silently fall back to InProcess
- agents/providers/capabilities include private provider, pricing, and proof status fields

Input interpretation:

The skills themselves are part of the product surface because they shape agent behavior. They should be considered first-class hardening inputs, not documentation afterthoughts.

## Agent Framework Changes

Core and MAF changes include:

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
  - 83 insertions, 60 deletions.
  - Current size: 1,455 lines.

- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
  - Current size: 2,115 lines.

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
  - Current size: 1,082 lines.

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
  - Current size: 1,766 lines.

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
  - Current size: 782 lines.

- `src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
  - 435 new lines.

- `src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
  - 41 insertions, 6 deletions.

- `src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs`
  - 117 insertions, 7 deletions.
  - Current size: 1,034 lines.

Input interpretation:

Provider pricing, execution run tracking, tool policy, MCP capability behavior, and seed normalization are all now in active motion. These surfaces affect run cost accounting, capability availability, agent dispatch, and evidence trust.

## AgentFramework Module UI Changes

High-line additions and changed UI components:

- `CapabilityConfigurationEditorSupport.cs`: 523 new lines.
- `CapabilitySetupWizardDialog.razor.cs`: 374 new lines.
- `CapabilitySetupWizardDialog.razor`: 318 new lines.
- `CapabilityDetailsDialog.razor`: 331 new lines.
- `CapabilityDetailsDialog.razor.cs`: 241 new lines.
- `AgentProviderProfilesPanel.razor.cs`: 287 new lines.
- `AgentProviderProfilesPanel.razor`: 211 new lines.
- `AgentCapabilitiesPanel.razor.cs`: 259 insertions, 11 deletions.
- `AgentCapabilitiesPanel.razor`: 137 insertions, 63 deletions.
- `ProviderProfileTreeNodeBuilder.cs`: 153 new lines.

Input interpretation:

Provider and capability setup is now a major UI/runtime boundary. The hardening bundle should include UI behavior, DTO shape, persistence, and policy consistency as inputs.

## Processes Module Changes

Relevant changed files:

- `ProcessStepEditorForm.razor`
  - 319 insertions, 261 deletions.

- `ProcessObservationGraphsPanel.razor`
  - 267 new lines.

- `ProcessWorkspace.Graphs.cs`
  - 157 new lines.

- `ProcessWorkspaceGraphsTab.razor`
  - 80 new lines.

- `ProcessRunStatusResolver.cs`
  - 78 new lines.

- `ProcessRunAutomationDispatchService.Costing.cs`
  - 80 new lines.

- `ProcessesService.Launch.Staffing.cs`
  - 50 insertions, 17 deletions.
  - Current size: 1,653 lines.

- `ProcessObservationService.cs`
  - 65 insertions, 9 deletions.
  - Current size: 1,199 lines.

- `ProcessesService.Runtime.StepTransitions.cs`
  - 6 insertions, 37 deletions.
  - Current size: 840 lines.

Input interpretation:

Processes module changes improved observability, run graphs, costing, and status resolution. The same work increased the need for a canonical status/artifact/transition model across UI, runtime, dispatcher, process API, and tests.

## Plugin And Workflow Executor Changes

Changed plugin files:

- `src/plugins/CanDoItAll.Plugin.Docker/DockerBundledPlugin.cs`
- `src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs`
- `src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs`
- `src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`

The live workflow used Office365 executors:

- `office365.messages-by-category`
- `office365.mark-message-processed`

Input interpretation:

Office365 workflow success is real evidence, but email executor side effects are important: category processing mutates mailbox state. Testing/hardening should distinguish read-only preview, dry-run, and processed-message behavior.

## Tests Added Or Expanded

Test movement:

- Integration tests: 985 insertions, 11 deletions.
- Unit tests: 305 insertions, 2 deletions.
- Component tests: 215 insertions, 5 deletions.

Notable test files:

- `ProcessLaunchPlanningIntegrationTests.cs`: 295 new lines.
- `ProcessTemplateGovernanceTests.cs`: 142 new lines.
- `ProcessRunAutomationDispatchServiceTests.cs`: 110 new lines.
- `ProcessRunStatusResolverTests.cs`: 114 new lines.
- `AgentFrameworkExecutionRunTrackingIntegrationTests.cs`: 55 new lines.
- `AgentFrameworkWorkspaceSeedIntegrationTests.cs`: 58 new lines.
- `AiAgentProfileIntegrationTests.cs`: 53 new lines.
- `ProviderPricingTests.cs`: 127 new lines.
- `ApiDocsSkillsParityTests.cs`: 107 new lines.
- `ProcessWorkspaceTests.cs`: 167 new lines.

Input interpretation:

The tests are moving toward governance and parity coverage. The later bundle should inspect which behaviors are truly asserted versus only indirectly exercised by scenario tests.

