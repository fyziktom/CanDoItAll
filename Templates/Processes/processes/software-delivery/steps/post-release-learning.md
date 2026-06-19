# Capture first-pass post-release learning

Turn the release outcome into explicit learning about design, QA, operations, and process behavior.

Before returning Completed, write the post-release learning record to `artifacts/process-runs/<current-process-run-id>/steps/post-release-learning.md` and include that exact path in `evidenceRefs`. For a local generated application or output-folder handoff, treat production telemetry/support observations as not applicable when the release record explicitly bounded the run to local artifact delivery; still capture process lessons, proof gaps, and follow-up actions.

## Contract
- Inputs: Rollout outcome, telemetry record, support observations, and any release incident notes.
- Outputs: Post-release learning review with corrective actions and simulation updates.
- Evidence: Learning record at `artifacts/process-runs/<current-process-run-id>/steps/post-release-learning.md`, timeline, contributing factors, missing controls, next corrective actions, and a project_structure_node_create receipt for the learning decision when a project-structure target is present.
- Operation target scope: `ExternalActionControlled`
