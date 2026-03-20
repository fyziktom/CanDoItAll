# PROMPT 01 — Fix duration-change ripple (A2, A3) + tests

Goal: Dot tool / duration edits must respect InsertMode ripple editing and never create overlaps.

Read:
- `DESIGN/REFLOW_AND_SPACING.md` (Part 1)
- `SCENARIOS/RHYTHM_SCENARIOS.md` (S1, S2)

Tasks:
1) Implement InsertMode-aware duration change:
   - Extend `SetNoteDotsCommand` (and any other duration-change command) to include `InsertMode`.
   - Update `NotationEditorCanvas.razor` so when it executes `SetNoteDotsCommand`, it passes `State.Settings.InsertMode`.
   - Update `ScoreEditingOperations.ChangeDuration(...)` to accept InsertMode and implement:
     - Replace: delete overlapped events
     - InsertAndShift: shift later events by delta (only when delta > 0)
     - Split: split/trim overlapped events (reuse existing SplitInMeasure logic if possible)
   - Ensure chord stacks (multiple notes at same Start) are updated as a group.

2) Add unit tests:
   - New tests in `tests/MusicTheory.Tests/NotationEditorCoreTests.cs` (or a new file):
     - S1: two quarter notes; dot first; InsertAndShift => second start becomes 3/8; auto-rest fills end.
     - S2: Replace mode overlaps deletion.
   - Add an invariant helper in tests: assert no overlap within a (staff, voice) after operations.

3) Add/Update Playwright test:
   - New test `E2E_NotationEditor_RippleDot_ShiftsFollowingNotes_AndAddsRests`
   - Load `score_ripple_dot_in_measure.json` as fixture:
     - toggle InsertMode to InsertAndShift (add UI control or keyboard shortcut if needed)
     - select Dot tool and click first note
     - wait for render commands
     - assert an `auto-rest` exists in base commands
     - assert note-head X positions are not equal for distinct starts (coarse collision check)

4) Run:
   - `dotnet test`
   - Ensure Playwright tests pass.

Acceptance criteria:
- The editor does not create overlapping events in a single voice after dot/duration change.
- Unit tests cover ripple semantics.
- Playwright test passes.

Update:
- Mark **A2** and **A3** as done in `MASTER_CHECKLIST.md`.
- Add progress log entry with tests executed.

STOP.
