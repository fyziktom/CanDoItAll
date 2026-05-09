# Current State

## Process Start Dialog

- `ProjectStructurePage.Processes.cs` owns `processStartDialog`, creates a launch plan on the first Continue action, maps `ProcessLaunchPlanDetails` into `ProjectStructureProcessStartDialogState`, and starts the process only after required roles are resolved and `AssignmentsReviewed` is true.
- `ProjectStructureCanvasDialogs.razor` renders the staffing stage inside `ProjectStructureOverlayDialog` as stacked `PanelCard` sections.
- The current modal title is role-target specific, for example `Assign roles for {TargetNodeTitle}`, and the action button is `Start`.
- The current staffing UI has summary pills, a review checkbox, optional HR confirmation text, role cards, and candidate cards.

## Agent Switcher

- `AgentSwitchDialog.razor` already provides search, tag filtering, favorites-first ordering, favorite toggling, and compact `AgentSelectionCard` rendering.
- `AgentChatPanel.razor.cs` shows the expected integration pattern: load `IAgentFrameworkWorkspaceService.ListAgentsAsync`, call `DialogService.OpenAsync<AgentSwitchDialog>`, and pass a `FavoriteToggled` callback that updates `AgentSpecialTags.Favorite`.
- Existing component tests in `AgentChatModalTests.cs` cover search/tag filtering and favorites-first behavior.

## Shared Components

- The CanDoItAll component MCP confirms `Grid`, `Stack`, `Split`, and `Cluster` are the preferred layout primitives for this surface.
- Custom CSS is still required for the full-screen shell, role-sidebar sizing, assignment-card treatments, grid responsiveness, and bottom selected-agent detail panel.

## Test/Proof Surfaces

- Component tests can render `ProjectStructureCanvasDialogs` with a synthetic staffing state and assert the new semantic/test-id structure.
- Browser proof should use the real application route for project structure process start when available, or a deterministic test route/seeded state if the full route is too costly to reach.
