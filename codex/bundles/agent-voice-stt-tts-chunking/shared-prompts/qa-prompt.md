# QA Prompt

Review the implementation against the raw request, not only against the code diff.

Check these outcomes:

- Long TTS is split after speech text preprocessing and before provider calls.
- TTS chunking is sentence-oriented and conservatively below the provider cap.
- Progressive callers enqueue each chunk as it arrives.
- STT accepts ordered recording chunks and preserves transcript order.
- Provider-specific limits are not duplicated in Blazor components.
- Existing short TTS/STT paths still work.
- Failed chunks fail explicitly.

Required evidence:

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentVoiceTests`.
- Browser proof for the voice UI route if the app can be hosted locally.
- Execution report rows for subbundle gates, browser analytics, raw-note closure, and residual risk.

Mark any missing live OpenAI or microphone proof as a validation gap only if unit tests and browser wiring proof are otherwise complete.
