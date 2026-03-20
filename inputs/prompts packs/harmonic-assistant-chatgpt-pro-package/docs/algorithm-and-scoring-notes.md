# Realtime Harmonic Assistant: Algorithm and Scoring Notes

## 1) Overview
The engine is a hybrid of:
- probabilistic candidate normalization,
- lightweight key-context hypothesis tracking,
- bounded beam search with rule-based transition generation,
- voice-leading penalty and functional bonuses.

Core file:
- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`

## 2) Input Normalization
Raw detection candidates come from `RealtimeChordDetectionService`.
The assistant transforms the top 5 candidates into normalized probabilities:

1. `minScore = min(candidate.Score)`
2. `shiftedScore = max(0.001, candidate.Score - minScore + 0.001)`
3. `probability = shiftedScore / sum(shiftedScore[])`

Reference:
- `BuildWeightedCandidates`: `RealtimeHarmonicAssistantEngine.cs:114-140`

Properties:
- Always non-zero post-shift (prevents dead candidates).
- Relative ranking preserved.
- Sensitivity depends on score spread from detector.

## 3) Hypothesis Update Model
A hypothesis = `(CurrentChord, KeyContext, Weight, Reasons)`.

### 3.1 New hypotheses from current event
For each weighted candidate and each resolved key:
- `Weight = candidateProbability * 0.75`
- Reasons include `"detected chord"` and key hypothesis label.

Reference:
- `RealtimeHarmonicAssistantEngine.cs:149-158`

### 3.2 Continuation hypotheses from previous state
For each previous hypothesis + current candidate:

- `keyCompatibility = 1.0 if key contains chord root else 0.48`
- `voiceLeading = 1 / (1 + VoiceLeadingCost(prevChord, currChord))`
- `stability = 1.12 if same harmony else 1.0`

Weight update:
```text
updatedWeight =
(
  prevWeight * 0.45
  + candidateProbability * 0.45
  + keyCompatibility * 0.08
  + voiceLeading * 0.02
) * stability
```

Reference:
- `RealtimeHarmonicAssistantEngine.cs:165-175`

Post-processing:
- Keep top 5 by weight.
- Normalize weight by total sum.
- Reference: `RealtimeHarmonicAssistantEngine.cs:184-197`

## 4) Key Inference Rules
### 4.1 Hard lock branch
If both:
- `settings.LockKey == true`
- `settings.LockedKeyPitchClass.HasValue == true`
then key is forced to `LockedKeyPitchClass + LockedMode`.

Reference:
- `ResolveBestKey` `RealtimeHarmonicAssistantEngine.cs:202-208`
- `ResolveCandidateKeys` `RealtimeHarmonicAssistantEngine.cs:225-231`

### 4.2 Non-lock key candidates
From candidate chord:
- Infer mode from symbol (`min` or starts with `m` => natural minor, else major).
- Build:
  - primary key (chord root + inferred mode)
  - relative key (major<->minor relation)
  - optional darker mode (Dorian) when bridge or low brightness.

Reference:
- `RealtimeHarmonicAssistantEngine.cs:233-252`

## 5) Suggestion Path Scoring
Suggestions are generated per hypothesis via beam search.

### 5.1 Beam initialization
Initial path score:
```text
log(max(0.0001, hypothesis.Weight + 0.0001))
```
Reference:
- `RealtimeHarmonicAssistantEngine.cs:297`

### 5.2 Step expansion score
For each transition:
```text
stepScore = node.Score + transition.BaseScore - voiceLeadingCost * 0.11
```
Reference:
- `RealtimeHarmonicAssistantEngine.cs:308-309`

### 5.3 Beam bounds
- `horizon = clamp(HorizonChords, 3, 6)`
- `width = clamp(BeamWidth, 4, 12)`
- per depth: keep top `width`

Reference:
- `RealtimeHarmonicAssistantEngine.cs:293-331`

### 5.4 Path probability
After collecting all path scores:
1. Keep top 12 by score
2. `expScore = exp(path.Score)`
3. `Probability = expScore / sum(expScore[])`

Reference:
- `RealtimeHarmonicAssistantEngine.cs:275-288`

Implication:
- Score scale strongly affects probability concentration.
- Long horizons can amplify score separation.

## 6) Transition Generation Logic
Transition generation starts from functional motion by degree.

### 6.1 Functional target degree tables
Given current degree:
- I -> V, vi, IV, ii
- ii -> V, I, vi
- iii -> vi, IV, ii
- IV -> V, I, ii
- V -> I, vi, IV
- vi -> IV, ii, V

Reference:
- `RealtimeHarmonicAssistantEngine.cs:352-361`

### 6.2 Base diatonic candidates
Each target degree creates a chord from `BuildDiatonicChord`.
Base score:
```text
1.1 + cadenceBonus - depth * 0.03
```
Cadence bonus = `0.35` when target degree is tonic.

Reference:
- `RealtimeHarmonicAssistantEngine.cs:366-378`

### 6.3 Optional device candidates
Enabled based on style pack device groups + settings thresholds:

1. Secondary dominants
- condition: style has group + (`Colorfulness > 0.4` or Bebop style)
- base score: `1.15`
- `RealtimeHarmonicAssistantEngine.cs:380-389`

2. Tritone substitutions
- condition: style has group + (Bebop or `Colorfulness >= 0.6`)
- base score: `1.02`
- `RealtimeHarmonicAssistantEngine.cs:391-400`

3. Modal interchange
- condition: style has group + (`Brightness <= 0.45` or section Bridge)
- base score: `0.96`
- `RealtimeHarmonicAssistantEngine.cs:402-411`

4. Brightness lift option
- condition: `Brightness > 0.65`
- base score: `0.92`
- `RealtimeHarmonicAssistantEngine.cs:413-420`

5. Simple cadence anchor
- condition: `Colorfulness < 0.45 && depth <= 2`
- base score: `1.08`
- `RealtimeHarmonicAssistantEngine.cs:422-430`

### 6.4 Dedup and pruning
- Group by `(root pitch class, symbol)`, keep highest score per group.
- Keep top 10 transitions.
- `RealtimeHarmonicAssistantEngine.cs:432-437`

## 7) Diatonic Chord Construction Rules
Chord symbol by degree and minor-family mode:

- I: `maj` or `min`
- II: `min/min7` or `m7b5` in minor-family
- III: `min/min7` in major family, `maj` in minor-family
- IV: `maj/maj7` in major family, `min` in minor-family
- V: `7` in major family, `min` in minor-family
- VI: `min/min7` in major family, `maj` in minor-family
- VII: `dim` in major family, `7` in minor-family

Reference:
- `RealtimeHarmonicAssistantEngine.cs:445-455`

Safety fallback:
- If chord symbol not in library, fallback to `"maj"`.
- `RealtimeHarmonicAssistantEngine.cs:457-463`

## 8) Voice-Leading Cost Formula
Given from-chord and to-chord pitch class sets:

Inputs:
- `commonTones = |fromSet ∩ toSet|`
- `bassMotion = circularDistance(fromRoot, toRoot)`
- `nearestMotion = Σ (for each tone in toSet: min circular distance to fromSet)`

Cost:
```text
cost = nearestMotion * 0.45 + bassMotion * 0.35 - commonTones * 0.5
cost = max(0.05, cost)
```

References:
- `RealtimeHarmonicAssistantEngine.cs:487-503`
- `CircularDistance` `RealtimeHarmonicAssistantEngine.cs:505-509`

Behavioral effect:
- Encourages shared tones.
- Penalizes large root motion and aggregate pitch-class travel.
- Never zero to avoid division/weight singularities.

## 9) Chord Detection Input Quality Constraints
Assistant quality is limited by detection quality.
Realtime detection quality drivers:
- `RealtimeChordWindowDetector` stability/debounce window.
- Sustain downweighting in recognition pitch-class set selection.

References:
- `src/MusicTheory.Core/Recognition/RealtimeChordWindowDetector.cs`
- `src/MusicTheory.Core/Recognition/RealtimeChordDetectionService.cs`

Notable knobs:
- Debounce (`110ms` default)
- Max chord window (`320ms`)
- Silence reset (`700ms`)
- Sustain decay (`1600ms` default in detection options)

## 10) Algorithmic Limitations to Address
1. Style constraints and device weights are not actively used in realtime scoring.
2. Key lock is partial from UI perspective (missing locked key choice).
3. No explicit confidence gating on unstable detection snapshots in assistant update path.
4. Scoring is rule-heavy and not calibrated by recorded user acceptance/outcome metrics.
5. Search horizon and beam width are not user-tunable in current page UI.
