# SB18 Red-Team Semantic Proof

## Threat Model

The risky shallow pass is a form-only editor that appears to expose step sections but stores free-text route tokens, silently accepts invalid backward loops, ignores stale versions, or lets subprocess mappings bypass compatibility metadata.

## Rejection Evidence

- Backward route without loop budget is rejected by `Step_editor_rejects_backward_route_without_loop_budget`.
- Stale version token is rejected by `Step_editor_rejects_stale_version_tokens`.
- Old legacy editor symbols are absent from modified active code according to `bundle://proof/SB18/scans/old-symbol-scan.txt`.
- Projection/UI boundary scan has no EF, runtime, dispatcher, old observation, or DOM parsing references in the step editor implementation.
- Anti-stub scan has no TODO, stub, or `NotImplementedException` matches in modified SB18 files.

## Positive Evidence

- `Step_editor_projection_reads_operation_routes_artifacts_roles_and_subprocess_options` proves template-backed fields become typed projection data.
- `Step_editor_commands_save_add_branch_artifact_and_map_subprocess` proves the service accepts realistic typed save, branch, artifact, and subprocess commands.
- Component tests prove the Blazor shell emits typed command payloads for save, route/artifact changes, and subprocess mapping.
- Playwright proves the browser flow reaches the real shell and completes save, add-branch, loop-budget save, add-artifact, subprocess map, and screenshot capture without Blazor error UI, page errors, or unexpected failed requests.

## Decision

The implementation is not a static/render-only facade. It has typed producer/consumer command flow, validation failures for unsafe cases, and browser proof for the user-facing workflow.
