# Route planning improvement plan (Realtime Harmonic Assistant Engine)

The current route planning is “rule-heavy” and only lightly influenced by:
- settings brightness/colorfulness,
- style pack enabled device groups.
It does **not** incorporate:
- style pack device weights and constraints (most ignored),
- scored-note scale/style context (new requirement),
- a multi-dimensional tonal-space distance metric.

This spec defines a practical, implementable upgrade.

## 1) New inputs available to the engine
From detection:
- Top chord candidates (existing)
- **Pitch class score profile** (new)
- **Inferred scale context** (new)
- Derived “style hint” flags (new; e.g., blues)

## 2) Practical algorithmic approach
Keep the existing architecture (hypotheses + beam search) but improve:
- transition candidate generation
- transition scoring

### 2.1 Transition candidate generation improvements
1) Keep the diatonic functional target-degree table (good baseline).
2) Expand device candidates using the style pack:
   - attach metadata: `DeviceName`, `DeviceGroup`, `DeviceWeight`
   - incorporate additional devices already present in style packs (if available in repo):
     - backdoor dominants
     - diminished passing
     - turnaround variants
     - passing approach dominants
     - chromatic mediants, Neapolitan, augmented sixth, etc.
3) Optional: add “blues grammar” transitions when context says blues:
   - I7 → IV7 → I7 → V7 (and turnarounds)
   - bIII7 as color device (if allowed)

### 2.2 Transition scoring improvements (additive terms)
Base score remains but add these terms:

#### A) Style pack weight multiplier
- For device transitions: `score *= style.DeviceWeights[DeviceName]` (default 1.0)
- For diatonic transitions: use a neutral multiplier (1.0) or style-specific base

#### B) Style constraints (rolling window)
Use the existing constraints in `HarmonicStyleConstraints`:
- MaxChromaticBarsPer8
- MaxSecondaryDominantsPer8
- MaxSubstitutionsPerCadence
Implement lightweight counters from history (last 8 events):
- if the candidate violates constraints, either:
  - suppress the candidate, or
  - apply a strong penalty multiplier (e.g., 0.25)

#### C) Tonal distance / circle-of-fifths coherence
Add a bonus for musically coherent movement:
- prefer V→I, ii→V, and fourth/fifth motion
- compute fifths distance between roots:
  - `fifthsSteps = minStepsOnCircleOfFifths(fromRoot, toRoot)` (0..6)
- apply:
  - bonus for steps 1 (perfect fifth/fourth): +0.10
  - small penalty for large jumps: `-0.03 * max(0, fifthsSteps - 2)`

This complements voice-leading cost (which is pitch-class-based).

#### D) Mood-axis continuity
Use the same chord→mood mapping as the canvas:
- avoid chaotic up/down jumps unless style wants it
- penalty term:
  - `-0.06 * abs(mood(to) - mood(from))`

#### E) Scale context compatibility
Use inferred scale context:
- if top inferred scale is (Root, Mode) with pitch class set S
- reward chords whose pitch classes are mostly contained in S
- penalty if chord tones contradict S strongly

This is implementable by:
- `coverage = |chordTones ∩ S| / |chordTones|`
- `scaleBonus = (coverage - 0.5) * 0.18` (clamped)
- if “blues” context, prefer dominant chords and b3/b5 vocabulary

### 2.3 Beam search objective remains stable
Keep beam search, but the new scoring terms should:
- reduce incoherent jumps
- increase style differentiation
- use user playing context as an actual signal

## 3) Validation plan
### 3.1 Quantitative unit tests
- Determinism: same input -> same top path.
- Style weight effect: switching style packs changes ranking in expected direction.
- Blues context test: with note-score profile matching minor blues, suggestions include I7/IV7/V7 grammar more often.

### 3.2 Qualitative manual validation
- Play arpeggiated C7 with blues melody notes:
  - chord detection remains C7
  - scale hint shows C minor blues (or close)
  - suggestions include blues-friendly dominants and turnarounds
- Move toward darker chords:
  - graph moves downward
  - suggested branches appear below centerline and are tinted darker
  - route planning proposes coherent dark choices (modal interchange / minor family)

## 4) Acceptance criteria
- Route planning is clearly more coherent and style-sensitive than baseline.
- It uses scored-note context (scale inference) as a real signal.
- It remains performant (no unbounded search or expensive per-frame work).
