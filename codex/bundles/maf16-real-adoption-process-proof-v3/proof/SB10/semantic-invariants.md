# SB10 Semantic Invariants

- Invariant ID: `SB10-INV-001`
- Source raw note: `repo://codex/bundles/maf16-real-adoption-process-proof-v3/requirements/01-normalized-requirements.md` RQ07.
- Expected behavior: Artifact dedupe must not return an existing artifact when the projection identity belongs to a different step expectation in the same run.
- Disallowed shallow implementation: Process-run-wide projection identity reuse without step and expectation scope checks.
- Failing-first test: `bundle://proof/SB10/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB10/transcripts/passing.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` and `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`.
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` rejects wrong-scope projection identity reuse.
- Red-team negative case: `bundle://proof/SB10/transcripts/failing-first.txt` records the wrong-step collision case.
- Downstream dependency check: SB11 and SB13 rely on artifact records being bound to the correct expectation.
