# Current State

## Workflows Page

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor` currently exposes a primary `Templates` tab after `Editor`.
- The Templates tab renders:
  - a `SurfaceCard` with template pack count, seed version, and template list
  - a `SurfaceCard` with LLM component library details
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs` keeps `private WorkflowTemplatePack? templatePack`.
- `EnsureTemplatePackLoadedAsync()` calls `TemplatePackLoader.Load()` and is invoked when `WorkflowTabRequiresTemplatePack(index)` returns true for the Templates tab.
- `WorkflowTabRequiresComponentLibrary(index)` currently treats the Templates tab as a component-library-loading trigger.

## Existing Draft Creation

- `CreateStarterWorkflowAsync()` creates a starter component and workflow directly as a draft.
- No current UI flow copies a template into user drafts.
- Template seeding creates managed active examples through `WorkflowExampleCatalogSeedService`, but the requested "Add to my drafts" behavior should create user-owned drafts, not managed seed examples.

## Existing Preview Surfaces

- `WorkflowCanvasEditor` is already used on the Editor tab.
- The request wants a preview dialog "with workflow canvas"; the likely implementation should reuse `WorkflowCanvasEditor` or its underlying canvas model rather than inventing a second renderer.
- Any preview-only use must avoid accidentally saving edits from the preview dialog.

## Existing Tests

- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs` already covers:
  - Templates tab lists examples
  - Workflow seeding creates examples
  - Managed seeding preserves non-managed definitions with template names
  - Canvas editor behavior
- The Templates tab test must be replaced with catalogue-dialog behavior.
- The seeding tests must be updated when SEAMARK template names become generic.

## SEAMARK-Specific Templates

- `repo://Templates/Workflows/workflows/default-workflows.yaml` contains two SEAMARK templates:
  - `seamark-xray-device-folder-summary`
  - `seamark-price-list-extraction`
- These names, descriptions, routing instructions, LLM node names, and output asset titles include company-specific details and exact price facts.
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs` also contains SEAMARK test labels and a SEAMARK test folder path.

## Design Proposal Artifacts

- Catalogue proposal: `bundle://evidence/design/template-catalogue-dialog-proposal.png`.
- Preview proposal: `bundle://evidence/design/template-preview-dialog-proposal.png`.
- These designs are planning aids only; real app screenshots remain required proof.
