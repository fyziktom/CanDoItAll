# SB02 Semantic Invariants

## SB02-INV-001

- Invariant ID: SB02-INV-001
- Source raw note: N001, N002 mapped to RQ03, RQ04, RQ08, RQ12.
- Expected behavior: Workflow and subprocess-backed process steps now load the same process finalizer context as direct execution, and subprocess source-less projection gaps become diagnostics instead of satisfying required artifacts.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB02/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB02/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB02/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: A subprocess projection with no source artifact records a gap diagnostic and cannot masquerade as a required deliverable.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

