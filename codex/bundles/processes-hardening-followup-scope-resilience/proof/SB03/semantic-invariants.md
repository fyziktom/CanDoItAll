# SB03 Semantic Invariants

## SB03-INV-001

- Invariant ID: SB03-INV-001
- Source raw note: N003 mapped to RQ05, RQ11, RQ12.
- Expected behavior: Artifact validation failures now route to modeled negative or repair branch outcomes when the process has enough evidence for a governed disposition, while missing upstream inputs still block.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB03/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB03/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB03/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: Missing upstream artifact inputs remain hard-blocking and are not converted into a repair disposition.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

