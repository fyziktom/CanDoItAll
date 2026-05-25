# SB01 Semantic Invariants

## SB01-INV-001

- Invariant ID: SB01-INV-001
- Source raw note: N004 mapped to RQ01, RQ02, RQ11, RQ12.
- Expected behavior: Boundary metadata is computed per process step, carried in execution metadata, maps cooperation profiles to read-only or mutating tool access, and allows external artifact destinations only when explicitly grounded.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB01/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB01/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB01/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: A Blazor architecture step is classified AnalysisDesign and does not receive mutable product aliases; explicit business-plan artifact destinations remain writable.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

