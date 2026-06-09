# SB006 Semantic Invariants

- Invariant ID: `SB006_INV_001`
- Source raw note: Tests contain bundle names and bundle folders are being deleted.
- Expected behavior: C# source and tests no longer depend on concrete transient bundle folders; durable architecture assertions use stable test data or direct source checks.
- Disallowed shallow implementation: deleting assertions until tests pass, hiding concrete bundle names behind collapsed rows, or leaving repository-wide scans to read transient bundle artifacts.
- Failing-first test: `bundle://proof/SB006/transcripts/failing-first-head-bundle-path-scan.txt`
- Passing test: `bundle://proof/SB006/transcripts/passing-working-tree-bundle-path-scan.txt` and `bundle://proof/SB006/transcripts/full-unit-tests.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverFakeProofResistanceTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs`, and `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverEvidencePolicy.cs`
- Production assertions: supplied evidence URI policy does not approve transient bundle repository URIs; secret scanning no longer treats transient bundle artifacts as stable tracked proof inputs.
- Red-team negative case: `bundle://proof/SB006/transcripts/failing-first-head-bundle-path-scan.txt` proves the same source scan fails against HEAD before the repair.
- Downstream dependency check: SB007-SB009 may rely on full unit proof without needing historical bundle folders to exist.

