# 08_DRUM_GENERATOR_AUDIO_AND_LIBRARY.md

## Goal
Add a detailed drum generator that provides:
- selectable drum “kit” (kick/snare/hats at minimum),
- pattern editor on beats (step sequencer),
- metronomic accurate playback using WebAudio scheduling,
- optional swing/humanization,
- **adaptive tempo** that follows MIDI input (human feel),
- works offline.

## Audio constraints (web)
- Use WebAudio scheduling (`AudioBufferSourceNode.start(when)`) and a look-ahead scheduler.
- Avoid `setTimeout`-only timing (too jittery).
- Keep CPU low; pre-decode buffers and reuse.

## Legal drum sample library (must implement a practical approach)
You must implement **one built-in kit** that is safe to redistribute:
- Default approach: **generate a small synthetic kit** at build time and include WAVs under CC0 (we own them).
- Optional enhancements:
  - Add a script/instructions for downloading a public domain kit (e.g., LM-2/TR-505 public domain drum machine samples from the “Sampled” dataset).
  - Add a CC-BY kit option (e.g., DrumGizmo DRSKit) only if attribution is handled in UI and docs.

Do NOT bundle huge kits by default.

## Where to put assets in the repo
- `src/App.Web/wwwroot/assets/drums/synthetic_cc0/`
  - `kick.wav`
  - `snare.wav`
  - `hihat_closed.wav`
  - `hihat_open.wav`
  - `LICENSE.txt` (CC0 text and attribution, if any)
- Optional: `src/App.Web/wwwroot/assets/drums/external/` (gitignored) for larger kits.

## Implementation (must do)

### A) WebAudio engine (JS)
Create:
- `src/App.Web/wwwroot/drumMachineAudio.js`

Features:
- init AudioContext
- load sample buffers (fetch + decode)
- schedule events in a look-ahead loop:
  - schedule window: 0.1–0.2s ahead
  - tick interval: 25ms
- support per-hit velocity + pan optional
- expose functions:
  - `init()`
  - `loadKit(kitId)`
  - `setTempo(bpm)`
  - `setSwing(amount)`
  - `setPattern(patternDto)`
  - `start()`, `stop()`
  - `tapTempo(timestamp)` (for adaptive tempo)
  - `setHumanize(msJitter)`

### B) Blazor interop service
Create:
- `src/App.Blazor/Services/DrumMachineService.cs`
- `src/App.Blazor/Services/DrumMachineInterop.cs`

Responsibilities:
- manage current kit/pattern/tempo
- integrate adaptive tempo tracking from MIDI
- persist pattern presets locally (IndexedDB later)

### C) Pattern model
Add to `src/App.Shared` (DTO) + optionally `MusicTheory.Core`:
- `DrumPattern`
  - `StepsPerBar` (16)
  - `Bars` (1–8)
  - tracks: kick/snare/hihat closed/hihat open
  - each step: on/off + velocity (0–1)
- `DrumKitDefinition`
  - mapping of logical instruments to sample urls

### D) UI
Create page:
- `src/App.Blazor/Pages/DrumGenerator.razor` (route `/drums` or `/practice/drums`)
UI requirements:
- kit selector (Synthetic CC0 default)
- tempo control + tap tempo
- swing control
- step grid editor (click to toggle steps)
- play/stop
- “Follow my playing” toggle (adaptive tempo from MIDI note-ons)

Add stable selectors:
- `data-testid="drums-play"`, `data-testid="drums-stop"`, `data-testid="drums-tempo"`

### E) Adaptive tempo (human feel)
Implement tempo tracking algorithm:
- Capture MIDI note-on timestamps (from `IMidiService.NoteOn`)
- Compute inter-onset intervals (IOI) for recent taps
- Convert to BPM and smooth using EMA:
  - `bpm = 60_000 / median(IOI)`
  - `bpmSmoothed = alpha*bpm + (1-alpha)*prev`
- Apply constraints (min 40, max 220)
- Optional: quantize to nearest integer BPM when stable
- When “follow” is enabled:
  - gradually adjust scheduler tempo (avoid sudden jumps)

### F) Testing
- Unit test tempo estimator logic in `tests/MusicTheory.Tests` (pure functions).
- Add a Playwright smoke test:
  - open drum page
  - toggle a few steps
  - press play (verify UI state changes)

## Edge cases (MUST)
- Autoplay restrictions: AudioContext requires user gesture to start; handle gracefully.
- Offline: samples must be local.
- Safari limitations: keep fallback (basic metronome click) if WebAudio constraints appear.

## Acceptance criteria
- Playback is stable (no audible jitter at 120 BPM with 16th notes).
- Pattern edits apply in realtime while playing.
- Adaptive tempo follows user taps within ~2–4 beats without oscillation.
- Default kit is legally redistributable and included in repo.

## Verification steps
- Manual:
  - enable follow mode; play steady quarter notes; confirm BPM converges.
  - test swing/humanize does not break timing.
- Automated:
  - unit tests pass
  - Playwright smoke test passes
