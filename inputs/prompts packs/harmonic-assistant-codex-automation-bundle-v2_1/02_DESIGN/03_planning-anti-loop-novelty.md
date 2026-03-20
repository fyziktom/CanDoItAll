# Anti-looping / Novelty in Planning (v2.1)

Observed issue:
- Suggestions sometimes oscillate between 2-3 chords.

## Strategy
Add a novelty penalty to planning score:
- Keep a rolling window of last K chords (e.g., 8-16) in history.
- For a candidate next chord:
  - penalty if chord repeats within last K steps:
    - `repeatPenalty = alpha * (1 - (distanceFromLastRepeat / K))`
- Additional penalty for 2-cycle and 3-cycle patterns:
  - detect if last 2 chords are (A,B) and candidate is A → penalize strongly
  - detect if last 3 chords are (A,B,C) and candidate is A → penalize

Allow explicit loops:
- If a “Loop Mode” module is enabled or user explicitly requests vamp:
  - reduce novelty penalty or disable it.

Implementation location:
- In `RealtimeHarmonicAssistantEngine` scoring function that ranks transitions or paths.
- Alternatively, add a post-filter that prunes top paths that are cyclical.

Acceptance:
- In a neutral session, top suggestions should not alternate endlessly between two chords unless user has enabled loop mode.
