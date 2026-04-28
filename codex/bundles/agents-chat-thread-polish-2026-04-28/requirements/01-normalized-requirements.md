# Normalized Requirements

- R1: Thread cards in the Agents chat left rail must fit within the rail without horizontal overflow or clipping.
- R2: Thread card previews must be shortened in the card, with a TooltipService-backed path to the longer preview.
- R3: The selected thread title must be editable with the shared `Editable` component and persisted to the chat session.
- R4: The switch-agent modal must support text search over agent identity fields.
- R5: The switch-agent modal must support tag filtering with the shared `TagEditor` component and must not add tags to agents from this filter.
- R6: The switch-agent modal must support marking agents as favourite with a star icon backed by an internal tag.
- R7: Favourite agents must sort first in the modal, including after text or tag filtering.
- R8: The implementation must pass build, focused component tests, and Playwright screenshot review.
