# Assumptions And Risks

## Assumptions

- The manager agent selected in the Processes Manager chat tab is a normal `AgentDefinition` and its `ConfigurationJson` contains the same voice-access metadata used elsewhere.
- A real environment may already have valid voice provider settings, but automated proof should avoid relying on external OpenAI calls unless explicitly available.
- Existing user worktree edits are intentional and must not be reverted.

## Critical Path Risks

- SB01 and SB02 are critical foundations. If the source inventory is wrong or the Manager chat panel is not the disabled button owner, downstream browser proof can produce a shallow pass.
- SB03 is a critical foundation for generic provider trouble. If STT/TTS provider runtime dispatch is broken, enabling buttons in SB02 is insufficient.
- SB04 is final closure. If browser proof cannot open the real Manager chat route, the raw "real demos/tests" request remains partially solved.

## Validation Risks

- Browser microphone permission may require manual acceptance; Playwright proof may need a fake-media stream or a documented host/browser permission gap.
- Test data may not contain a voice-enabled manager agent by default; component tests must seed one directly, and browser proof may need scenario seeding.
- Provider tests using fake drivers prove typed dispatch and capability wiring, not live OpenAI account health.

## Reopen Triggers

- If SB02 tests show `ChatWorkspacePanel` receives voice state but buttons remain disabled, reopen SB01 because another shared-panel condition is controlling the disabled state.
- If SB03 shows driver factory or provider runtime cannot resolve STT/TTS despite existing tests, reopen SB02 only after the provider contract is repaired.
- If browser proof shows `/agents` voice works but Manager chat remains disabled, reopen SB02.
- If browser proof shows controls enabled but recording/speak click does not invoke JS/voice callbacks, reopen SB02 and add callback wiring proof.
