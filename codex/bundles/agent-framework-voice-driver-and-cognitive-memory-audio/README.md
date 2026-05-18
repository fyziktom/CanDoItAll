# AgentFramework Voice Driver And Cognitive Memory Audio

This bundle coordinates the AgentFramework voice driver, agent chat audio mode, and Cognitive Memory probe voice dialogue.

## Profile

- `initiative`

## Mission

Add provider-neutral text-to-speech and speech-to-text support to the AgentFramework MAF wrapper with OpenAI as the first driver, then expose audio mode in agent chat and Cognitive Memory probing without bypassing existing agent permissions or memory review gates.

## Outcome Contract

- Requested outcome: voice driver project, general/per-agent voice settings, normal/floating chat audio mode, and Cognitive Memory probe audio dialogue.
- Hard constraints: strongly typed driver interfaces/factory, secure credential resolution, no silent fallback, no raw key persistence, no direct Cognitive Memory truth mutation from voice.
- Evidence required before closure: unit/component tests, solution build, browser validation analytics for chat/probe UI, and final bundle validator pass.
- Known blockers or explicit scope exceptions: local voice model drivers and realtime duplex streaming are deferred; live OpenAI sample playback requires configured credentials.

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

1. `subbundles/01-01-voice-driver-core`
2. `subbundles/02-02-agent-settings-and-chat-audio`
3. `subbundles/03-03-cognitive-memory-voice-dialogue`

## Dependency And Validation Map

Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Ready after validation`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Completed`
