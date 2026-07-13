# Source Artifacts

## Generated Design Proposals

- `bundle://evidence/design/template-catalogue-dialog-proposal.png`
  - Built-in `image_gen` tool mode.
  - Purpose: visual target for the template catalogue dialog.
  - Design intent: wide list/detail modal, search row, basic description, Preview buttons, no canvas.
- `bundle://evidence/design/template-preview-dialog-proposal.png`
  - Built-in `image_gen` tool mode.
  - Purpose: visual target for the template preview and draft-adoption dialog.
  - Design intent: extra-wide canvas-first modal, metadata/inspector sidebars, "Back to catalogue" and "Add to my drafts" footer actions.

## Source References Discovered During Preparation

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.css`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://src/CanDoItAll.AgentFramework.Workflows.Templates/WorkflowTemplatePack.cs`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://Templates/Workflows/workflows/default-workflows.yaml`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs`

## Component MCP Result

- Attempted to query `mcp__candoitall_components.components_search`, `component_get`, and `component_usage_examples`.
- Result: MCP transport closed.
- Fallback: use existing repo usages of BaseLib components already present on `WorkflowsPage`, especially `PageScaffold`, `PageHeader`, `PageHeaderActionButton`, `Tabs`, `Grid`, `SurfaceCard`, `Stack`, `Cluster`, `Button`, `StatusBadge`, `EmptyState`, and `Dialog`.
