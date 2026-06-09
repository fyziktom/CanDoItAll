# SB013 MAF Workflow Runtime Proof

## Status
Completed.

## Behavior Proven
- Workflow-backed process role dispatch starts workflow assignments.
- Workflow completion maps to completed process step state and workflow run links.
- Human-input workflow state maps to `WaitingApproval`.
- Workflow output artifacts are available through process run detail readback.

## Proof
- Focused integration transcript: `bundle://proof/SB013/transcripts/maf-workflow-role-runtime-tests.txt`
- Source assertions: `bundle://proof/SB013/transcripts/maf-workflow-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB013/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB013/transcripts/anti-stub-and-runtime-host-drift-scan.txt`

## Included Tests
- `ProcessWorkflowExecutorIntegrationTests.DispatchAsync_runs_workflow_assignment_and_projects_process_link`
- `ProcessWorkflowExecutorIntegrationTests.DispatchAsync_maps_human_input_workflow_to_waiting_approval`
- `ProcessMockAgentRuntimeIntegrationTests.Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch`
