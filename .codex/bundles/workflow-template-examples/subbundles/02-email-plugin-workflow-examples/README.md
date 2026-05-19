# 02-email-plugin-workflow-examples

## Status

- `Completed`

## Objective

- Add plugin-backed email task workflow examples for Gmail and Office365 that identify concrete email tasks and create task nodes under the specified project-structure node, while preserving existing email summary examples.

## Success Criteria

- New Gmail and Office365 task workflow keys load from `email-plugin-task-workflows.yaml`.
- Each workflow downloads one bounded email message, asks the LLM to classify and extract tasks, routes task/asap outcomes to `CreateTaskNodes`, routes informational/no-action outcomes to `CreateAsset`, and marks the source message processed after storage or task creation.
- Task creation nodes set `includeInputPayload: true` so mark-processed nodes can read message IDs.

## Covered Inputs

- R3, R4, R5.
- Raw notes `N001` and `N002`.

## Prerequisites

- `01-template-pack-file-loading-foundation` closure gate passed or manifest file loading otherwise proven.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Workflows\workflows`
- `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ProjectStructureWorkflowExecutor.cs`

## Deliverables

- `gmail-label-email-tasks-to-project` template.
- `office365-category-email-tasks-to-project` template.
- New file to create: `C:\repositories\CanDoItAll\Templates\Workflows\workflows\email-plugin-task-workflows.yaml`.
- Loader/test assertions for both keys and existing summary keys.

## Dependency Impact

- Email examples are independent from file-analysis examples but share manifest loading. Weak JSON-path proof would produce seeded examples that fail at runtime, so closure requires graph/settings assertions.

## Validation Depth

- Process-critical template closure.

## Implementation Steps

1. Create `email-plugin-task-workflows.yaml`.
2. Define Gmail task workflow with download, LLM classify, SWITCH, project-structure task/summary branches, mark processed, and end nodes.
3. Define Office365 task workflow with equivalent category-based flow.
4. Add assertions that the workflows load and contain expected executor IDs/settings.
5. Update execution report with test proof.

## Scope Exceptions

- Live Gmail and Office365 OAuth execution is not required for this template-data change.

## Do Not Do

- Do not add plugin OAuth scopes or executor behavior changes unless current templates cannot be expressed with existing executors.
- Do not create tasks for informational emails; route those to summary assets.

## Acceptance Checklist

- Gmail task workflow key loads.
- Office365 task workflow key loads.
- Both graphs compile through `CreateGraph`.
- Both graphs contain project-structure `CreateTaskNodes` and `CreateAsset` branches.
- Both graphs contain mark-processed executor nodes.

## Proof Required

- Targeted unit test covering the new email task templates.
- Bundle execution report row updated.

## Browser Validation Logging

- N/A - no browser-visible UI changes.

## Progression Gate

- This subbundle passes when the email task templates load, graph construction succeeds, and targeted assertions prove task creation and mark-processed paths exist for both providers.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
