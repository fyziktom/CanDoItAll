# QA Prompt

Review the implemented voice feature as a regression-focused engineer.

Check:

- Driver factory selects exactly the configured driver and fails predictably otherwise.
- Provider credentials are resolved through secrets/environment/config and are not serialized into settings, agent metadata, logs, or browser payloads.
- STT and TTS can be enabled independently but can share one OpenAI provider profile.
- Per-agent voice access prevents audio mode when disabled.
- Per-agent voice override wins over general voice settings.
- Normal chat and contextual floating chat share the same audio controls and do not diverge behaviorally.
- Cognitive Memory voice correction cannot store without explicit confirmation and never bypasses review-gated feedback.
- Browser UI has no clipped controls, overlapping text, hidden state, or inaccessible icon-only actions.

Required proof review:

- Targeted unit and component test output.
- Solution build output.
- Browser validation analytics rows with screenshots for normal chat, floating chat, and Probe workbench.
- Execution report gate rows for all subbundles.
