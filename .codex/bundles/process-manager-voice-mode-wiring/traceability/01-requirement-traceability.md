# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001/R001 manager tab does not enable voice mode | `requirements/01-normalized-requirements.md` | `subbundles/02-process-manager-chat-voice-wiring` | `dotnet test --filter ProcessWorkspaceShell...` plus Playwright `/processes` proof | Depends on SB01 source inventory. |
| N002/R002 general sample works and Manager chat should share the voice flow | `architecture/01-target-solution.md` | `subbundles/02-process-manager-chat-voice-wiring` | Source assertions and component tests proving callbacks call `IAgentVoiceService` | Does not require changing sample playback. |
| N003/R001 specific agent allows voice mode | `inputs/02-structured-input.md` | `subbundles/02-process-manager-chat-voice-wiring` | Voice-enabled manager agent fixture with `AgentVoiceAccessMetadata.Write` | Negative disabled-agent fixture also required. |
| N004/R001 buttons still disabled | `analysis/01-current-state.md` | `subbundles/02-process-manager-chat-voice-wiring` | Failing-first transcript before implementation and passing transcript after implementation | Current source shows `CanUseVoiceMode` is omitted from Manager chat panel. |
| N005/R004 generic provider/voice-driver refactor trouble | `architecture/01-target-solution.md` | `subbundles/03-provider-runtime-voice-driver-integration` | Unit tests around `ProviderRuntimeVoiceDriver` and driver registry capability resolution | Must include STT and TTS. |
| N006/R005 real demos/tests with voice mode | `plan/01-phase-plan.md` | `subbundles/04-browser-voice-mode-demo-and-closure` | Playwright transcript, screenshots, browser analytics row, raw-note closure | Real microphone may be documented as an environment gap if permission blocks recording. |
