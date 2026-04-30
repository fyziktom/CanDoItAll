# 01-agent-chat-thread-switch-polish

## Status

- `Completed`

## Objective

- Polish the Agents chat thread rail and switch-agent modal according to the screenshot feedback.

## Covered Inputs

- Left thread card must fit the rail.
- Thread preview must be shorter with tooltip detail.
- Thread title must be editable with `Editable`.
- Switch-agent modal must search by name and filter by tags with `TagEditor`.
- Favourite star must persist through an internal tag and sort favourites first.

## Prerequisites

- Previous Agents chat two-column layout is present.
- Shared BaseLib `Editable`, `TagEditor`, `TooltipTarget`, `DialogService`, `Stack`, and `Grid` are available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`

## Deliverables

- Rail-safe thread card rendering.
- Editable selected thread title.
- Persisted chat-session rename service method.
- Switch-agent search and tag filter controls.
- Persisted favourite star backed by internal tag.
- Focused component tests and browser proof.

## Dependency Impact

- `IAgentFrameworkWorkspaceService` changes affect the workspace facade and any test doubles.
- `AgentDefinition.Tags` semantics gain one internal reserved tag.
- The modal remains self-contained and does not change the agent settings editor.

## Validation Depth

- Critical UI foundation with service persistence.

## Implementation Steps

1. Add internal favourite tag constant.
2. Add chat-session rename API and wire the editable header.
3. Replace the left thread list card with compact local markup and tooltip preview.
4. Add switch-agent modal search, tag filter, and favourite star behavior.
5. Add focused tests.
6. Run build, focused tests, Playwright interactions, and screenshot review.

## Do Not Do

- Do not add favourites to the normal agent settings tag editor.
- Do not leave raw long previews visible in the left rail.
- Do not use nested buttons inside selectable agent cards.

## Acceptance Checklist

- Thread card stays inside the left panel.
- Long preview text is clipped in the card and available through tooltip.
- Header title can be edited and persisted.
- Agent modal filters by search text.
- Agent modal filters by selected visible tags.
- Favourite star toggles and favourites sort first.

## Proof Required

- `dotnet build CanDoItAll.slnx --no-restore`
- Focused component tests for chat workspace and switch-agent modal.
- Playwright screenshot of main chat page.
- Playwright screenshot of switch-agent modal with search/filter/favourite UI.

## Browser Validation Logging

- Route: `/agents?tab=chat`
- Viewports: large desktop first.
- Actions: open Agents chat page, inspect left card, open switch-agent modal, filter/search, toggle favourite, inspect sorting, hover tooltip where practical.
- Screenshots: store outside tracked source or under bundle evidence if explicitly needed.

## Progression Gate

- Final closure approved: build/tests passed and screenshot review found no clipping, overflow, or modal layering issue.

## Suggested Agent Prompt

```text
Implement the Agents chat polish subbundle with shared BaseLib components and capture Playwright proof of the page and switch-agent modal.
```
