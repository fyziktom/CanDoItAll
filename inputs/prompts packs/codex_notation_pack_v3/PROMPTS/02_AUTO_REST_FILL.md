# PROMPT 02 — AutoRestFill completeness + meter grouping (A4)

Goal: AutoRestFill must never leave gaps and must respect beat grouping.

Read:
- `DESIGN/REFLOW_AND_SPACING.md` (Part 2)

Tasks:
1) Update `AutoRestFillEngine`:
   - Candidate durations must include at least down to 1/64.
   - Use `MeterGrouping.GetBeatBoundaries(timeSignature)` for boundary splitting (not denominator-only beat unit).
   - Ensure the fill loop never stops early when `remaining > 0`:
     - if no candidate fits, fall back to splitting into smallest supported duration repeatedly.
   - Optional: add config for dotted rests (off by default).

2) Add unit tests:
   - Add a test that creates a 1/64 gap and asserts it is filled.
   - Add a test for 6/8 that ensures rest boundaries respect beat grouping (3/8 beats).
   - Ensure tests are deterministic.

3) Run `dotnet test`.

Acceptance criteria:
- No gaps remain when AutoRestFillEnabled=true.
- Tests prove 1/64 gap handling.

Update checklist:
- Mark **A4** done.

STOP.
