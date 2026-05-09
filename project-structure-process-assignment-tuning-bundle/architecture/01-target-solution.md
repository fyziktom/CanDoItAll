# Target Solution

## UI Structure

- Keep `ProjectStructureProcessAssignmentDialog` as the single fullscreen staffing surface.
- Use `selectedRoleId == null` for `All` summary mode.
- Add a rail `All` button before filtered role rows.
- Split the workspace:
  - Summary mode renders the existing role-card grid, enhanced with metadata badges.
  - Role mode renders a role header, candidate ranking grid, and final plus-card directory action.

## Candidate Metadata

- Extend `ProjectStructureProcessStartCandidateState` with optional agent catalog fields:
  - provider name
  - model
  - role title
  - summary
  - status
  - workload
  - avatar url
  - tool names
  - skill names
- Refresh agent/provider metadata before launch-plan state is mapped into the dialog.
- Fall back gracefully when a candidate has no technical agent match or provider metadata.

## Details Dialog

- Add a small readonly Workbench component for assignment-time agent details.
- Open it through existing `DialogService` from the assignment component.
- Use `ModalSize.Wide` and a higher z-index test-id rule so it appears above the fullscreen assignment overlay.

## Validation

- Extend component tests to cover `All`, role-specific ordering, plus-card callback, metadata badges, and details action.
- Reuse existing `AgentChatModalTests` for picker behavior.
- Capture browser screenshots for summary, role drilldown, picker, details, and tooltip state.
