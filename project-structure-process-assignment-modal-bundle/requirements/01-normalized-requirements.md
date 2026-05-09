# Normalized Requirements

## Requirements

- R-001: When a project-structure process node Start action reaches staffing review, the role assignment UI opens as a full-screen modal.
- R-002: The full-screen modal header matches the design structure: process-assignment eyebrow, `Assign AI agents to process roles` title, helper copy, assigned-role progress text/bar, and `Cancel`, `Save and close`, `Review and start` actions.
- R-003: The modal includes a left role rail with search, a filter affordance, role count, role rows, assigned state, unresolved `Assign` action, and an HR help action.
- R-004: The main workspace shows process roles as ordered assignment cards with required labels, resolved/selected visuals, recommended candidate badges, empty assignment panels, and per-role `Assign agent` or `Change agent` commands.
- R-005: The selected agent detail panel appears at the bottom of the main workspace and summarizes the current assignment with avatar, selected state, score, capabilities/model metadata, profile action, and recommendation reasons.
- R-006: Manual assignment uses the existing `AgentSwitchDialog`/`AgentSelectionCard` behavior from chat so search, tag filtering, favorites-first sorting, and favorite toggling remain available.
- R-007: Candidate assignment persists through the existing process launch plan, updates role resolved/gap state, and preserves the required-role start gate.
- R-008: The design remains readable at large desktop and narrower widths without overlapping text, hidden actions, or clipped dialogs.
- R-009: Browser proof includes screenshots of the implemented full-screen modal and the manual agent picker open from the assignment flow.

## Non-Goals

- Do not redesign process definition editing, process workspace launch planning, or the chat page switcher.
- Do not change process runtime execution semantics except what is necessary to persist manual agent selection safely.
- Do not add decorative marketing copy or tutorial text to the modal.
