# 09 — Add scale context inference (including blues) from scored pitch classes

Goal: use low-scored tones to infer scale/style context (e.g., blues), and expose it to:
- UI (optional),
- route planning (required).

## Files to modify
- `src/MusicTheory.Core/Models/Enums.cs` (extend ModeType if needed)
- `src/MusicTheory.Core/Theory/TonalScaleLibrary.cs`
- `src/MusicTheory.Core/Recognition/RealtimeChordDetectionService.cs`
- The record/type that represents scale suggestions in detection results:
  - search for `CandidateScaleSuggestion` and `RealtimeChordDetectionResult` and update accordingly
- Add tests:
  - `tests/MusicTheory.Tests/TonalScaleLibraryContextInferenceTests.cs`

## 1) Extend ModeType with additional scales
Add enum values (append to preserve existing numeric values where possible):
- `MajorPentatonic`
- `MinorPentatonic`
- `BluesMinor`
- (optional) `BluesMajor`

## 2) Add scale definitions to TonalScaleLibrary
In `TonalScaleLibrary`:
- Add `TonalScaleDefinition` entries for the new modes:
  - Major pentatonic: [0,2,4,7,9]
  - Minor pentatonic: [0,3,5,7,10]
  - Minor blues: [0,3,5,6,7,10]  (b5 = +6)
  - Major blues (optional): [0,2,3,4,7,9]

Add ModePriority entries with reasonable weights.

## 3) Add inference method from pitch class scores
Add:
- `GetCandidateScalesForPitchClassScores(IReadOnlyDictionary<int,double> pitchClassScores, int? impliedRootPitchClass = null, int maxResults = 6)`

Scoring recommendation:
- normalize scores (sum to 1)
- for each root candidate (0..11 plus impliedRoot):
  - for each scale definition:
    - coverage = sum(scores[pc] where pc in scale)
    - penalty = sum(scores[pc] where pc not in scale and scores[pc] > 0.06) * 0.7
    - rootBias = impliedRoot match +0.10
    - finalScore = coverage - penalty + rootBias + modePriority*0.12
Return top results.

## 4) Expose inferred context in detection results
In `RealtimeChordDetectionService.Detect(...)`:
- after computing note scores, call the new inference method and store:
  - top inferred scales (max 4)
- include them in `RealtimeChordDetectionResult` (add a property if needed), e.g.:
  - `IReadOnlyList<CandidateScaleSuggestion> InferredScaleContext`

Do NOT remove existing chord-based `CompatibleScales` (still needed).

## 5) Add tests
Create `TonalScaleLibraryContextInferenceTests`:
- scenario: C7 chord + pitch class score profile including Eb and F# should rank C minor blues high.
Example dictionary (scores sum ~1.0):
- C: 0.22, E: 0.12, Bb: 0.16, G:0.12, Eb:0.10, F:0.08, F#:0.08, A:0.04, D:0.04
Expect:
- top results include root C with mode BluesMinor (or within top 2)

## Acceptance criteria
- Scale inference works and is deterministic.
- Blues is detectable in the provided example profile.
- No existing tests regress.

## Self-check
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~TonalScaleLibraryContextInferenceTests"`
