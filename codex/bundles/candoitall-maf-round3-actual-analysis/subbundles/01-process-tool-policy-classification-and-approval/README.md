# 01 - Process Tool Policy Classification and Approval

## Problem

Process tools mutate process definitions, process runs, assignments, transitions, and artifacts. Current mutation classification appears to include only workspace mutation tools. This can make process mutation tools invisible to:

- mutation approval requirements;
- repeat-signature mutation guards;
- finalizer sequence validation;
- tool policy telemetry.

## Required implementation

Update `AgentToolInvocationPolicyMetadata.IsMutationTool(...)` or introduce a tool metadata registry so that process mutation tools classify as mutation:

```text
processes_definition_save
processes_definition_publish
processes_definition_delete
processes_definition_import
processes_run_start
processes_step_transition
processes_assignment_resolve
processes_artifact_record
```

Keep read-only process tools classified as read:

```text
processes_definitions_list
processes_definition_editor_get
processes_definition_export
processes_runs_list
processes_run_detail_get
processes_analytics_get
processes_party_options_list
processes_executor_options_list
processes_templates_list
processes_template_get
processes_template_mermaid_get
```

Then align process tool exposure with approval policy:

- For interactive/human-facing agents, process mutation tools should require approval.
- For governed internal automation where approval is intentionally suppressed, the invocation policy must be explicit and logged.
- If provider/session cannot support required approval, do not expose process mutation tools.

## Acceptance criteria

- Unit tests verify each process mutation tool classifies as `Mutation`.
- Unit tests verify process read tools remain `Read`.
- Finalizer sequence validation treats process mutation after required finalizer as a violation.
- Tool policy logs classification for process tools.
- Provider approval filtering includes process mutation tools when they are wrapped.

## Suggested implementation approach

Prefer a central registry:

```csharp
public sealed record AgentToolPolicyMetadata(
    string Name,
    ToolInvocationClassification Classification,
    bool RequiresApprovalByDefault,
    bool IsStateChanging);
```

This avoids hardcoding long string lists in many places.

All source-code comments must be in English.

## Execution status

Completed. Process mutation/read tools are registered centrally, mutation tools require approval by default, MAF exposure wraps process mutations, and focused policy/finalizer/runtime tests pass.
