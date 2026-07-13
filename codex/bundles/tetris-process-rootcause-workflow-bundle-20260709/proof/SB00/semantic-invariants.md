# SB00 Semantic Invariants

- Invariant ID: `SB00-INV-branch-regression`
- Source raw note: GPTPro RC1, RC2, RC4, RC8 and the user request that Tetris is one example of a wider process-template failure.
- Expected behavior: Accepted-branch proof failures, repair-branch defect evidence, stale runtime proof, and template metadata gaps are represented by tests before relying on later fixes.
- Disallowed shallow implementation: A fixture that only checks one Tetris file or one hardcoded branch key.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `QualityAccepted_with_full_browser_receipts_requires_acceptance_criteria_ids` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- Production assertions: Adapter completion gates reject missing acceptance criteria and stale runtime/browser evidence through real diagnostic codes.
- Red-team negative case: Repair branch without deterministic defect evidence remains rejected.
- Downstream dependency check: SB01-SB04 use the same branch/receipt diagnostics rather than introducing independent fixture-only assertions.
