# SB02 Semantic Invariants

- Invariant ID: SB02-INV-001
- Source raw note: Continue the process-dispatch decoupling work without starting a broad Process Core split.
- Expected behavior: Process-owned execution snapshots were added to neutral contracts without EF, UI, or AgentFramework references.
- Disallowed shallow implementation: Passing through old AgentFramework execution snapshots, adding stubs, hiding failures with fallback behavior, or introducing Process Core/driver-pack projects.
- Failing-first test: N/A - process boundary/non-production proof; the guard is enforced by architecture scans and targeted regression tests.
- Passing test: bundle://proof/SB02/transcripts/boundary-scans.txt
- Changed source files: repo://src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs
- Production assertions: Source scans and tests prove the execution boundary behavior without UI, EF, MAF product-module, or process tool changes.
- Red-team negative case: The boundary scan would fail if old execution result/detail/query/exception tokens reappear outside the adapter, or if contracts reference AgentFramework/EF/UI types.
- Downstream dependency check: Later artifact validation/projection isolation can proceed because execution snapshots, failures, and receipt observation no longer block the cutline.
