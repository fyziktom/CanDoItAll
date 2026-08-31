# Workflow preview failure attribution follow-up

Authorized by root after the first complete Integration run: one quiet, no-build execution of the exact two existing failing Components methods against the unchanged frozen candidate. Expected count2. No production or test source edits, no timeout increase, and no broad-suite rerun.

```text
FullyQualifiedName=CanDoItAll.Tests.Components.AgentFramework.WorkflowsPageTests.Workflow_canvas_preview_prompts_for_project_context_and_can_skip_project_writes|FullyQualifiedName=CanDoItAll.Tests.Components.AgentFramework.WorkflowsPageTests.Workflow_canvas_preview_selects_running_node_from_progress
```

Use the hardened runner with Suite Components, distinct Phase `components-preview-attribution`, NoBuild, the exact filter above and ExpectedCount2. It retains the same checked52049 PostgreSQL bootstrap and disabled live/interactive flags. First discover exactly these two cases without a build, then execute once only after full Integration finishes. Preserve the original30-second bUnit waits and all first-run TRX results.

## First-run observations

- Project-context method:32.1288seconds, failed at WorkflowsPageTests.cs:1215 waiting for `workflow-canvas-preview-input-dialog`;4checks,19renders. Failure time2026-08-31T15:07:18.7987213Z.
- Progress-selection method:32.7789seconds, failed at WorkflowsPageTests.cs:1382/1385 because `workflow-canvas-test-result` was absent;1check,34renders. Failure time2026-08-31T15:06:03.6719115Z. The preceding HadProgressObserver assertion was not the reported last failing assertion.
- Both methods install controlled IWorkflowTestRunner fakes. Neither requires a real provider completion. The project-context method also installs a controlled project gateway.
- Components ran from14:57:43Z to its recorded terminal command timestamp. Root's native build completed14:57:17Z and Docker build completed14:59:14.3714465Z. Therefore neither app build was active during the15:05–15:07 failing-test intervals. An initial CodeAnalytics request remained outstanding until approximately15:07, but that is contextual timing, not evidence that it caused the failures. A prior informal attribution to an active app-build window was incorrect and is explicitly withdrawn.

A passing quiet follow-up establishes non-reproduction under that controlled run; it does not erase the first failures or prove the cause was machine load. A repeated failure requires exact UI/fake-contract investigation or a separately verified old-binary comparison. No baseline binary equivalence or pre-existing-failure classification is assumed for these two component cases.