# Process Dispatch Tool Validation And Recovery Boundary v1

Bundle preparation status: `Ready`
Bundle readiness gate: `Passed prepared-stage validator`
Execution status: `Completed`
Subbundle gate review: `Passed`
Final closure gate: `Passed completed-stage validator`
Browser validation analytics: `N/A confirmed`
Profile: `initiative`
Prepared date: `2026-06-04`
Target branch: `maf-processes-refactor`
Prepared reviewed branch head: `df98a1066e59baa014f05799cfedd80db6ac0aee`
Execution branch head: `75ff7adab008d037b5397dd4288a28ccc8d385d5`

## Validation Summary

- Bundle preparation status: `Ready`
- Bundle readiness gate: `Passed prepared-stage validator`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed completed-stage validator`
- Browser validation analytics: `N/A confirmed`
- Prepared-stage validation passed before production implementation started.
- Execution and closure proof is recorded under `proof/SBxx/`.
- Browser validation remained N/A because this bundle changed runtime/service dispatch logic only.

## Purpose

Continue the `maf-processes-refactor` dispatcher decomposition without starting `CanDoItAll.Processes.Core`.
The previous artifact validation bundle extracted local artifact validation rule families and reduced `ArtifactValidation.cs`.
The next safe seam is the tool-validation and completion/recovery decision area in `ProcessRunAutomationDispatchService.ToolValidation.cs`,
with narrow supporting scans over `StepCompletionFinalizer`, `RecoveryDirective`, and `RecoveryPackets`.

This bundle must reduce dispatcher coupling by creating process-module-local snapshots and helpers first, then migrating one narrow consumer at a time.

## Hard Constraints

- Do **not** create `CanDoItAll.Processes.Core`.
- Do **not** create process driver packs, `IProcessDriverPack`, `ProcessDriver`, or `DriverPack` production APIs.
- Do **not** reintroduce MAF, Workbench, Projects, or product-tool dependencies into `CanDoItAll.AgentFramework.Maf` or `CanDoItAll.AgentFramework.Tooling`.
- Do **not** move EF entities, `DbContext`, storage placement, process state mutation, or final step transitions into pure helper classes.
- Do **not** rename process tools, required-tool names, artifact keys, provider keys, or receipt metadata fields.
- Do **not** run small, medium, mobile, phone, tablet, Android, iPhone, or responsive screenshot proof.
- Browser proof is expected to be `N/A`; if UI proof unexpectedly becomes necessary, use only large desktop/PC proof and document why.

## Mission

Create a module-local tool-validation and recovery-decision boundary that:

1. Preserves all existing dispatcher behavior.
2. Moves pure required-tool, receipt, critical-failure, carry-forward, and completion-decision logic behind typed local helpers.
3. Keeps source orchestration and side effects in the dispatcher.
4. Prepares a driver-readiness semantic map for later process helper drivers without implementing driver APIs yet.
5. Leaves a clear cutline for the next dispatcher isolation bundle.

## Why This Is Not Process Core Yet

The current source is closer to a future Process Core than before, but still not ready.
Tool validation and recovery/finalization decisions still depend on dispatcher state, execution snapshots, artifact satisfaction, carried proof,
retry decisions, provider fallback, and declared step outcomes. These seams need module-local isolation and parity tests before any core extraction.

## Expected Outcome

A completed implementation should leave:

- `ProcessRunAutomationDispatchService.ToolValidation.cs` smaller and more orchestration-focused.
- New helpers under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/` only.
- Focused tests proving missing required tools, critical failures, carry-forward rules, completion status/reason, and recovery retry behavior.
- Final scans proving no Process Core, no driver packs, no MAF/Tooling product coupling, no prohibited viewport proof artifacts.
- A driver-readiness map documenting which tool-validation semantic families future drivers can satisfy later.
