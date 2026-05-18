# Agent Voice TTS Readable Speech Filter

This bundle coordinates the follow-up improvement for AgentFramework voice output: make spoken responses shorter and less annoying by omitting machine identifiers from TTS while preserving them in the visible text answer.

## Profile

- `feedback`

## Mission

Before text is sent to text-to-speech, remove full GUIDs and safe truncated identifier fragments from the spoken payload, then optionally add a concise one-time notice that exact IDs were skipped and remain available in the text response.

## Outcome Contract

- Requested outcome: agent chat and floating contextual chat should speak readable answers without reading long IDs aloud.
- Hard constraints: visible text responses must remain unchanged; ID removal must happen only for TTS; full GUID removal is required; shortened ID removal must be conservative; no provider-specific implementation should be baked into OpenAI only.
- Evidence required before closure: sanitizer unit tests, voice service/unit coverage for notice suppression, chat/floating chat wiring proof, solution build, and targeted component tests.
- Known blockers or explicit scope exceptions: natural-language detection of every arbitrary shortened identifier is intentionally not attempted. This bundle only covers full GUIDs and identifier-looking truncated hexadecimal values with explicit ellipsis.

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
- `evidence/` browser screenshots and live validation notes captured during execution

## Recommended Execution Order

1. `subbundles/01-01-tts-speech-text-sanitizer`
2. `subbundles/02-02-chat-voice-notice-state-and-proof`
3. Final validation and closure

## Dependency And Validation Map

Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Ready after validation`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
