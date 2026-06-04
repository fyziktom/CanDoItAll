# SB10 Semantic Invariants

- Invariant ID: SB10-INV-001
- Source raw note: Continue the process-dispatch decoupling work without starting a broad Process Core split.
- Expected behavior: Build, unit guardrails, client tests, helper tests, dispatch tests, and scans prove the snapshot boundary stays consistent after helper migration.
- Disallowed shallow implementation: Passing through old AgentFramework execution snapshots, adding stubs, hiding failures with fallback behavior, or introducing Process Core/driver-pack projects.
- Failing-first test: N/A - process boundary/non-production proof; the guard is enforced by architecture scans and targeted regression tests.
- Passing test: bundle://proof/SB10/transcripts/unit-boundary-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationReceiptObservationHelper.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationReceiptObservationHelperTests.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: Source scans and tests prove the execution boundary behavior without UI, EF, MAF product-module, or process tool changes.
- Red-team negative case: The boundary scan would fail if old execution result/detail/query/exception tokens reappear outside the adapter, or if contracts reference AgentFramework/EF/UI types.
- Downstream dependency check: Later artifact validation/projection isolation can proceed because execution snapshots, failures, and receipt observation no longer block the cutline.
