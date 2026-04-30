# Assumptions And Risks

## Working Assumptions

- The internal favourite marker can be stored in `AgentDefinition.Tags` as a constant value and hidden from user-facing tag filters.
- Inline title editing belongs in the chat header because the screenshot marks the selected thread title there.

## Critical Path Risks

- Adding a chat-session rename API touches the shared workspace service interface and can break compile if every facade is not updated.
- A nested favourite button inside a selectable card would create invalid button markup, so the card structure must be adjusted carefully.

## Validation Risks

- CSS isolation can prevent left-rail clipping fixes from reaching nested shared components.
- Browser proof must include the modal open state because the request changes dialog filtering and favourite UI.

## Reopen Triggers

- The thread card still overflows horizontally or clips under the center chat workspace.
- Favourite toggles are visible only locally and do not persist after reopening the modal.
- Tag filtering edits agent settings instead of acting as a temporary modal filter.
