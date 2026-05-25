# SB05 Semantic Invariants

## SB05-INV-001

- Invariant ID: SB05-INV-001
- Source raw note: N001, N002, N005 mapped to RQ07, RQ08, RQ11, RQ12.
- Expected behavior: Artifact validation distinguishes runtime logs from decision logs, accepts legitimate TODO registers, validates JSON file content, and checks current-run lineage by producer kind including subprocess artifacts.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB05/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB05/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: Malformed JSON, stale workspace execution artifacts, and placeholder records are rejected; legal/business decision logs are not coerced into runtime proof.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

