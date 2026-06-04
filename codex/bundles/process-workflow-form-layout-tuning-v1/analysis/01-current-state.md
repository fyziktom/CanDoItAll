# Current State

## Processes Steps Surface

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceStepsTab.razor` renders the Steps tab and repeats `ProcessStepEditorForm` once per step.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor` is one long `SurfaceCard` with a single vertical `Stack`. It mixes identity, execution policy, dependencies, operation contract, notes, contract summaries, branch outcomes, role assignments, and artifact expectations.
- `ProcessStepEditorForm.razor` already uses shared components such as `SurfaceCard`, `Stack`, `Split`, `Grid`, `FormField`, `Cluster`, `StatusBadge`, `FormSection`, `Button`, `EmptyState`, and child editor components.
- `ProcessStepBranchOutcomeEditor.razor`, `ProcessStepRoleAssignmentEditor.razor`, and `ProcessArtifactExpectationEditor.razor` are separate setup forms for step routing, role bindings, and artifact expectations. They are usable but inherit the parent long-stack problem because all collections are shown in the same scroll.

## Workflows Editor Surface

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor` exposes the `/agents/workflows` route and renders the Editor tab with `WorkflowCanvasEditor`.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor` has a canvas stage plus an inspector. The inspector currently stacks definition fields, selected-node fields, executor settings, edge editor, validation, and preview input together.
- The workflow inspector already uses shared components (`SurfaceCard`, `CanvasWorkbenchStage`, `Grid`, `Stack`, `Cluster`, `Button`, `TextBlock`, `StatusBadge`, `EmptyState`, `Dialog`). It also has component-local CSS for field and panel treatment.
- The same file has a full node-details dialog that is already sectioned; the main inspector is the primary layout issue.

## Shared Component Grounding

- `repo://src/CanDoItAll.AppComponents/Components/Tabs.razor` supports `SelectedIndex`, `SelectedIndexChanged`, `RenderMode`, `TabsItem`, and variants passed through attributes. Existing pages use it for workspace and modal tab grouping.
- `repo://src/CanDoItAll.AppComponents/Components/FormField.razor` wraps labels and controls consistently.
- `repo://src/CanDoItAll.AppComponents/Components/Stack.razor` is used for one-dimensional layout.
- `Grid` is available through the shared component library and heavily used across app modules for form field groups.

## Proposal Artifacts

- Eight imagegen proposals were generated and copied under `bundle://evidence/imagegen-proposals/`.
- They propose tab splits for process step Basic info, Contracts, Routing/Roles/Artifacts, compact role assignment, compact artifact expectation, workflow inspector Node, workflow inspector Executor, and workflow inspector Routes.

## Implementation Implication

- The smallest correct change is Razor layout/state refactoring in the two main components plus compacting child editors where they contribute to the ugly layout.
- No service, persistence, runtime, or domain model changes are required.
