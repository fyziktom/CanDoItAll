# Structured Input

## Core Objective

- Long voice input and output should be chunked through shared voice-layer behavior so all app voice consumers get the same STT and TTS behavior.

## Success Criteria

- TTS splits long prepared speech text into ordered sentence-oriented chunks that are well below the provider's documented input cap.
- Voice consumers can request progressive TTS synthesis and enqueue each returned audio chunk immediately.
- STT accepts ordered browser recording chunks and transcribes them sequentially through the configured driver.
- Existing single-shot TTS and STT paths continue to work for short inputs.
- Unit tests prove chunking, synthesis order, transcription order, and existing preprocessor behavior.

## Hard Constraints

- Chunking must live in shared voice-layer contracts/services, not duplicated in each Blazor component.
- Failed STT or TTS chunks must fail the operation explicitly; no silent fallback or skipped chunk is allowed.
- Visible text content must not be changed by TTS chunking.
- Existing provider profile validation and credential resolution must remain intact.
- OpenAI driver behavior must remain provider-specific only where it builds OpenAI HTTP requests.

## Allowed Side Effects

- Voice contracts, voice service internals, OpenAI driver tests, browser voice JavaScript, and the known app voice callers may change.
- Voice settings UI may remain unchanged unless a necessary status or sample playback contract change is discovered.

## Source Artifacts

- `inputs/00-original-request.md`.
- `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- N001: "STT and TTS ... if it is very long it must be splited to chunks."
- N002: "Especially for the TTS the api is limited with 2000 tokens. and even that is too much."
- N003: "split it into sentences or few sentences and convert them."
- N004: "during they are already playing other sentences are comming, so it creates feeling of faster response."
- N005: "generic function of that drivers so it will work the same in all cases where we use it across our app."

## Dependency And Sequencing Signals

- Shared voice contract/service changes must land before UI callers can use progressive playback.
- Browser queue playback must exist before Blazor callers can enqueue TTS segments safely.
- STT chunk request models must be consumed by all current browser recording call sites.

## Validation Expectations

- Unit tests for the text chunker and service-level streaming behavior.
- Unit tests for multi-chunk STT aggregation and existing OpenAI request shape.
- Targeted `dotnet test` for `CanDoItAll.Tests.Unit`.
- Browser proof for at least one agent chat voice route when a local host can be started; otherwise record the explicit blocker.

## Evidence Contract

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentVoiceTests`.
- Build or broader test run if targeted changes expose compile failures outside the unit project.
- Browser validation analytics row for the voice UI route or an explicit environment blocker.

## UI Validation Strategy

- Use the normal agent chat route because it exercises the shared chat voice panel and browser voice bridge.
- Large-screen pass: open the app, verify voice controls still render and no status text overflows after progressive playback status changes.
- Narrower-width follow-up is only required if UI markup or CSS changes; this bundle is expected to change behavior wiring more than layout.

## Browser Validation Analytics

- Subbundle 01 logs `N/A` because it is service/driver code only.
- Subbundle 02 logs route, viewport, browser actions, status assertions, and screenshots if a local app host is available.

## Working Assumptions

- Conservative character-based TTS chunking is acceptable because exact tokenization is provider/model-specific and no tokenizer abstraction exists in the current voice layer.
- The browser `MediaRecorder` can emit ordered time-sliced chunks for long recordings; the service will preserve order and fail explicitly if any chunk cannot be transcribed.
- The existing short sample playback can remain single-shot unless tests or UI wiring show it needs the progressive API.

## Primary Risks

- Arbitrary server-side splitting of compressed audio bytes is unsafe; STT chunking should originate from browser recording chunks, not byte slicing on the server.
- Concatenating audio files server-side is not safe for every response format; progressive playback should queue chunks individually.
- If a local OpenAI provider is not configured, live synthesis proof must be limited to unit tests and browser UI wiring checks.
