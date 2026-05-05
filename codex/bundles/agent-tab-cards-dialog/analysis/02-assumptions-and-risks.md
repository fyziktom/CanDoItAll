# Assumptions And Risks

## Assumptions

- The "Agents cards similar as we have in chat tab" request means the switch-agent modal card presentation should become the shared visual and behavioral pattern.
- "Assign new (or from available list)" can be satisfied by exposing available cataloged skills and MCP servers in the modal and toggling assignment there; creating entirely new capability catalog records is optional follow-up unless the existing workspace service supports a compact create path cleanly inside the dialog.
- The existing SaveAgentAsync flow is the source of truth for persisting identity, runtime, access, tags, and capability assignment.
- Route-driven `agentId` deep links should still open the selected agent editing context through the dialog in the full app layout.

## Critical Path Risks

- If the shared card component does not preserve switch-dialog selection, filtering, favorite toggling, and current-agent markers, chat regressions will invalidate the Agents tab card foundation.
- If the editor state is split into modal tabs but save sync misses workspace/process/project/capability metadata, existing technical-agent configuration may be lost.
- If route-driven agent editing no longer works from CRM-HR, cross-module technical editing flow regresses.

## Validation Risks

- Browser proof requires a healthy local app. If dotnet watch cannot start, record the blocker and still run focused component/build tests.
- Dialog visual proof must inspect the open state, not just the card grid.
- Long text-area sizing is visual and layout-sensitive; component tests can prove markup/classes but not actual rendered space.

## Reopen Triggers

- Reopen subbundle 01 if switch-dialog tests fail, card click/favorite behavior changes, or the Agents tab uses a different card implementation.
- Reopen subbundle 02 if any technical editor field no longer saves, if capability assignment cannot be updated from the modal, or if double-click does not open DialogService.
- Reopen subbundle 02 if Summary or Instructions still render in constrained columns instead of full available modal width.
- Reopen subbundle 03 if browser proof cannot show both card grid and open dialog states.
