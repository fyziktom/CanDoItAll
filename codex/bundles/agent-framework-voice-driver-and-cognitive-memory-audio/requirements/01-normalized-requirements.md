# Normalized Requirements

| Id | Requirement | Source | Owner |
| --- | --- | --- | --- |
| R-001 | Add an AgentFramework voice project to the solution with provider-neutral TTS/STT interfaces, request/result records, a strongly typed driver enum, and an exact-driver factory. | `inputs/00-original-request.md` | `subbundles/01-01-voice-driver-core` |
| R-002 | Implement the first OpenAI voice driver using official OpenAI audio endpoints for transcription and speech synthesis. | `inputs/00-original-request.md`, `inputs/01-source-artifacts.md` | `subbundles/01-01-voice-driver-core` |
| R-003 | Resolve OpenAI credentials through the existing provider profile secret/environment/config path without storing raw keys in agent JSON or browser state. | `inputs/02-structured-input.md` | `subbundles/01-01-voice-driver-core` |
| R-004 | Persist general agent-module voice settings for STT driver, TTS driver, connection/provider selection, STT model, TTS model, default voice, sample text, and whether each capability is enabled. | `inputs/00-original-request.md` | `subbundles/02-02-agent-settings-and-chat-audio` |
| R-005 | Add UI to configure general voice settings and play a selected voice sample. | `inputs/00-original-request.md` | `subbundles/02-02-agent-settings-and-chat-audio` |
| R-006 | Add per-agent settings that allow or deny voice mode and optionally select a voice override that takes priority over the general voice. | `inputs/00-original-request.md` | `subbundles/02-02-agent-settings-and-chat-audio` |
| R-007 | Add voice mode controls to normal agent chat and floating contextual/project-structure chat. Voice mode must support recording input, transcription, send, response synthesis, and audio playback. | `inputs/00-original-request.md` | `subbundles/02-02-agent-settings-and-chat-audio` |
| R-008 | Audio feature failures must be explicit and actionable. Do not silently fall back to text-only, a different provider, or a different model. | `inputs/02-structured-input.md` | `subbundles/01-01-voice-driver-core`, `subbundles/02-02-agent-settings-and-chat-audio` |
| R-009 | Add Cognitive Memory probe voice controls for asking questions, receiving spoken answers/status, and submitting memory-worthy corrections by voice. | `inputs/00-original-request.md` | `subbundles/03-03-cognitive-memory-voice-dialogue` |
| R-010 | When the operator says something that should be stored, Cognitive Memory must speak a wait/processing status, create a review-gated interpretation using existing probe feedback paths, summarize how it understood the memory, and wait for explicit affirmative confirmation before saving the feedback/proposal. | `inputs/00-original-request.md` | `subbundles/03-03-cognitive-memory-voice-dialogue` |
| R-011 | Confirmation grammar must support clear affirmative phrases such as "yes", "ok", "okay", and "this is good, store it", and clear cancellation phrases. Ambiguous transcripts must ask for clarification instead of storing. | `inputs/00-original-request.md` | `subbundles/03-03-cognitive-memory-voice-dialogue` |
| R-012 | Browser-visible voice work must be proved with real rendered UI checks and screenshot/log evidence, not only by compile tests. | `inputs/02-structured-input.md` | `subbundles/02-02-agent-settings-and-chat-audio`, `subbundles/03-03-cognitive-memory-voice-dialogue` |

## Explicit Non-Goals

- Full realtime streaming duplex voice is not required in this phase.
- Local model drivers are not implemented in this phase; the contracts and factory must make them addable later.
- Voice input must not bypass agent permissions, provider policy, Cognitive Memory model access policy, or review gates.
