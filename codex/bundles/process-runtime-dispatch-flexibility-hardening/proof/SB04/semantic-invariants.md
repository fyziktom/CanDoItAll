# SB04 Semantic Invariants

- Invariant ID: SB04-EVIDENCE-POLICY
- Source raw note: N004, N007
- Expected behavior: Completion evidence, receipts, managed-artifact validation, retry policy, and issue-result creation remain driver-owned focused responsibilities.
- Disallowed shallow implementation: Keeping completion evidence mixed with unrelated launch or dispatch orchestration, or accepting missing product evidence silently.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: Product completion state, paths, receipts, retry policy, managed-artifact evidence, and issue results are split and covered by runtime adapter tests.
- Red-team negative case: Missing required evidence, ungrounded references, or readback failures remain visible in existing adapter result tests.
- Downstream dependency check: SB06 dispatch/recovery cleanup can rely on evidence policy without owning product-specific completion details.
