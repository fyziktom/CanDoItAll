# SB03 Semantic Invariants

- Invariant ID: SB03-INV-GATE-RECEIPTS
- Source raw note: Bundle required completion gate and receipt behavior to be directly testable outside adapter construction.
- Expected behavior: Required receipts and completion issues are evaluated by top-level services.
- Disallowed shallow implementation: Adapter-only tests that hide gate behavior behind full process execution.
- Failing-first test: N/A process/non-production exemption; negative service tests cover missing receipt and gate failure.
- Passing test: `ProcessCompletionGateEvaluatorTests` in `bundle://proof/SB03/transcripts/passing.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs`.
- Production assertions: Direct service tests and focused suite in `bundle://proof/SB03/transcripts/passing.txt`.
- Red-team negative case: Missing receipt scenarios fail direct gate tests.
- Downstream dependency check: CodeAnalytics snapshot `snap-20260709182007-390484e5` returned `cycles: []`.
