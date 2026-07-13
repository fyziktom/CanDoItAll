# Task 08 – Harden template and agent contracts without relying on longer prompts

## Problem

The current template contains strong prose instructions, but the agent still skipped the required helper. Prompt text alone is not enough.

## Template changes

### Add explicit execution class

Add a field to step metadata or integration metadata:

```json
{
  "executionClass": "AgentWithToolPlanGuard"
}
```

Valid values can start small:

- `AgentReasoningOnly`,
- `AgentWithToolPlanGuard`,
- `DeterministicToolPlan`,
- `RuntimeOwnedSubprocess`,
- `BranchDecision`.

### Add typed required tool plan

Add machine-readable required tool plan for `.NET solution setup` steps. It can be generated from existing launch variables at first, but template schema should be able to store it.

### Move subprocess bridge contract to template schema

For parent steps such as `prepare-solution-skeleton`, store accepted/no-go child outputs in the template definition, not only in `ProcessSubprocessContractResolver`.

### Validate template consistency

Template validation must verify:

- parent subprocess key exists,
- child definition exists,
- accepted child step keys exist,
- accepted child artifact slots exist,
- no-go child step keys exist,
- required tools are available for selected execution class,
- tool-critical launch variables are resolvable.

## Agent changes

Add explicit capability metadata to `.NET Application Developer`, e.g.:

```json
{
  "capabilities": [
    "dotnet.scaffold.solution",
    "dotnet.scaffold.blazorwasm",
    "dotnet.wire.solution-membership",
    "workspace.script.write-managed-helper",
    "workspace.script.run-pwsh-product-mutation",
    "product.readback.verify-file-content"
  ]
}
```

Readiness/assignment repair must match required step capabilities, not only tool names or generic role fit.

## Prompt changes

Only after typed enforcement is added, keep prompts shorter and more salient:

- state exact missing receipts,
- state exact readback failures,
- state exact resolved paths,
- remove duplicated broad prose where possible.

## Acceptance criteria

- `create-dotnet-project` is classified as guarded/deterministic, not generic reasoning work.
- Required tool plan is available as structured data.
- Template validation catches unresolved script refs before run.
- Parent subprocess accepted/no-go mapping is template-owned or at least template-validated.
- Agent readiness can fail due missing explicit capability even if generic tool name exists.

## Regression tests

```text
ProcessTemplateValidation_rejects_runtime_owned_subprocess_with_unknown_child_output_step
ProcessTemplateValidation_rejects_tool_plan_with_unresolved_script_ref
ProcessLaunchResolver_marks_dotnet_create_project_as_guarded_or_deterministic
AgentReadiness_requires_explicit_step_capability_for_dotnet_solution_wiring
```
