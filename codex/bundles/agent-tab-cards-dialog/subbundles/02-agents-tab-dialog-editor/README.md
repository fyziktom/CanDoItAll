# Agents Tab Dialog Editor

## Status

- `Completed`

## Objective

- Replace the inline Agents tab editor with a card-led grid and a DialogService modal that contains the existing technical editor split into tabs.

## Covered Inputs

- N001: "change layout in agent page tab Agents"
- N003: "when I doubleclick on some of those agents in agents tab, it opens modal (dialog service)"
- N004: "shows agent details (what we have now in \"technical editor\" on agents tab and allow setting"
- N005: "modal must have it in tabs. Identity, Runtime, Project Structure Access, etc will be each on own tab in dialog"
- N006: "tab for showing connected skills and mcps servers with possibility to assign new (or from available list)"
- N007: "fields for editing agent parameters will be correctly using available space"
- N008: "Summary and Instructions ... are not stretched to whole width ... and it should have larger default height"

## Prerequisites

- `subbundles/01-shared-agent-card-foundation` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCapabilitiesPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCapabilitiesPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\DialogService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`

## Deliverables

- Agents tab card grid with search, New agent action, and double-click editing.
- Agent details DialogService component with Identity, Runtime, Project Structure Access, Workspace Tools, Process Access, Skills and MCP, and Tags tabs.
- Dialog preserves save/delete behavior for agent editor fields.
- Skills/MCP tab shows attached and available capabilities and can assign/remove cataloged capabilities.
- Summary and Instructions text areas use full modal width and larger default heights.

## Dependency Impact

- This is the main behavior phase. Validation and closure depend on it for component tests, browser proof, raw-note closure, and cross-module route checks.

## Validation Depth

- `Critical UI and persistence foundation`

## Implementation Steps

1. Slim `AgentCatalogPanel` into a card-led panel that loads agents and opens dialogs.
2. Add a tabbed Agent Details dialog component and move current editor logic into it.
3. Add Skills and MCP capability assignment to the dialog using existing workspace-service APIs.
4. Preserve route-driven selected-agent behavior by opening the dialog when `RequestedAgentId` is supplied.
5. Update component tests for modal editing, double-click, route-driven dialog, capability assignment, and text-area layout classes.

## Scope Exceptions

- Creating a brand-new capability catalog item from inside the dialog is optional follow-up if implementing it would expand the modal beyond the user's card/dialog layout request. Assigning from the available capability list is required.

## Do Not Do

- Do not change AgentDefinition or workspace catalog persistence shape.
- Do not remove the separate Capabilities tab from the Agents shell unless tests and UX require it.
- Do not hide project/process list lazy loading behind eager expensive calls.

## Acceptance Checklist

- Agents tab displays agent cards as the primary surface.
- Double-clicking an agent card opens a DialogService modal.
- Modal tabs include Identity, Runtime, Project Structure Access, Workspace Tools, Process Access, Skills and MCP, and Tags.
- Save works for at least identity fields.
- Capability assignment can add/remove an available skill or MCP capability.
- Summary and Instructions are full-width and taller by default.

## Proof Required

- Focused component tests in `CanDoItAll.Tests.Components`.
- At least one build or targeted test command after implementation.
- Browser screenshot of `/agents?tab=agents` card grid.
- Browser screenshot of open Agent Details dialog on Identity and Skills/MCP states if possible.

## Browser Validation Logging

- Route: `/agents?tab=agents`.
- Viewports: large desktop first, then narrower width after large pass is stable.
- Actions: navigate to Agents tab, inspect cards, double-click an agent card, switch tabs in the dialog, inspect Identity and Skills/MCP tabs, optionally save a harmless edit if test data is disposable.
- Screenshots: `evidence/agent-tab-cards-desktop.png`, `evidence/agent-details-dialog-identity.png`, `evidence/agent-details-dialog-capabilities.png`, and one narrow viewport shot if captured.
- Review questions: no overlap, modal open state readable, tabs visible, Summary and Instructions fill available width, no dialog clipping, card grid uses available page space.

## Progression Gate

- Closure may start only after component tests prove double-click dialog opening, modal tab contents, save behavior, and capability assignment.

## Suggested Agent Prompt

```text
Implement the Agents tab card grid and tabbed Agent Details dialog only. Preserve existing editor behavior and update focused tests before moving to closure.
```
