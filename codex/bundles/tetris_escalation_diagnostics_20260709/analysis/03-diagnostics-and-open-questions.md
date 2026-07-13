# Diagnostics And Open Questions

## Adapter Diagnostics Seen On Root QA Step

From `api/target-run.json` and `api/target-history.json`:

1. `process.adapter.product_required_tool_receipt_missing`
   - Missing product tool receipts: `workspace_dotnet_run`, `browser_navigate`, `browser_snapshot`, `browser_take_screenshot`, `browser_console_messages`, `workspace_dotnet_stop`.

2. `process.adapter.required_tool_receipt_missing`
   - Missing process receipt contract entries for the same runtime/browser chain.

3. `process.adapter.completed_outcome_declares_unresolved_blocker`
   - QA returned Completed while text still said proof was missing and acceptance could not be granted.

4. `process.adapter.product_required_file_content_missing`
   - Default Blazor scaffold content still present in generated Tetris app files.

5. Final recovery decision
   - `ManagerRequired`
   - Source diagnostic: `process.adapter.required_tool_receipt_missing`
   - Policy: `process.current-step-safe-retry-budget-exhausted`
   - Retry detail: automatic retry `4/3`.

## Main Hypothesis

The QA step has two kinds of obligations mixed together:

- Acceptance proof obligations: run the app, navigate browser, collect screenshot/snapshot/console, stop app.
- Defect routing obligations: when deterministic product content gates fail, choose `repair-required` so implementation/repair work can run.

The current adapter appears to keep enforcing acceptance proof obligations even after QA selects `repair-required` for a product-content defect. That can turn a valid defect-routing decision into a manager escalation.

## Open Questions For Pro

1. Should required runtime/browser receipts be enforced only for `quality-accepted` branch outcomes in QA steps?
2. If `repair-required` is selected, should product content/readback failure be enough to route to `quality-repair` without browser proof?
3. Does `ProcessStepRecoveryInstructionBuilder` tell QA to preserve `repair-required` while the adapter still blocks because receipt checks are not branch-aware?
4. Does `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs` validate required tool receipts before considering branch outcomes or content-check failure categories?
5. Should `process.adapter.product_required_file_content_missing` be treated as a branch-routing success for `qa-validation`, similar to how content checks are branch-gated for `quality-accepted`?
6. Why did one QA attempt claim `quality-accepted` despite the product snapshot still containing Counter/Weather/MainLayout scaffold content?
7. Are evidence refs under `artifacts/process-runs/.../tool-runs/...` and `artifacts/scopes/.../process-runs/.../tool-runs/...` normalized consistently when checking current-run receipts?
8. Is retry budget consumed by diagnostics that should not retry the same QA step but should activate the repair branch?

## Suggested Source Hotspots

- `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs`: check ordering and branch-aware enforcement.
- `AgentFrameworkProcessExecutionAdapter.ProductCompletionParsing.cs`: branch outcome keys for content checks.
- `ProcessStepRecoveryInstructionBuilder.cs`: QA product content and receipt recovery guidance.
- `ProjectStructureProcessLaunchVariableContributor.cs`: required receipts/checks emitted for software-delivery QA and repair steps.
- `Templates/Processes/processes/software-delivery/definition.json`: branch outcomes and required operations for `qa-validation`, `quality-repair`, `qa-recheck`.
