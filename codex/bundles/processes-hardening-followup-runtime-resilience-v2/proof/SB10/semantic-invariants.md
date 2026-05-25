# SB10 Semantic Invariants

## SB10-INV-001

- Invariant ID: `SB10-INV-001`
- Source raw note: N001, N002, N003, N004, N005, N006, N007
- Expected behavior: the runtime hardening remains generic and passes red-team scenarios for software and non-software processes while rejecting shallow or stale proof.
- Disallowed shallow implementation: Blazor/.NET/JavaScript hardcoding in core process rules, count-only proof, placeholder artifact acceptance, or green tests that do not exercise production paths.
- Failing-first test: `bundle://proof/SB10/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB10/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB10/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions.txt`
- Red-team negative case: business/legal/manufacturing/research artifacts are accepted without software-only runtime proof while software wrong-root and stale evidence remain rejected.
- Downstream dependency check: final closure validates SB01-SB09 proof files and all command results.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Generic red-team validation suite | `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs` and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `bundle://reviews/01-execution-report.md` | `bundle://proof/SB10/transcripts/passing.txt` | `bundle://proof/SB10/transcripts/failing-first.txt` |
