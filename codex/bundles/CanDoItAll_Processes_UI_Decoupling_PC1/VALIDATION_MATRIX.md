# PC1 test selection and closure

The 42 acceptance groups in [the plan](acceptance-plan.json) are feature/proof obligations, not fixed test counts. Current source and discovery decide the concrete selections. Follow the shared [validation protocol](../UI_Decoupling_Shared_Bundle/VALIDATION.md) and current `docs/testing.md`.

## Source-verified initial entry points

| Project | Candidate filter / source | Required proof |
| --- | --- | --- |
| `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | `FullyQualifiedName~CanDoItAll.Tests.Components.Processes.ProcessWorkspaceShellTests` | Workspace **and Live** rows, context/lifetime and the launch partial T01/T02 |
| Same project | `FullyQualifiedName~CanDoItAll.Tests.Components.Processes.ProcessRunFilesDialogTests` | Current scope refresh/forbidden behavior plus R2/R3 |
| Same project | `FullyQualifiedName~CanDoItAll.Tests.Components.Processes.ProcessRunCancellationActionTests` | Duplicate/retirement/refusal/known commit/unknown outcome T03 |
| `tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` | `FullyQualifiedName~CanDoItAll.Tests.Playwright.Smoke.ProcessShellSmokeTests` | Actual source canvas and global/project/live journey T05; selectors/readiness must be reconciled |
| New light Processes UI test project | Derive exact current namespace after implementation | Real complete renderers, session/draft/origin, scenario and dependency negative controls |
| Actual owning Unit/Integration projects | Derive through current CodeAnalytics and source | Native launch/preparation/operator, project admission, projection/canvas/template, file scope/session/action, chat/context/voice, API serialization/streaming and Workbench consumers |

The cancellation source inspected in full contains nine data-expanded cases. The two launch methods inspected in the partial contain three data-expanded cases, **not the entire ProcessWorkspaceShellTests class**. These are source observations, not observed runner discovery. Recompute current counts before each new/changed filter. Do not invent filters named after a source filename when the methods belong to a different partial class.

## Inner loop example

After recording the expected count from current source and building all changed production projects, adapt this confirmed source entry to the current environment:

```powershell
$configuration = 'ProcUiProof'
$project = './tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'
$filter = 'FullyQualifiedName~CanDoItAll.Tests.Components.Processes.ProcessRunCancellationActionTests'

dotnet test $project --configuration $configuration --list-tests --filter $filter /m:1
# Compare actual discovery with the source-derived expectation before execution.
dotnet test $project --configuration $configuration --no-build --no-restore --filter $filter /m:1
```

List-tests must refresh the owning test assembly for the current sources. Record exact configuration, filter and discovered/executed/theory counts. Use a bounded union for the final native Process family rather than repeatedly running an unfiltered project. New regression tests are part of the required union even before the index knows them.

Build each changed production project directly: native Processes, the new UI and any new contract/presentation project, affected Web/composition and actual changed consumers. Build the sandbox and light test assembly independently. Preserve product-solution/test-solution separation. Add a new light test project to current Components/Stable solutions and **every actual CI component selection** after inspecting the current workflow; do not assume one list is enough.

## Mandatory native boundary coverage

Find exact current owning tests for retained ProcessLaunchIntent/prepared launch, continuation/link delivery, ProcessRuntimeOperatorApplicationService, project deletion/recreation admission, ProcessWorkspaceProjectionClient/canvas queries, template import, ProcessRunFileScopeProvider/Coordinator and content/action lifetimes. Keep existing relevant API payload/authorization/profile-fenced event-stream consumers when types or orchestration change. A native owner test must exercise the real owner with controlled dependency boundaries, not replace it with a pre-canned success receipt.

Keep targeted Workbench execution and workflow/process launch/staffing/approval tests when the shared projection/type/launch context changes. Deterministic provider fixtures may establish application integration; they do not prove a real external model or hardware device. The existing observation-only launch component fixture is proof of restoration logic, not an end-to-end fresh launch.

## Browser, database and isolation

Run the independent and production journeys in UI_JOURNEYS.md. Use 1920×1080, device scale 1 for new desktop proof. Preserve existing useful 2048×1200/1440×900 desktop cases; do not start a mobile redesign or delete an existing desktop scenario to enforce a new single viewport.

Use explicitly isolated PostgreSQL 18 through `CANDOITALL_TESTS_POSTGRES_CONNECTION`, with the current lease/cleanup mechanism. Keep credentials private. Leave ordinary external Playwright base URLs unset; in particular do not point fixtures at the user's application on 5032. Set `CANDOITALL_TEST_CONFIGURATION` consistently with owned child builds. Use fresh uniquely marked storage, ports and processes. Linux SDK containers requiring descendant cleanup run with `--init` under current testing guidance.

## Final frozen gate

PC1 is expected to alter render contracts/composition/test registration and assets. After all focused lanes stabilize, name the documented invalidation trigger and execute **one final broad Stable selection using the current canonical command/traits**. It is not required after each stage and is not an automatic rerun of old live/Docker/demo campaigns. If final changes invalidate only a bounded topic, rerun that topic and document why earlier broader evidence remains applicable; shared contract/build changes normally invalidate more.

Run required source/publish/asset/guard checks and portability/secret/doc gates on the final candidate. A failing gate remains a failure; a missing native/browser prerequisite remains blocked. Attribute quarantined/skipped cases. Current live-test wrappers can return runner PASS without reaching a provider; inspect actual manifest execution and request counts before any live claim.

Record current field/action coverage, exact candidate/source pair, filters/counts/oracles, native IDs/content, browser traces/screenshots, assets and measurement samples. No supplied template has a pre-passed product row. Use `templates/closure.md` for final statuses.
