# SB13 Semantic Invariants

- Invariant ID: `SB13-INV-001`
- Source raw note: `repo://codex/bundles/maf16-real-adoption-process-proof-v3/requirements/01-normalized-requirements.md` RQ09.
- Expected behavior: Operator read model and health classification must expose a recorded required artifact with matching content-unavailable validation diagnostic as `ContentUnavailable`, not `Satisfied`.
- Disallowed shallow implementation: UI-only status color changes or read-model satisfaction based only on artifact row existence.
- Failing-first test: `bundle://proof/SB13/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB13/transcripts/passing.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs`.
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs` matches persisted diagnostics to artifact obligations, and `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs` classifies `ContentUnavailable` as missing artifact risk.
- Red-team negative case: `bundle://proof/SB13/transcripts/failing-first.txt` records the old read-model shape that would have treated the artifact as satisfied.
- Downstream dependency check: SB15 and SB18 rely on operator-visible diagnostics before a live run.
