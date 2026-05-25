# SB07 Semantic Invariants

## SB07-INV-001

- Invariant ID: SB07-INV-001
- Source raw note: N006 mapped to RQ10, RQ11, RQ12.
- Expected behavior: ProcessDefinitionLinter analyzes editor models for ambiguous boundaries, weak workflow artifacts, subprocess parent mapping risks, missing branch dispositions, and decision-log/runtime-proof confusion.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB07/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB07/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: Legal approval decision logs do not trigger runtime-proof warnings, while finance approval branches and workflow artifacts produce targeted warnings.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

