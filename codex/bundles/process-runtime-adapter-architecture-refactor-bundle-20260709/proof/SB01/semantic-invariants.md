# SB01 Semantic Invariants

- Invariant ID: SB01-INV-ARCH-BASELINE
- Source raw note: Bundle required exact baseline inventory before refactor.
- Expected behavior: Adapter partial inventory is explicit and guarded by tests.
- Disallowed shallow implementation: Counting files without checking expected file names.
- Failing-first test: N/A process/non-production exemption; characterization gate.
- Passing test: `ProcessRuntimeArchitectureBaselineTests` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeArchitectureBaselineTests.cs`.
- Production assertions: Source audit in `bundle://proof/SB01/transcripts/passing.txt`.
- Red-team negative case: Old direct .NET setup executor symbol search would fail the source assertion.
- Downstream dependency check: CodeAnalytics snapshot `snap-20260709182007-390484e5` returned `cycles: []`.
