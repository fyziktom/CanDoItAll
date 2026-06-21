# SB18 Semantic Invariants

## Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB18-INV-001 | Step editor UI renders from `ProcessDefinitionStepEditorProjection` and step draft DTOs, not runtime entities, persistence entities, or direct template files. | `projection-boundary-scan.txt`, `source-assertions.txt`, component test `Step_editor_renders_operation_routes_artifacts_and_subprocess_mapping`. |
| SB18-INV-002 | Operation contracts use typed operation kinds and an explicit typed target scope; save rejects incomplete execution metadata. | Unit tests `Step_editor_projection_reads_operation_routes_artifacts_roles_and_subprocess_options` and `Step_editor_commands_save_add_branch_artifact_and_map_subprocess`; Playwright operation target/operation save action. |
| SB18-INV-003 | Branch routing is typed and loop-aware; backward routes require an explicit loop budget. | Unit test `Step_editor_rejects_backward_route_without_loop_budget`; Playwright route target `PreviousStep` plus loop budget save action. |
| SB18-INV-004 | Artifact expectations carry typed trust, sensitivity, retention, workflow output, child artifact, future usage, and validation/provenance summaries through projection and command flow. | Unit/component step editor tests and Playwright add-artifact receipt. |
| SB18-INV-005 | Subprocess mappings use typed definition options and child artifact mappings instead of free-form references. | Unit test `Step_editor_commands_save_add_branch_artifact_and_map_subprocess`; component typed-boundary test; Playwright subprocess definition selection and map receipt. |
| SB18-INV-006 | Stale step submissions fail predictably and do not silently overwrite a newer draft snapshot. | Unit test `Step_editor_rejects_stale_version_tokens`. |

## Shallow-Pass Trap

A shallow implementation could display labels for contracts, routing, artifacts, and subprocesses while storing edits as free-form strings or ignoring invalid route/subprocess rules. SB18 rejects that trap by asserting typed command payloads, stale-token rejection, loop-budget validation for backward routes, and Playwright action receipts rather than only checking rendered markup.

## Adversarial Negative Proof

`Step_editor_rejects_backward_route_without_loop_budget` submits a draft with a backward route target and no loop budget; the service returns a rejected receipt. `Step_editor_rejects_stale_version_tokens` submits a command with an outdated version token; the service rejects the command and keeps the projection version stable.

## Semantic Positive Proof

Focused unit tests load a realistic template and verify operation contracts, route metadata, role bindings, artifact expectations, and subprocess options. Component tests verify rendered sections and typed command payloads. Playwright loads `/processes`, selects the architecture governance definition, edits a step operation target, adds a branch outcome with a loop budget, adds an artifact expectation, maps a subprocess, asserts receipts, captures screenshots, and records browser console/network summary.

## Production Behavior Artifact Matrix

| Production artifact | Producer | Consumer | Lifecycle | Proof citation |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionStepEditorProjection` | Application step editor projection service | Process shell and step editor panel | Created from template step authoring defaults and refreshed after commands. | `test-unit-step-editor-sb18.txt`, `test-components-process-shell-sb18.txt`. |
| `ProcessDefinitionStepEditorCommand` | Step editor panel | Projection client and application service | Created from UI authoring state with typed command kind, step key, route/artifact keys, subprocess key, and expected version token. | `source-assertions.txt`, component command-boundary tests. |
| `ProcessDefinitionStepCommandReceipt` | Application step editor projection service | Shell state and receipt UI | Accepted/rejected outcome is rendered and carried with the returned projection. | `test-playwright-process-shell-sb18.txt`, unit negative tests. |
| Template step authoring defaults | Template loader and step summary builder | Step editor projection service | Canonical template JSON fields become typed step summaries consumed by the editor. | `test-unit-step-editor-sb18.txt`. |
| Subprocess mapping draft | Step editor panel command and service snapshot | Step editor projection and downstream launch/runtime subbundles | Selected subprocess definition and child artifact mappings are retained in typed draft state. | `Step_editor_commands_save_add_branch_artifact_and_map_subprocess`, `processes-definition-step-editor.png`. |
