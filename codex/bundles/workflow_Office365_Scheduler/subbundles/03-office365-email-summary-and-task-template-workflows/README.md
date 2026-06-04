# 03-office365-email-summary-and-task-template-workflows

## Status

- Status: `Completed`

## Objective

Add managed workflow templates for recurring Office365 email-watch summary and task creation scenarios.

## Covered Inputs

- R5: summary workflow stores Markdown summary asset under configured project/node and then marks message processed.
- R6: task workflow creates project task nodes under configured project/node and then marks message processed.
- R12: templates are file-backed under `Templates/Workflows` and loaded through the manifest.

## Prerequisites

- SB02 executor behavior and output contract passed closure.
- Project-structure workflow write executors and template loader behavior are understood.

## Exact Source References

- `repo://Templates/Workflows/manifest.yaml`
- `repo://Templates/Workflows/workflows/workflow-executor-catalog-workflows.yaml`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/ProjectStructureWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/JsonTransformWorkflowExecutor.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureWorkflowScenarioHarnessTests.cs`

## Scope

- Add `Templates/Workflows/workflows/office365-email-watch-workflows.yaml`.
- Register the file in `Templates/Workflows/manifest.yaml` and bump seed/version metadata as required by the existing loader.
- Add summary and task templates that branch no-message to successful no-op, write project output before category mutation, and end with compact Scheduler-friendly JSON.
- Add preview simulation coverage for the Office365 executor nodes.

## Dependency Impact

- SB04 reads template `inputParameters` from these templates.
- SB06 proves write-before-mark ordering and idempotent retry behavior using these templates.
- SB08 validates template visibility and fake Graph end-to-end scenarios.

## Validation Depth

- Critical semantic proof that no-message path skips LLM/project/category writes.
- Scenario tests for one-message summary and one-message tasks.
- Loader/manifest/seed tests proving file-backed templates are discoverable.
- Source assertions proving mark-processed occurs after project write.

## Implementation Steps

1. Add summary and task workflow template definitions.
2. Add template manifest entry and seed version update.
3. Add preview simulation entries.
4. Add loader and scenario tests.
5. Record source assertions, transcripts, manifest, and semantic invariants.

## Do Not Do

- Do not mark messages processed before project output succeeds.
- Do not call LLM or mark processed on the no-message path.
- Do not rely on raw JSON-only setup for common parameters.

## Acceptance Checklist

- Summary and task templates load through the manifest without duplicate keys.
- No-message path ends successfully without side effects.
- Summary path writes a Markdown asset and then marks the message processed.
- Task path writes task nodes and then marks the message processed.
- Templates accept Scheduler input JSON keys for email, category, project, node, connection, and lookback.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript for absent templates or wrong ordering.
- Passing loader and scenario transcripts.
- Source assertion and anti-stub audit transcripts.
- Browser/template visibility proof if the Workflows UI surface changes.

## Browser Validation Logging

- Record `/agents/workflows` template visibility proof if template discovery is browser-visible in this repo.

## Progression Gate

- Passed to SB04. Templates are file-backed, registered through the manifest, covered by loader/graph tests, and expose dynamic Scheduler input paths that SB04 can promote into typed parameter metadata.

## Closure Notes

- Implemented `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` with summary and task workflows.
- Registered the file in `repo://Templates/Workflows/manifest.yaml` and bumped template seed/version metadata.
- Added graph assertions for no-message branches, write-before-mark ordering, and dynamic Office365 Scheduler input paths.
- Added Office365 resolver coverage proving runtime settings resolve from Scheduler input JSON.
- Proof is recorded in `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md`.

## Suggested Agent Prompt

Add the Office365 email-watch summary/task templates with no-message branching and write-before-mark semantics, then prove loader and fake scenario behavior before moving to Scheduler schema work.
