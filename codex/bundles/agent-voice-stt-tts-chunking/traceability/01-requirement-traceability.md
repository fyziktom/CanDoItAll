# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 / R005: long STT and TTS must split to chunks. | `requirements/01-normalized-requirements.md` | `subbundles/01-voice-chunking-core` | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentVoiceTests` | STT chunking must use ordered media chunks, not raw byte slicing. |
| N002 / R001: TTS chunk size must stay well below the API limit. | `architecture/01-target-solution.md` | `subbundles/01-voice-chunking-core` | Unit tests verify conservative chunk budget. | Exact token counting is an explicit non-goal until a tokenizer abstraction exists. |
| N003 / R002: TTS should split by sentence or few sentences. | `requirements/01-normalized-requirements.md` | `subbundles/01-voice-chunking-core` | Text chunker unit tests. | Long single sentences may split on whitespace within the budget. |
| N004 / R003: playback starts while later sentences are still coming. | `architecture/01-target-solution.md` | `subbundles/02-progressive-playback-integration-and-closure` | Unit tests plus browser validation row or explicit host blocker. | Browser queue must avoid overlapping chunks. |
| N005 / R007: generic driver function across the app. | `architecture/01-target-solution.md` | `subbundles/01-voice-chunking-core`, `subbundles/02-progressive-playback-integration-and-closure` | Repository search proves voice callers use shared service behavior. | Blazor components must not know OpenAI limits. |
