# Agent Policy Settings And Knowledge Guidance In CanDoItAll Web

## Status

- `Completed`

## Objective

- Add central workspace-managed agent policy and knowledge-guidance administration in CanDoItAll web so remote MCP clients can be configured and governed from the main machine.

## Covered Inputs

- `R005`, `R006`, `R011`, `R012`, `R017`
- `N003`, `N004`, `N010`, `N011`, `N012`

## Prerequisites

- `01-central-project-structure-agent-api-locking-checklist-import-and-analytics-foundation` completed with trusted contracts and persistence seams

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectStructureAgentAdministrationModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectStructureAgentAdministrationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectManagementKnowledgeService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ProjectStructureAgentSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\SettingsPageProjectStructureAgentTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectStructureAgentPolicyIntegrationTests.cs`

## Deliverables

- Persisted agent-profile and project-specific approval-policy settings.
- Generated setup guidance for other workstations, including the central base URL hint and the local MCP settings shape.
- Central knowledge-guidance provider surfaced through settings and API.
- Browser-visible settings UI for agent access and setup.
- Automated tests plus browser proof for the new settings surface.

## Dependency Impact

- `03` depends on the exact settings schema and generated setup contract defined here.
- `04` depends on the settings UI being understandable and visually correct because operators must be able to configure the system.
- Weak proof here would invalidate claims that permissions and rollout are centrally manageable.

## Validation Depth

- `Critical UI foundation`
- `Component-test and browser-proof required`

## Implementation Steps

1. Add strongly typed persistence models and services for agent profiles, project overrides, and central base URL or setup guidance settings.
2. Add a knowledge-provider abstraction with a default static implementation and expose it through the central API.
3. Extend the settings UI with a dedicated project-structure agent surface instead of hiding these controls in unrelated tabs.
4. Add generated setup output that other machines can use for MCP settings or reinstall flows.
5. Add component coverage and browser proof for the new settings surface.

## Scope Exceptions

- If generated setup output cannot include machine-specific secrets safely in the browser, show the secret only in the dedicated secure workflow and document the safety rule explicitly.

## Do Not Do

- Do not push project-structure agent configuration into appsettings-only hidden config.
- Do not make approval rules MCP-client-only.
- Do not add a visually ambiguous settings surface that operators have to interpret by guesswork.

## Acceptance Checklist

- Agent profiles can be created, edited, enabled, disabled, and scoped by capability.
- Estimate thresholds and project-specific overrides can be configured centrally.
- Setup guidance reflects the actual MCP settings contract.
- Knowledge guidance can be read through the central API.
- Browser proof confirms the new settings surface is readable and coherent.

## Proof Required

- `dotnet test` for workspace or settings service coverage
- `dotnet test` for component coverage if applicable
- Headed Playwright validation on `/settings`
- Large-screen and narrower-width screenshots of the agent settings surface
- Explicit screenshot review notes for readability, spacing, alignment, and setup-guidance clarity

## Browser Validation Logging

- `Route: /settings`
- `Viewport passes: 1600x900 first, then 1280x800`
- `Playwright actions: open settings, switch to the project-structure agent section, create or inspect an agent policy record, verify generated setup guidance is visible`
- `Expected screenshots: C:\repositories\CanDoItAll\output\project-structure-mcp\browser\project-structure-settings-desktop.png and C:\repositories\CanDoItAll\output\project-structure-mcp\browser\project-structure-settings-medium.png`
- `Review questions: is the agent section discoverable, are tokens and thresholds readable, is setup guidance copy clear, is the layout aligned with the existing design system`

## Progression Gate

- The central policy UI and knowledge-guidance surface must have passing automated proof plus reviewed Playwright screenshots before the remote MCP client may rely on the settings contract.

## Suggested Agent Prompt

```text
Implement the central CanDoItAll web settings and knowledge-guidance surface for the project-structure MCP. Keep the design aligned with the existing settings page, add strongly typed policy models, and do not move on until browser proof confirms the section is usable.
```
