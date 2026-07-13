# Scope Inventory

## Primary Production Files

| Surface | File | Expected Change |
| --- | --- | --- |
| Workflows page markup | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor` | Remove Templates tab; add catalogue and preview dialogs; add Workflows-tab button. |
| Workflows page logic | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs` | Move template loading behind dialog open; add preview/adoption state and draft naming. |
| Workflows page styles | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css` | Add dialog-specific large-screen layout support if shared components are insufficient. |
| Canvas preview | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor` | Prefer reuse; change only if read-only preview cannot be expressed safely from the parent. |
| Template content | `repo://Templates/Workflows/workflows/default-workflows.yaml` | Debrand SEAMARK examples into generic offer-analysis examples. |

## Test Files

| Test Surface | File | Expected Change |
| --- | --- | --- |
| Component behavior | `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs` | Replace Templates-tab tests; add catalogue, preview, lazy-load, and draft naming tests. |
| Template loading/debranding | `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs` | Add source/generic-name assertions if component tests do not cover full pack. |
| Playwright smoke | `repo://tests/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs` | Update selectors if tab removal affects smoke. |
| Project-structure workflow smoke | `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs` | Remove SEAMARK labels/paths if they are UI-facing or fixture-specific. |

## Design Evidence

| Dialog | Proposal |
| --- | --- |
| Template catalogue | `bundle://evidence/design/template-catalogue-dialog-proposal.png` |
| Template preview | `bundle://evidence/design/template-preview-dialog-proposal.png` |
