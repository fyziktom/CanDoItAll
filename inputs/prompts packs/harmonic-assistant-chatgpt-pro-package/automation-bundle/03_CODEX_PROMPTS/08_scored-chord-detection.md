# 08 — Upgrade chord detection to use scored tones + confidence gating

Goal: modify `RealtimeChordDetectionService` to optionally detect chords using the pitch-class scoring snapshot:
- start from highest-scored tones
- progressively include lower-scored tones until confidence threshold is met
- preserve low-scored tones for scale inference later

## Files to modify
- `src/MusicTheory.Core/Recognition/RealtimeChordDetectionService.cs`
- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs` (call new overload)
- `src/MusicTheory.Core/Recognition/RealtimeChordDetectionOptions.cs` (if exists; if embedded, update record)
- `tests/MusicTheory.Tests/RealtimeChordDetectionTests.cs` (add new tests)

## 1) Add new detection overload
Add:
- `Detect(RealtimeChordWindowSnapshot snapshot, RealtimeNoteScoreSnapshot? scores, RealtimeChordDetectionOptions? options = null)`

Keep old signature as convenience:
- it calls the new overload with `scores = null`.

## 2) Add new options
Extend `RealtimeChordDetectionOptions` with tunables:
- `double MinDetectionConfidence = 0.62`
- `int MinMatchedPitchClasses = 3`
- `int StartPitchClassCount = 3`
- `int MaxPitchClassCount = 8` (or reuse existing MaxCandidates naming carefully)
- `double MinPitchClassScoreToInclude = 0.05` (optional)

Ensure defaults preserve baseline behavior when `scores == null`.

## 3) Implement scored pitch class selection loop
When `scores != null` and has pitch class scores:
1. get ranked pitch classes desc by score
2. for K in [StartPitchClassCount .. MaxPitchClassCount]:
   - selected = top K pitch classes
   - call recognition engine using `selected`
   - build and score candidates as currently done
   - compute confidence (per design doc):
     - use best vs second score gap and coverage
   - if accepted: break and return
3. if never accepted: return best attempt (largest K)

Important: when building observed notes for voicing analyzer:
- filter `snapshot.ActiveNotes` to only notes whose pitch class is in `selected`
- plus include the detected bass note if needed

## 4) Wire session service to pass score snapshot
In `RealtimeChordDetectionSessionService.EvaluateNowAsync`:
- pass `scoreSnapshot` into detection service detect call

## 5) Update / add tests
In `tests/MusicTheory.Tests/RealtimeChordDetectionTests.cs`, add:

### Test A: arpeggio still detects chord
- Simulate NoteOn events for C-E-G-Bb in sequence (no overlapping holds)
- Use score tracker snapshot to provide pitch class scores
- Expect top chord candidate is C7

### Test B: melody overlay does not break chord
- Simulate left hand chord tones + right hand melody notes (e.g., C7 arpeggio + passing tones)
- Ensure detection remains C7 and confidence gating is met

If score tracker isn't directly accessible in test project:
- create a synthetic `RealtimeNoteScoreSnapshot` with pitch class scores for the scenario.

## Acceptance criteria
- Existing tests pass.
- New tests pass.
- Chord detection is noticeably more stable with arpeggios + melody noise.

## Self-check
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests"`
