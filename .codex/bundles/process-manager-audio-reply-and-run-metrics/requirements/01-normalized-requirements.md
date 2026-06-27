# Normalized Requirements

| Id | Requirement | Observable success |
| --- | --- | --- |
| R001 | Manager chat must auto-speak the assistant response after a successful send while Manager voice mode is enabled. | Component test proves `IAgentVoiceService.SynthesizeChunksAsync` is called with the assistant response and JS audio queue calls are issued. |
| R002 | Manual read behavior must remain available and voice access restrictions must still apply. | Existing voice-control tests continue to pass. |
| R003 | Manager chat projection loading must include selected-run usage telemetry needed for cost/token answers. | Component test proves the Manager tab request sets `IncludeUsageTelemetry = true` while keeping broad history/active-agent loading disabled. |
| R004 | Manager chat prompt must include selected-run usage metrics when the projection has them. | Component test proves the prompt sent to the workspace service contains cost and token fields. |
| R005 | The prompt/tool policy must not disable runtime tools for cost/token questions when metrics may need runtime lookup. | Unit test proves cost/token prompt keeps runtime tools enabled unless the wording explicitly says to use only the preloaded context. |
