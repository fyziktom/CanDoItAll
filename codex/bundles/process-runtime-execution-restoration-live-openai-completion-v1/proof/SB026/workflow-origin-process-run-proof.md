# SB026 Workflow-Origin Process Run Proof

## Status
Completed.

## Behavior Proven
- Workflow-origin process start uses `StartRunFromTriggerAsync` with `WorkflowRun` source metadata.
- Workflow-origin process run persists trigger source identity and requester into the run trigger reason.
- Workflow-origin process start does not introduce a workflow executor hook, workflow run link, or execution run.
- Missing workflow trigger source identity is rejected with typed validation errors.

## Proof
- Focused integration transcript: `bundle://proof/SB026/transcripts/workflow-origin-process-run-tests.txt`
- Source assertions: `bundle://proof/SB026/transcripts/workflow-origin-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB026/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB026/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
