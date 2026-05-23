# SB07 Semantic Invariants

## Invariants

- Invariant ID: `SB07-I001`
- Source raw note: User required proof that agents can build and validate the app, including screenshots, without Codex building or fixing the demo app.
- Expected behavior: Agent output includes build/test/runtime/browser evidence, clean console, screenshot, and project-structure result writeback.
- Disallowed shallow implementation: Accepting chat-only summaries, stale screenshots, or manually repairing generated app files.
- Failing-first test: The live process initially exposed missing/projection artifact blocks and one QA repair branch.
- Passing test: `bundle://proof/SB07/transcripts/browser-validation.txt` records clean browser evidence; process run completed with repaired evidence index.
- Changed source files: Runtime/test repairs in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` and `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`.
- Production assertions: Evidence index states runtime smoke succeeded, tests passed, screenshot was produced, console had 0 errors and 0 warnings, and validation node `custom:63fea9e39f6f4262b9ea65876186d738` was created.
- Red-team negative case: If screenshot or console evidence were stale or missing, the Blazor templates require repair/revalidation before record closure.
- Downstream dependency check: Project-structure writeback makes the result visible to future project-structure-driven reruns.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Screenshot evidence | Agent QA browser step | Final validation, project structure, user demo | Copied into `bundle://proof/SB07/screenshots/tetris-revalidated-current.png` and referenced by process artifacts | `bundle://proof/SB07/transcripts/browser-validation.txt` |
| Console evidence | Agent QA browser step | Final validation and process record step | Stores browser console messages with 0 errors and 0 warnings | `bundle://proof/SB07/transcripts/browser-validation.txt` |
| Project-structure validation node | Delivery manager final record step | Project structure readers | Durable evidence that results were written back | `bundle://proof/SB06/summaries/final-run-summary.md` |
