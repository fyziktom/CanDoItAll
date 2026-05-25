# SB06 Semantic Invariants

## SB06-INV-001

- Invariant ID: SB06-INV-001
- Source raw note: N002 mapped to RQ09, RQ12.
- Expected behavior: Repeated no-progress retry reasons are compressed after the first attempt unless the current run produced new evidence, mutation, manual directive, provider repair, or repair-worthy signal.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB06/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB06/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB06/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: A second successful-but-incomplete attempt with only missing-tool/no-artifact reasons does not spin another identical retry.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

