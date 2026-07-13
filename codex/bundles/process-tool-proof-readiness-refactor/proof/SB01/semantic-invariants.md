# SB01 Semantic Invariants

- Invariant ID: `SB01-required-current-run-receipts`
- Source raw note: `bundle://requirements/01-normalized-requirements.md`
- Expected behavior: Process steps can declare typed required tool receipts, and completion rejects missing or stale current-run receipts.
- Disallowed shallow implementation: Do not enforce required proof only through prompt text or artifact summaries.
- Failing-first test: `Completion_rejects_stale_process_capability_scope_tool_receipt`
- Passing test: `Completion_accepts_process_capability_scope_current_run_tool_receipt`
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- Production assertions: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` calls the typed gate before accepting completion.
- Red-team negative case: A successful outcome with an old `browser_take_screenshot` receipt is rejected.
- Downstream dependency check: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeCapabilityScopeModels.cs` carries generic receipt metadata without process-template references.

