# Target Solution

## UI Structure

- Keep `ProjectStructureCanvasDialogs` as the rendering owner for project-structure overlay dialogs.
- Add a full-screen mode for the process staffing stage, using the existing overlay host for modal behavior and scoped CSS for layout.
- Use shared `Grid`, `Stack`, `Split`, and `Cluster` components for structure where practical.
- Add page-local CSS only for the assignment shell, role rail, card grid, role card states, empty assignment panel, and bottom detail panel.

## Assignment State

- Extend the process-start dialog state only where required for UI selection and manual agent assignment.
- Keep `ProcessLaunchPlanDetails` as the persisted source of truth.
- Continue to use `ProcessesService.SelectLaunchCandidateAsync` when the selected agent already maps to a launch candidate.
- If needed, add a backend method that creates or reuses a manual technical-agent launch candidate, selects it, updates role resolution summaries, and returns the reloaded launch plan.

## Manual Agent Picker

- Inject `IAgentFrameworkWorkspaceService` and `DialogService` into `ProjectStructurePage.Processes.cs`.
- Open `AgentSwitchDialog` with the same parameters used by chat: `Agents`, `SelectedAgentId`, and `FavoriteToggled`.
- Filter or handle the returned `AgentDefinition.Id` by matching `ProjectStructureProcessStartCandidateState.TechnicalAgentId` if present, or by creating/selecting a manual candidate.

## Proof Plan

- Add or update component tests for the full-screen process assignment modal and manual picker callback/test ids.
- Add service-level coverage if a manual technical-agent launch candidate method is introduced.
- Run targeted tests, then a real browser validation pass with screenshots.
