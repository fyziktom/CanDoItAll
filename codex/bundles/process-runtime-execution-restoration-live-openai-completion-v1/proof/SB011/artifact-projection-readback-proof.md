# SB011 Artifact Projection Readback Proof

## Status
Completed.

## Behavior Proven
- Process execution projects expected artifacts into `ProcessArtifactRecord` rows.
- Managed artifacts are persisted and inspected through storage-backed readback.
- Run detail readback exposes workflow/artifact links.
- Artifact handoff between mock agents preserves required output records and managed artifact content.

## Proof
- Focused integration transcript: `bundle://proof/SB011/transcripts/artifact-projection-readback-tests.txt`
- Source assertions: `bundle://proof/SB011/transcripts/artifact-projection-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB011/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB011/transcripts/anti-stub-and-runtime-host-drift-scan.txt`

## Included Tests
- `ProcessMockAgentRuntimeIntegrationTests.Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch`
- `ProcessMockAgentRuntimeIntegrationTests.Process_mock_three_agent_artifact_handoff_completes_required_outputs_without_full_delivery_process`
- `ProcessWorkflowExecutorIntegrationTests.Process_run_detail_api_includes_workflow_run_links`
