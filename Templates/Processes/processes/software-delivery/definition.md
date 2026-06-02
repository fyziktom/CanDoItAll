# Multi-team software delivery and release governance

**Key:** `software-delivery`
**Criticality:** High
**Autonomy level:** Guarded

.NET-focused multi-team delivery template for planned software change with explicit app-type classification, architecture design and review, subprocess-backed implementation, QA, runtime command writeback, UI screenshot writeback, security, release, deployment, and retrospective governance.

## Value
Delivers .NET application changes through typed, observable subprocesses that keep architecture, implementation, validation, runtime commands, screenshots, release authority, and project-structure evidence explicit.

## Permission model
Architecture, implementation, validation, runtime command writeback, screenshot writeback, security, and release gates have explicit operation contracts. `allowedOperations` and `operationTargetScope` must stay source-aligned with the canonical ProcessStepOperation and ProcessStepTargetScope catalogs. Architects and QA reviewers do not mutate product files; implementation and repair remain the only product-mutable lanes.

## Steps
### 1. Clarify .NET scope and app type boundary (`feature-intake`)
- Step kind: Start
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: None
- Outputs: Decision-ready .NET scope packet with acceptance boundary, app-type hypothesis, dependency map, assumptions, exclusions, and validation hooks.
- Evidence: Intake notes, acceptance criteria, .NET app-type hypothesis, product root hints, UI/no-UI hints, run/test command hints, known exclusions, assumptions, and unresolved dependency register.

### 2. Run .NET architecture design and review subprocess (`architecture-review`)
- Step kind: Subprocess
- Operation target scope: ExternalActionControlled
- Depends on: feature-intake
- Subprocess: `dotnet-architecture-design-review`
- Outputs: Observed child architecture run with app-type classification, reviewed design decision, implementation-ready handoff, and unresolved architecture risks.
- Evidence: Child run status, .NET app classification, architecture decision, review findings, implementation start criteria, and UI/no-UI applicability.

### 3. Run .NET implementation slice subprocess (`implementation`)
- Step kind: Subprocess
- Operation target scope: ExternalActionControlled
- Depends on: feature-intake, architecture-review
- Subprocess: `dotnet-development-slice`
- Outputs: Observed child implementation slice with reviewable change set, test evidence, blockers, rollout inputs, and parent-ready handoff.
- Evidence: Child run status, change-set projection, validation outputs, output-placement notes, migration steps when applicable, touched-surface inventory, and blockers.

### 4. Complete peer review and integration readiness (`peer-review`)
- Step kind: Review
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: architecture-review, implementation
- Outputs: Peer-reviewed change set with explicit residual risk and follow-up items.
- Evidence: Review notes, unresolved issues list, and approved follow-up actions.

### 5. Run QA validation and runtime or browser proof (`qa-validation`)
- Step kind: Review
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: implementation, architecture-review, peer-review
- Outputs: Targeted QA result with runtime/API/browser evidence as applicable, regressions, warning and executed-test counts, shipped entrypoint/runtime consistency, residual quality risk, and an explicit accepted or repair-required branch. Browser-workflow quality acceptance requires process-visible screenshot, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion.
- Evidence: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, runtime/API/browser proof as applicable, screenshots for UI surfaces, defect notes, and current-run process-visible browser artifacts when a visible browser workflow is in scope.

### 6. Repair validation findings (`quality-repair`)
- Step kind: Work
- Operation target scope: ExternalProductTargetMutable
- Depends on: implementation, architecture-review, peer-review, qa-validation/repair-required
- Outputs: Repaired change set and validation notes ready for QA recheck.
- Evidence: Changed files or deliverables, repair rationale, rerun validation, and remaining risks.

### 7. Re-run QA validation and runtime or browser proof after repair (`qa-recheck`)
- Step kind: Review
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: quality-repair, qa-validation/repair-required, implementation
- Outputs: Recheck result with warning-free validation, nonzero executed-test proof when tests are expected, shipped entrypoint/runtime consistency, runtime/API/browser evidence as applicable, regression evidence, and explicit quality disposition. Browser-workflow repair acceptance requires fresh process-visible screenshot, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion.
- Evidence: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, runtime/API/browser proof as applicable, screenshots for UI surfaces, repair verification, unresolved defects if any, and fresh current-run process-visible browser artifacts when a visible browser workflow is in scope.

