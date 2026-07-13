# SB03 Semantic Invariants

- Invariant ID: `SB03-missing-proof-manager-retry`
- Source raw note: `bundle://requirements/01-normalized-requirements.md`
- Expected behavior: A blocked or completed step missing typed current-run proof is routed through a manager retry diagnostic instead of artifact-only success.
- Disallowed shallow implementation: Do not treat a written markdown artifact as a substitute for required runtime receipts.
- Failing-first test: `Blocked_step_with_missing_process_receipt_gets_manager_retry_diagnostic`
- Passing test: `Blocked_step_with_missing_process_receipt_gets_manager_retry_diagnostic`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- Production assertions: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` emits `process.adapter.required_tool_receipt_blocked_retry`.
- Red-team negative case: A blocked outcome that says `browser_take_screenshot` is missing receives a proof-specific manager retry diagnostic.
- Downstream dependency check: The diagnostic uses the same typed receipt gate as completion validation.

