# SB08 Semantic Invariants

## SB08-INV-001

- Invariant ID: SB08-INV-001
- Source raw note: N004, N005, N006 mapped to RQ11, RQ12.
- Expected behavior: Integration red-team tests cover software architecture drift, external artifact destinations, non-software artifact validation, subprocess lineage, disposition routing, retry compression, and definition linting.
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/SB08/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/SB08/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs
- Production assertions: bundle://proof/SB08/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: The red-team cases reject architecture product mutation, malformed JSON artifacts, stale lineage, missing upstream routing, repeated no-progress retries, and weak process definitions.
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.

