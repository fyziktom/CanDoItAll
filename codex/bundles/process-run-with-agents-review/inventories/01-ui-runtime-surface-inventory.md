# UI Runtime Surface Inventory

## Existing UI Surfaces

- `ProcessWorkspaceRunsLaunchSection.razor`: launch plan creation, candidate selection, approval, provisioning, execute-ready launch.
- `ProcessWorkspaceRunsLifecycleSection.razor`: run history, selected run, step status controls, branch outcome selection.
- `ProcessWorkspaceRunsActiveSection.razor`: active AgentFramework execution summaries for active runs.
- `ProcessWorkspaceRunsExecutionSection.razor`: technical execution run details, approvals, checkpoints, tool receipts, produced artifacts.
- `ProcessWorkspaceRunsArtifactsSection.razor`: process artifacts, work briefs, decision records, conformance observations.
- `ProcessWorkspaceRunsCanvasSection.razor` and `ProcessCanvasSelectionPanel.razor`: runtime canvas selection and manual step actions.
- `ProcessWorkspace.LiveRefresh.cs`: 4-second refresh while active process or launch state exists.

## Missing UI Surfaces

- Outbox attempts and dead-lettered dispatch records.
- Required artifact expectation satisfaction matrix.
- Retry/recovery attempt ledger.
- Context-loss/crash recovery classification.
- Manual rerun action for failed or blocked agent-owned steps.
- Process health summary that combines run status, step status, outbox status, and AgentFramework execution health.
