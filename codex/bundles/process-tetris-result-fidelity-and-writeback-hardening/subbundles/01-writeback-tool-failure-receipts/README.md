# writeback-tool-failure-receipts

## Status

- `Ready`

## Objective

Make required project-structure writeback tool failures auditable and recoverable so a final writeback step cannot end in the observed invalid state: claimed tool failure, no failed receipt, process failure.

## Covered Inputs

- N004, N005.
- Requirements R001, R002.

## Prerequisites

- Confirm the HR duplicate-template-key fix is present and the app can execute process APIs.
- Read `bundle://evidence/run-0cca729a-detail.json` and `bundle://evidence/06-project-structure-result-writeback-summary.md`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureRuntimeGateway.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Runtime/tool-path change that records failed project-structure tool attempts with safe actionable diagnostics.
- Completion-evaluation behavior that still rejects blocked required-tool claims when no failed receipt exists.
- Recovery packet or escalation content that includes failed project-structure tool name, safe reason, target project id, target node id, and source workspace path when present.
- Focused tests for positive and negative branches.

## Dependency Impact

- SB04 depends on this. Without this subbundle, the final rerun can fail after artifacts are written but before project-structure closure.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof in `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md`.

## Implementation Steps

1. Reproduce the no-failed-receipt writeback path with a focused test or an existing fixture.
2. Identify where MAF project-structure tool exceptions are converted into execution detail/tool receipts.
3. Add safe failed-receipt recording for `project_structure_asset_create` and `project_structure_node_create` failures.
4. Preserve the existing invalid-outcome guard for claimed required-tool failure with no receipt.
5. Ensure `Function failed` is not the only diagnostic available to the agent/runtime; include safe code/message.
6. Add tests for:
   - no failed receipt remains invalid,
   - failed receipt makes blocked recovery/escalation valid,
   - successful writeback still requires successful required tools.

## Scope Exceptions

- Do not manually create project-structure nodes for run `0cca729a...` inside this subbundle.
- Do not weaken required tool enforcement to allow prose-only writeback.

## Do Not Do

- Do not treat `workspace_write_file` evidence artifacts as a substitute for project-structure writeback.
- Do not swallow exceptions or add a fallback that hides failed asset creation.
- Do not log sensitive file contents or raw media payloads.

## Acceptance Checklist

- [ ] Failed project-structure tool calls produce durable failed receipts or equivalent governed platform error records.
- [ ] Blocked claims without failed receipts still fail with the existing governance message.
- [ ] Recovery/escalation includes the exact required tool names and safe diagnostics.
- [ ] Focused tests pass.

## Proof Required

- `bundle://proof/SB01/manifest.md` with changed-file hashes, test transcript paths, source assertions, anti-stub audit, and production behavior artifact matrix.
- `bundle://proof/SB01/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, and production behavior artifact matrix.
- Test command transcript for the affected process runtime/tool tests.

## Browser Validation Logging

- N/A. This subbundle is process runtime/tool governance.

## Progression Gate

- SB02/SB04 may proceed only after the focused tests prove both no-receipt rejection and failed-receipt recovery behavior.

## Suggested Agent Prompt

```text
Implement only SB01. Make project-structure writeback tool failures durable and auditable without weakening governed required-tool validation. Prove no-failed-receipt blocked outcomes still fail, and failed project-structure tool receipts enter the intended recovery/escalation path.
```
