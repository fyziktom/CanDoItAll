# Approve first-pass release readiness

Approve or reject release using first-pass QA proof, shipped entrypoint/runtime consistency, security posture, rollback readiness, support coverage, and the declared release boundary. Conditions must apply to the approved boundary; out-of-boundary production hardening belongs in future recommendations unless explicitly required.

For a local generated application, artifact handoff, static export, or non-production deliverable, do not block solely because there is no separate pre-existing support ownership note. If QA, security, runtime-command, and screenshot/no-UI evidence are sufficient, the release approval record itself must name bounded support and rollback/removal owners from the process role context, project assignment context, or implementation handoff. Mark production telemetry, live service ownership, and deployment-window controls as not applicable when the approved boundary has no live production host.

Before returning Completed, write the release approval record to `artifacts/process-runs/<current-process-run-id>/steps/release-approval.md` and include that exact path in `evidenceRefs`. Do not rely on `artifacts/process-runs/<current-process-run-id>/release-approval.md` as the only approval record reference, because the runtime validates produced managed artifacts through the `steps/` path.

When UI screenshots are applicable, accept current-run screenshot evidence from the screenshot writeback child run referenced by the parent `capture-ui-screenshots` step. The durable screenshot evidence may live under the child run, for example `artifacts/process-runs/<child-run-id>/steps/screenshot-handoff.md`, `artifacts/process-runs/<child-run-id>/steps/capture-ui-screenshots.md`, `artifacts/process-runs/<child-run-id>/browser/*.png`, and the Screenshots parent or image asset node ids. Do not block solely because accepted screenshot files are stored under the child process-run artifact root instead of copied into the parent run root.

When release approval depends on visual UI claims, require current-run `workspace_analyze_image` or `workspace_analyze_images` receipts from QA or screenshot writeback evidence. Do not approve from screenshot file paths, dimensions, project-structure image asset ids, or chat summaries alone.

Block only when the current evidence is missing a boundary-critical proof input, the release boundary is unclear, the shipped entrypoint/runtime cannot be tied to QA proof, security review rejected the boundary, rollback/removal cannot be described, or no accountable support/rollback owner can be identified from either upstream evidence or the current release decision context.

## Contract
- Inputs: QA evidence that names the shipped entrypoint and referenced runtime, security outcome, Run command nodes, UI screenshot or no-UI evidence, rollback/removal plan or removable-artifact statement, upstream or decision-recorded support ownership, and declared release boundary.
- Outputs: Approved or rejected release readiness with accountable rationale, boundary-applicable conditions only, and the support/rollback ownership record when approval proceeds.
- Evidence: Approval note at `artifacts/process-runs/<current-process-run-id>/steps/release-approval.md`, residual risk register, rollback/removal ownership record, declared-boundary confirmation, Run command node references, Screenshots parent/image asset or no-UI evidence, provider-backed image-analysis receipts for visual approval claims, and confirmation that QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts.
- Operation target scope: `ExternalProductTargetReadOnly`