### 8. Perform security and data-handling review (`security-review`)
- Step kind: Approval
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: implementation, architecture-review, peer-review, qa-validation/quality-accepted
- Outputs: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.

### 9. Record .NET run commands under process run node (`record-runtime-commands`)
- Step kind: Subprocess
- Operation target scope: ExternalActionControlled
- Depends on: implementation, architecture-review, qa-validation/quality-accepted
- Subprocess: `dotnet-runtime-command-writeback`
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.

### 10. Capture and store .NET UI screenshots (`capture-ui-screenshots`)
- Step kind: Subprocess
- Operation target scope: ExternalActionControlled
- Depends on: qa-validation/quality-accepted, record-runtime-commands
- Subprocess: `dotnet-ui-screenshot-writeback`
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.

### 11. Approve first-pass release readiness (`release-approval`)
- Step kind: Approval
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: implementation, architecture-review, qa-validation/quality-accepted, security-review, record-runtime-commands, capture-ui-screenshots
- Outputs: Approved or rejected release readiness with accountable rationale and boundary-applicable conditions only.
- Evidence: Approval note, residual risk register, rollback/removal ownership record, declared-boundary confirmation, Run command node references, Screenshots parent/image asset or no-UI evidence, and confirmation that QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts.

### 12. Execute first-pass controlled release rollout (`execute-release-rollout`)
- Step kind: Delivery
- Operation target scope: ExternalActionControlled
- Depends on: release-approval
- Outputs: Executed rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence: Operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.

### 13. Capture first-pass post-release learning (`post-release-learning`)
- Step kind: End
- Operation target scope: ExternalActionControlled
- Depends on: execute-release-rollout
- Outputs: Post-release learning review with corrective actions and simulation updates.
- Evidence: Timeline, contributing factors, missing controls, next corrective actions, and a project_structure_node_create receipt for the learning decision when a project-structure target is present.

### 14. Perform security review after repair (`security-review-after-repair`)
- Step kind: Approval
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: implementation, architecture-review, quality-repair, qa-recheck/quality-accepted
- Outputs: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.

### 15. Record repaired .NET run commands under process run node (`record-runtime-commands-after-repair`)
- Step kind: Subprocess
- Operation target scope: ExternalActionControlled
- Depends on: quality-repair, implementation, architecture-review, qa-recheck/quality-accepted
- Subprocess: `dotnet-runtime-command-writeback`
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.

### 16. Capture and store repaired .NET UI screenshots (`capture-ui-screenshots-after-repair`)
- Step kind: Subprocess
- Operation target scope: ExternalActionControlled
- Depends on: qa-recheck/quality-accepted, record-runtime-commands-after-repair
- Subprocess: `dotnet-ui-screenshot-writeback`
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.

### 17. Escalate unresolved repair findings (`repair-escalation`)
- Step kind: Approval
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: qa-recheck/repair-escalation, quality-repair
- Outputs: Explicit no-go, scope reset, or replan decision with accountable owner.
- Evidence: Escalation decision, unresolved defect list, required next repair scope, and owner.

### 18. Approve repaired release readiness (`release-approval-after-repair`)
- Step kind: Approval
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: implementation, architecture-review, qa-recheck/quality-accepted, security-review-after-repair, record-runtime-commands-after-repair, capture-ui-screenshots-after-repair
- Outputs: Approved or rejected repaired release readiness with accountable rationale and boundary-applicable conditions only.
- Evidence: Approval note, residual risk register, rollback/removal ownership record, declared-boundary confirmation, repaired Run command node references, Screenshots parent/image asset or no-UI evidence, and confirmation that repaired QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts.

### 19. Execute repaired controlled release rollout (`execute-release-rollout-after-repair`)
- Step kind: Delivery
- Operation target scope: ExternalActionControlled
- Depends on: release-approval-after-repair
- Outputs: Executed repaired rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence: Operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.

### 20. Capture repaired-release learning (`post-release-learning-after-repair`)
- Step kind: End
- Operation target scope: ExternalActionControlled
- Depends on: execute-release-rollout-after-repair
- Outputs: Post-release learning review with corrective actions and simulation updates.
- Evidence: Timeline, contributing factors, missing controls, next corrective actions, and a project_structure_node_create receipt for the learning decision when a project-structure target is present.
