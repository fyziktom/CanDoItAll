# SB06 Semantic Invariants

## Invariants

- Invariant ID: `SB06-I001`
- Source raw note: User required Codex to act as user, confirm escalations, observe the run, and store compact summaries rather than loading all process data.
- Expected behavior: The live process is observed through APIs, phase evidence is summarized, and UX observations are written into process data.
- Disallowed shallow implementation: Chat-only observations or manually editing the generated app.
- Failing-first test: Earlier runs blocked on missing/projection artifacts and repeated downstream attempts without useful recovery.
- Passing test: Run `f0c184d4-e823-409e-b159-0fca1f911b00` completed; `bundle://proof/SB06/transcripts/live-run-observation.txt` records final status and operator directive.
- Changed source files: Runtime fixes are covered by SB01/SB02/SB07; SB06 itself changed no production files.
- Production assertions: Final run record reports status `Completed`, blocked count `0`, capability gap count `0`.
- Red-team negative case: A repeated missing-artifact retry loop would leave the run blocked; this run progressed to repaired evidence writeback.
- Downstream dependency check: SB07 could read final app evidence and project-structure writeback from the completed run.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Final run record | Process runtime API | Observer and final validation | Captures completion status and step counts | `bundle://proof/SB06/summaries/final-run-summary.md` |
| Manager directive | Process manager directive API | Process journal | Stores operator UX observations inside runtime data | `bundle://proof/SB06/transcripts/live-run-observation.txt` |
