# Tests prompts and scenarios

This folder provides additional guidance for Codex to implement robust tests.

## Recommended additions
1. `RealtimeNoteScoreTrackerTests`
   - decays as expected
   - hold weighting increases score over time
   - sustain multiplier reduces held contribution

2. `RealtimeChordDetectionScoredInputTests`
   - arpeggios
   - melody overlay
   - fast repeated chord changes

3. `TonalScaleLibraryContextInferenceTests`
   - blues inference
   - pentatonic inference
   - diatonic mode inference

4. `RealtimeHarmonicAssistantContextTests`
   - route planning uses inferred scale context
   - style weights influence ranking
   - determinism preserved

## Scenarios (see /04_TESTS/scenarios/*.json)
- `c7_arpeggio_with_blues_melody.json`
- `ii_v_i_in_major.json`
- `dark_shift_progression.json`
