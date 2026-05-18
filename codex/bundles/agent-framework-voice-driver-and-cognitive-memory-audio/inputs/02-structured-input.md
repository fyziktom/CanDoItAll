# Structured Input

## Objectives

- Add provider-neutral voice contracts and a first OpenAI-backed implementation for TTS and STT.
- Keep voice integration inside AgentFramework/MAF boundaries rather than placing provider calls in Blazor components.
- Persist general voice settings for the agent module and per-agent voice access metadata.
- Add normal and contextual agent chat audio mode.
- Add Cognitive Memory probe voice dialogue with explicit confirmation before storing correction/proposal feedback.

## Hard Constraints

- Do not mutate canonical Cognitive Memory truth directly from voice or probe chat. New memory-worthy corrections must use existing probe feedback/review-gated mutation paths.
- Do not store raw API keys in agent definitions or browser state. Use existing provider secret/environment credential resolution.
- Do not add silent fallback between drivers. Missing driver, missing provider, missing key, failed transcription, or failed synthesis must surface a predictable error.
- Keep local-model support possible through interfaces and a factory keyed by a strongly typed driver enum.
- Normal chat and floating contextual chat must use shared UI wiring where possible.
- Use existing components and styles; do not introduce a new frontend stack.

## Assumptions

- The first implementation can use non-realtime OpenAI REST audio endpoints. True realtime duplex audio can be added later behind the same driver contracts.
- Browser recording can use MediaRecorder and send a completed audio blob to the Blazor server for STT.
- TTS playback can request synthesized audio after the assistant/probe response and play it in the browser.
- General voice settings can live with existing AgentFramework persisted JSON settings to avoid adding a migration-only settings table for this phase.

## Risks

- Audio permissions and MediaRecorder formats vary by browser. The proof must use a supported browser path and show graceful failure if recording is unavailable.
- OpenAI TTS has disclosure policy requirements. The UI must make AI-generated audio status visible in settings or mode controls without hiding it in code comments.
- Cognitive Memory confirmation grammar is ambiguous. The implementation should recognize a small affirmative/negative phrase set first, then leave semantic expansion to a later local/semantic intent classifier.
- Long memory corrections can exceed STT/TTS practical limits. Keep recording size capped and truncate spoken summaries to a bounded prompt/response size.

## Validation Expectations

- Unit tests cover metadata normalization, driver factory selection, settings normalization, affirmative confirmation detection, and OpenAI driver request construction without calling the network.
- Component tests cover voice controls in the shared chat panel and per-agent voice settings.
- Build/test proof includes targeted unit/component tests plus a solution build for compile integration.
- Browser proof covers `/agents?tab=chat`, the contextual/floating project-structure chat path, and `/cognitive-memory?projectId=...` probe audio controls.
