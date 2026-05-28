# Assumptions And Risks

## Assumptions

- The user's intent was to unblock the process, not merely send a chat message to the manager agent.
- The correct unblock action for the observed escalation is governed rework/rerun, not approval.
- A true approval can only be continued when the escalation carries a source execution run id and external approval id.

## Critical Path Risks

- If escalation action routing is wrong, the UI can either keep creating stuck manager-chat runs or hide a required human decision.
- If the live app is not restarted after build, port 5032 will continue serving the old behavior.

## Validation Risks

- Browser proof depends on the running app accepting a restart and serving the Live Processes route.
- The reported run may progress asynchronously after rework is requested, so closure must distinguish "action queued" from "entire business process completed".

## Reopen Triggers

- Reopen if a blocked-step escalation still shows `Approve`.
- Reopen if a true approval-required escalation cannot continue a source execution run when valid source ids are available.
- Reopen if tests pass but live 5032 validation shows another manager-chat quick-decision run is created for the blocked escalation.
