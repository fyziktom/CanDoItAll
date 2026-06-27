# Assumptions And Risks

## Assumptions

- Manager chat should follow the same voice-mode semantics as `AgentChatPanel`: if voice mode is enabled, a successful assistant response is spoken automatically.
- Selected-run usage telemetry should be available to Manager chat from the process runtime projection without loading full runtime history or artifact details.
- It is acceptable for auto-speak to run for typed messages while voice mode is enabled; that is consistent with other voice chat surfaces.

## Critical Path Risks

- Loading usage telemetry in Manager chat must remain bounded. If the projection service loads usage for too many runs, this needs rework before closure.
- Prompt enrichment must be clear enough for the model to answer from metrics rather than repeat the old "not included" response.

## Validation Risks

- Full microphone capture is environment-permission dependent. The reliable proof is component-level transcription/synthesis wiring plus browser proof that the tab and controls load after restart.
- Real token/cost values depend on available runtime usage observations in the local database.

## Reopen Triggers

- Manager tab still requests `IncludeUsageTelemetry = false`.
- Cost/token prompt sent to the agent lacks an explicit selected-run usage section.
- Auto-speak test proves synthesis is not invoked after a successful voice-mode send.
