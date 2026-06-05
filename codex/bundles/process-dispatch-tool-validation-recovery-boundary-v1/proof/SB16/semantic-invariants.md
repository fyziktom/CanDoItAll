# SB16 Semantic Invariants

- Invariant ID: `SB16-FINAL-RED-TEAM`
- Source raw note: Do not rush Process Core, do not add driver APIs prematurely, and do not create prohibited viewport proof artifacts.
- Expected behavior: Final closure records hashes, tests, scans, full build, raw-note closure, and driver-readiness documentation without adding broad production surface.
- Disallowed shallow implementation: Leaving status placeholders, omitting hashes, skipping anti-stub scans, or creating a core/driver production surface.
- Failing-first test: N/A - process closure proof; no new production behavior was added in SB16.
- Passing test: `bundle://proof/SB16/transcripts/final-source-scans.txt`, `bundle://proof/SB15/transcripts/full-solution-build.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://codex/bundles/process-dispatch-tool-validation-recovery-boundary-v1/reviews/01-execution-report.md`
- Production assertions: Extracted helpers are local, typed, non-stubbed, and covered by focused tests plus full build.
- Red-team negative case: Final scans reject Process Core, production driver APIs, prohibited viewport proof file paths, and helper stubs.
- Downstream dependency check: Next dispatcher cutline is documented in the execution report and inventories.
