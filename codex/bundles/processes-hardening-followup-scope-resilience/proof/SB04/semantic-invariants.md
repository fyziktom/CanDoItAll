# SB04 Semantic Invariants

## SB04-INV-001

- Invariant ID: SB04-INV-001
- Source raw note: N002, N003 mapped to RQ06, RQ08, RQ12.
- Expected behavior: Missing upstream artifact materialization now records a durable fingerprint event, deduplicates repeated requests, and requeues source work only when a real materialization target exists.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB04/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB04/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB04/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: Duplicate missing-upstream fingerprints do not repeatedly rerun the same source step, and absent materialization targets are recorded instead of ignored.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

