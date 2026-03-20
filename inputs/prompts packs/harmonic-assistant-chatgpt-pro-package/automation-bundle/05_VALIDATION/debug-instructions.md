# Debug instructions

## Typical debugging workflow
1. Enable debug panel on `/harmony`.
2. Play a chord slowly as an arpeggio.
3. Verify:
   - pitch class scores rise for chord tones
   - low-scored melody tones exist but are lower
   - detected chord remains stable
   - inferred scale context matches your playing

## If chord detection is wrong
- Increase `WindowMs` slightly (more memory for arpeggios).
- Increase hold boost if sustained tones are underweighted.
- Increase confidence threshold if false positives occur.
- Reduce melody influence by raising `MinPitchClassScoreToInclude` (if implemented).

## If canvas is cluttered
- Reduce history steps.
- Increase min zoom.
- Increase base step spacing.
- Increase font scale via canvas controls.
