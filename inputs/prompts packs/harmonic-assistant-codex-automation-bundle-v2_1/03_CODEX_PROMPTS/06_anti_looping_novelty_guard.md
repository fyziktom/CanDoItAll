# 06 — Anti-looping / novelty guard implementation

Goal: prevent cycling between 2-3 chords unless explicitly allowed.

Tasks:
1) Implement novelty penalty in the engine scoring (see /02_DESIGN/03_planning-anti-loop-novelty.md).
2) Add “Loop Mode” toggle:
   - when enabled: reduce or disable novelty penalty
3) Add tests:
   - given a history A,B and candidate A, ensure it is penalized (loop mode off).
   - when loop mode on, penalty is reduced.

Acceptance:
- No more obvious 2-chord oscillation in top-3 suggestions in neutral scenarios.
