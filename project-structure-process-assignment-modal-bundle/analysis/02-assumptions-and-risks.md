# Assumptions And Risks

## Assumptions

- The staffing-stage launch plan is already created before the redesigned assignment UI is shown.
- Role order in the screenshot maps to existing `ProcessLaunchPlanRole.DisplayOrder` order as projected by `launchPlan.Roles`.
- The `Save and close` action can safely close the modal without starting the process because launch-plan edits are already persisted when candidates are selected.
- `Review and start` maps to the existing final start action and should remain disabled until required roles are resolved and reviewed.
- The manual agent picker can first attempt to select an existing launch candidate by `TechnicalAgentId`, then create or select a safe manual candidate only if needed.

## Critical Path Risks

- If the manual picker only selects candidates already in the launch plan, the user requirement to add a specific agent manually is only partially solved.
- If `ProjectStructureOverlayDialog` inline styles cannot be overridden cleanly, the fullscreen design may require a scoped overlay mode parameter rather than CSS-only changes.
- If the assignment modal body uses too many nested cards, it will miss the design intent and become hard to scan.

## Validation Risks

- Component tests can prove structure but cannot prove visual correspondence to the screenshot.
- Browser proof must capture both the full-screen assignment modal and the agent switcher modal opened from an assignment action.
- The screenshot comparison is visual and qualitative, so the execution report must answer explicit visual questions instead of only attaching images.

## Reopen Triggers

- Reopen subbundle 01 if browser proof shows the modal is not full-screen, header actions wrap badly, role cards overlap, or the bottom detail panel hides essential role actions.
- Reopen subbundle 02 if a manually selected agent cannot be persisted as the selected process-role candidate.
- Reopen subbundle 03 if screenshots do not show both desktop and narrower modal states or if the manual picker open-state proof is missing.
