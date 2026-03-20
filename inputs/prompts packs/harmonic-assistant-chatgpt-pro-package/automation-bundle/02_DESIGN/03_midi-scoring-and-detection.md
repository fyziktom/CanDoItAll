# MIDI scoring + chord detection upgrade

Goal: detect chords reliably when users:
- arpeggiate chord tones,
- omit some chord tones,
- play melody tones simultaneously,
- use sustain pedal,
by tracking a **floating score** per note/pitch class in a time window.

## 1) New concept: Note score tracker
Create a new component in `MusicTheory.Core/Recognition`:

### 1.1 Data model
- `RealtimeNoteScoreOptions`
  - `WindowMs` (e.g., 1800ms) — “memory horizon”
  - `DecayMs` (e.g., 1200ms) — exponential decay constant
  - `NoteOnBoost` (e.g., 1.0)
  - `VelocityWeight` (0..1) — if velocity should matter
  - `HoldBoostPerSecond` (e.g., 0.9)
  - `SustainHeldMultiplier` (e.g., 0.35)
  - `MinScoreToKeep` (e.g., 0.03)
  - `MaxTrackedNotes` (e.g., 48) — safety
- `RealtimeNoteScoreSnapshot`
  - timestamp
  - per-midi-note scores (optional)
  - per-pitch-class scores (required)
  - top pitch classes (descending)
  - bass pitch class guess (optional)
  - inferred “scale context” candidates (see section 3)

### 1.2 Update semantics
The tracker consumes the same `RealtimeMidiEvent` stream:
- NoteOn: add score boost, mark held, store velocity
- NoteOff: mark not held (but keep score to decay)
- Sustain pedal:
  - when down: released notes remain “sustained”
  - when up: sustained notes become inactive and decay faster

### 1.3 Decay and hold accumulation
Use **exponential decay** computed lazily per note:
- `scoreNow = scoreLast * exp(-(now - lastUpdate) / DecayMs) + holdContribution`
Hold contribution:
- if held: `scoreNow += (dtSeconds * HoldBoostPerSecond) * heldMultiplier`
- heldMultiplier = 1.0 when pressed, `SustainHeldMultiplier` when sustained only

Prune:
- remove notes below `MinScoreToKeep` and older than WindowMs (or after decay drives near 0)
- cap total tracked notes to avoid pathological cases (dense clusters)

Thread safety:
- tracker can be protected by a lock, or use `ConcurrentDictionary`.
- In Blazor WASM single-thread it is simpler, but keep correctness for future hosting.

## 2) Chord detection using scored tones
Modify `RealtimeChordDetectionService` to optionally consume `RealtimeNoteScoreSnapshot`.

### 2.1 Ranked pitch class selection
- Start with the top-scored pitch classes.
- Begin with `K = 3` (triad minimum).
- Attempt recognition with the top K pitch classes.
- If confidence is below threshold, increase K by 1 and retry, up to `MaxPitchClassesForRecognition`.

### 2.2 Confidence definition (implementable now)
Define detection confidence from the scored candidate list:
- Compute weighted scores with existing `BuildCandidate` scoring.
- Let best = candidates[0], second = candidates[1] (if any).
- Define:
  - `gap = best.Score - second.Score`
  - `coverage = sum(pcScore for pc in best.MatchedPitchClasses) / sum(pcScore for selectedPitchClasses)`
  - `confidence = sigmoid(gap / 2.5) * 0.55 + clamp(coverage,0,1) * 0.45`
- Accept when:
  - `confidence >= options.MinDetectionConfidence` (configurable)
  - AND `best.MatchedPitchClasses.Count >= options.MinMatchedPitchClasses` (configurable)

### 2.3 Melody noise suppression
Because melody notes tend to be low-scored (played briefly), they will be added later (if at all).
But we still preserve them for scale context inference.

### 2.4 Bass pitch class guess
- Prefer the lowest currently sounding note from window snapshot.
- If unavailable, choose the lowest midi note with score above threshold from the score tracker.

## 3) Scale / style context inference from low-scored tones
The tracker output must retain low-scored tones because they reveal scale choices.

### 3.1 Extend TonalScaleLibrary
Add additional scales:
- Major pentatonic
- Minor pentatonic
- Minor blues (and optionally major blues)

Then implement:
- `GetCandidateScalesForPitchClassScores(pitchClassScores, impliedRoot, maxResults)`
Scoring:
- reward sum of in-scale pitch class scores
- penalize out-of-scale scores above a small threshold
- bias to impliedRoot or detected chord root

### 3.2 Blues example
If chord is `C7` but the low-scored pitch classes include:
- C, Eb, F, F#, G, Bb (and occasional A/E),
the scale inference should rank **C minor blues** highly.
That hint should be exposed to route planning.

## 4) Integration points
- `RealtimeChordDetectionSessionService` should hold:
  - `RealtimeChordWindowDetector` (stability)
  - **new** `RealtimeNoteScoreTracker` (scoring memory)
- On each MIDI event:
  - apply to both components
- On evaluation:
  - snapshot = detector.GetSnapshot(now)
  - scoreSnapshot = scoreTracker.GetSnapshot(now)
  - detection = detectionService.Detect(snapshot, scoreSnapshot, options)

## 5) Acceptance criteria
- Arpeggiated chord tones are detected as the intended chord even when not held simultaneously.
- Melody notes (brief, low-scored) do not dominate chord naming.
- Score decay is configurable and prevents old notes from lingering.
- Scale inference surfaces “blues” when the pitch-class distribution matches blues vocabulary.
