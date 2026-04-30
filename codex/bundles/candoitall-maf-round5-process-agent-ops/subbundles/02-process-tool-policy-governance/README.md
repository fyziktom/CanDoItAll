# 02 Process Tool Policy Governance

## Goal

Process mutation tools must be governed as mutations, not read-like tools.

## Tasks

1. Replace broad policy exception handling with `AgentToolPolicyBlockedException`.
2. Respect `BuiltInToolConfiguration.Enabled` in `IsBuiltInToolEnabled(...)`.
3. Add an explicit process tool metadata registry.
4. Classify these as mutations at minimum: `processes_definition_save`, `processes_definition_publish`, `processes_definition_delete`, `processes_definition_import`, `processes_template_import`, `processes_run_start`, `processes_step_transition`, `processes_assignment_resolve`, `processes_artifact_record`.
5. Deny unknown `processes_*` tools by default.
6. Approval-wrap or deny process mutation tools based on runtime policy and provider capability.
7. Add behavior tests for disabled tools, unknown process tools, mutation classification, approval wrapping, and real tool failure exception passthrough.

## Acceptance criteria

- Process mutation tool calls cannot bypass policy.
- Real tool exceptions are not reported as policy blocks.
- Disabled tools are not exposed to the model.
