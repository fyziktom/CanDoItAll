# Process Manager Voice Mode Wiring

This bundle is a coordination and execution package for `process-manager-voice-mode-wiring`.

## Profile

- `initiative`

## Mission

- Restore voice mode in the Processes page Manager chat tab and prove the shared AgentFramework voice path still connects agent voice-access metadata, general voice settings, provider runtime drivers, browser microphone capture, transcription, and text-to-speech after the provider refactor.

## Outcome Contract

- Requested outcome: Manager chat voice buttons are enabled for a selected manager agent whose `AgentVoiceAccessSettings.CanUseVoiceMode` is true, and voice record/toggle/speak actions flow through the same typed voice service used by the normal agent chat and contextual windows.
- Hard constraints: keep the shared `ChatWorkspacePanel` as the control surface; do not introduce stringly typed voice switches; keep provider-specific audio behavior behind `IAgentVoiceService`, `IAgentVoiceDriverFactory`, and provider runtime driver abstractions; fail explicitly when settings or provider capability are wrong.
- Evidence required before closure: failing-first component proof for the disabled manager-chat buttons, passing component proof, provider runtime voice driver tests, anti-stub/source audit, build/test transcripts, and Playwright browser proof for `/processes` Manager chat voice controls.
- Known blockers or explicit scope exceptions: real microphone permission and external OpenAI calls may be environment-dependent; if unavailable, browser proof must still validate enabled controls and JS interop paths, while unit tests prove provider dispatch with test drivers.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-voice-eligibility-source-inventory`
2. `subbundles/02-process-manager-chat-voice-wiring`
3. `subbundles/03-provider-runtime-voice-driver-integration`
4. `subbundles/04-browser-voice-mode-demo-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed: scripts/validate_bundle.py --stage prepared --profile initiative`
- Execution status: `Completed`
- Subbundle gate review: `SB01, SB02, SB03, and SB04 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
