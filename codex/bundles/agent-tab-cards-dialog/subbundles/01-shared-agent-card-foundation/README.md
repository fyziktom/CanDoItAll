# Shared Agent Card Foundation

## Status

- `Completed`

## Objective

- Make `AgentSelectionCard` the reusable card used by both the chat switch-agent modal and the Agents tab.

## Covered Inputs

- N002: "Hero of that tab will be Agents cards similar as we have in chat tab in switch agent modal. Ideal is to use same component for both."

## Prerequisites

- Prepared bundle readiness gate passes.
- Existing switch-agent modal behavior is understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSelectionCard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs`

## Deliverables

- `AgentSelectionCard` supports card selection, optional double-click callback, favorite affordance, tags, selected/current marker, status/workload/chat/capability metadata, and optional details tooltip.
- `AgentSwitchDialog` renders `AgentSelectionCard` instead of duplicating agent-card markup.
- Existing switch-agent filtering and favorite behavior remains intact.

## Dependency Impact

- Subbundle 02 depends on this shared card API and CSS. Weak proof here invalidates the Agents tab card grid and risks chat modal regression.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Extend `AgentSelectionCard` with parameters needed by the switch dialog and Agents tab.
2. Add component-scoped CSS if needed so the card owns its shared visual contract.
3. Refactor `AgentSwitchDialog` to use the shared component and remove duplicate card shell markup.
4. Run switch-dialog component tests and repair any selection/favorite/filter regressions.

## Scope Exceptions

- New capability creation is not part of this subbundle.

## Do Not Do

- Do not change agent persistence.
- Do not alter chat-thread switching semantics.
- Do not introduce a second Agents-tab-only card.

## Acceptance Checklist

- `AgentSwitchDialog` markup contains `AgentSelectionCard`.
- Switch-dialog cards still show selected/current agent, favorites, tags, status, summary, and details.
- Favorite toggling still promotes the agent in the sorted list.
- Search and tag filters still work.

## Proof Required

- Focused component tests covering `AgentSwitchDialog`.
- Source inspection that both switch dialog and later Agents tab use `AgentSelectionCard`.

## Browser Validation Logging

- Route: `/agents?tab=chat` or any chat surface that can open Switch Agent.
- Viewports: large desktop if browser proof is available in closure.
- Actions: open switch-agent dialog, inspect card grid, toggle favorite if feasible.
- Screenshots: record in `reviews/01-execution-report.md` if captured.
- Review questions: no text clipping, card actions reachable, favorite button not nested in invalid interactive markup.

## Progression Gate

- Downstream subbundle may start only after switch-dialog tests pass and the shared card is ready for Agents tab use.

## Suggested Agent Prompt

```text
Implement only the shared AgentSelectionCard foundation and switch-dialog refactor. Preserve switch dialog behavior and record proof before moving on.
```
