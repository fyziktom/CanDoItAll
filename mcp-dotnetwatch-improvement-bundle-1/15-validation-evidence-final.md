# 15. Validation Evidence Final

Date:

- 2026-03-24 / 2026-03-25

Scope:

- final implementation check against `README.md`, `07-prompts.md`, and `08-validation-criteria.md`
- bounded validation after prior interrupted long-running build/test attempt

## Repository state

- `git status --short` returned clean output before closeout
- no additional implementation edits were required during this pass; the remaining gap was validation closure

## Automated test evidence

### Unit tests

- command:
  `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj --configuration Debug --no-build --logger "console;verbosity=minimal"`
- result:
  31 passed, 0 failed, 0 skipped
- duration:
  928 ms

### Integration tests

- `BootstrapValidationTests`
  - 4 passed, 0 failed
  - duration: 2 m
- `BundleImprovementIntegrationTests`
  - 5 passed, 0 failed
  - duration: 3 m 29 s
- `McpServerIntegrationTests`
  - 10 passed, 0 failed
  - duration: 8 m 37 s
- `ValidationMatrixTests`
  - 10 passed, 0 failed
  - duration: 8 m 43 s

Integration total:

- 29 passed, 0 failed, 0 skipped

## Live tool and transcript evidence

### 1. Wrapper/bootstrap evidence

Bootstrap log path:

- `C:\repositories\CanDoItAll\.mcp-state\logs\mcp-dotnetwatch-bootstrap.log`

Representative excerpt:

- `2026-03-25T02:12:45.5557305+00:00 wrapper start | repo=C:\repositories\CanDoItAll | project=C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj | settings=C:\repositories\CanDoItAll\CanDoItAll.Mcp.DotNetWatch.settings.json | shadow=C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow | signature=632240a8303be08924ede011bd33f32438741d1c9d8a68ffb1f3eeaed136cf85`
- `2026-03-25T02:12:45.5890996+00:00 shadow check | manifest current | dll=C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow\builds\632240a8303be08924ede011bd33f32438741d1c9d8a68ffb1f3eeaed136cf85\bin\CanDoItAll.Mcp.DotNetWatch\debug\CanDoItAll.Mcp.DotNetWatch.dll`
- `2026-03-25T02:12:46.1083845+00:00 launch shadow host | dll=C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow\builds\632240a8303be08924ede011bd33f32438741d1c9d8a68ffb1f3eeaed136cf85\bin\CanDoItAll.Mcp.DotNetWatch\debug\CanDoItAll.Mcp.DotNetWatch.dll`

### 2. Bridge reliability and repair

Tool:

- `candoitall_workspace_info`

Observed at:

- `2026-03-25T02:21:45.2902971+00:00`

Evidence:

- `ok=true`
- `bridge.health=Repaired`
- `bridge.backendId=backend_7a959b3e2acb49c1bd67c7ac16a6cb0e`
- `bridge.currentShadowDllPath=C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow\builds\632240a8303be08924ede011bd33f32438741d1c9d8a68ffb1f3eeaed136cf85\bin\CanDoItAll.Mcp.DotNetWatch\debug\CanDoItAll.Mcp.DotNetWatch.dll`
- `workflowGuidance.mode=watch-small-step`

This satisfies the wrapper launch + `workspace_info` evidence and shows automatic repair/rebind was observable, not silent.

### 3. Healthy watch lane and compact workflow guidance

Tools:

- `candoitall_app_start`
- `candoitall_app_status`

Observed session:

- `sessionId=app_876164110fa44f59a71d46b7a566f3c1`
- `logicalAppId=bundle-validation-web`
- `laneKind=SourceWatch`
- `revision=bundle-validation-web:1`

Evidence:

- start reached `state=Healthy`
- status reported `revision.isConfirmed=true`
- status emitted compact guidance:
  - `mode=watch-small-step`
  - `next=edit-1-nearby-file`
  - `verify=wait RevisionConfirmed then browser-check`
  - `guard=stay-nearby`

### 4. Guidance suppression on raw logs

Tool:

- `candoitall_app_logs`

Observed session:

- `sessionId=app_876164110fa44f59a71d46b7a566f3c1`

Evidence:

- payload returned log entries and `filterSummary`
- payload did not include any `workflowGuidance` object
- returned entries were raw startup/build/watch lines only

This satisfies the non-pollution requirement for log/event style responses.

### 5. Atomic candidate prepare and commit

Tool:

- `candoitall_app_update_atomic`

Observed at:

- `2026-03-25T02:23:58.1269377+00:00`

Evidence:

- `transactionId=txn_23785120d08841a39223673c0eb3ba04`
- `candidateSessionId=app_712eda45c1cb4a0aa429aa1b99ee277b`
- `candidateSlotId=slot-a`
- `state=Committed`
- `candidateRevision=bundle-validation-web:slot-a:0f27d100df39da79c8edd36ecc000726332344cbf33bfcab17b5f1e3dced43b3`
- `observedUrls=http://127.0.0.1:5504`
- `rollbackAvailable=true`

Follow-up `candoitall_app_status` on `app_712eda45c1cb4a0aa429aa1b99ee277b` showed:

- `laneKind=PublishedActive`
- `slotId=slot-a`
- `entryPath=C:\repositories\CanDoItAll\.mcp-state\runtime-slots\bundle-validation-web\slot-a\payload\CanDoItAll.Web.dll`

This demonstrates isolated slot-based publish/activation rather than a single hot publish folder.

### 6. Rollback safety

Tool:

- `candoitall_app_rollback`

Observed at:

- `2026-03-25T02:24:10.9298769+00:00`

Evidence:

- `transactionId=txn_23785120d08841a39223673c0eb3ba04`
- `restoredSessionId=app_876164110fa44f59a71d46b7a566f3c1`
- `restoredRevision=bundle-validation-web:1`
- `previousRevision=bundle-validation-web:slot-a:0f27d100df39da79c8edd36ecc000726332344cbf33bfcab17b5f1e3dced43b3`

This proves previous and restored revisions are both visible through structured payloads.

### 7. Failed candidate preserves authoritative runtime

Tool:

- `candoitall_app_update_atomic`

Failure observed at:

- `2026-03-25T02:26:00.2592199+00:00`

Evidence:

- `error.code=CandidatePrepareFailed`
- `error.details.transactionId=txn_9b2ea0ef151b4e00b8ba81bbf6834481`
- `error.details.targetSlotId=slot-a`
- failure cause was intentional invalid framework targeting (`net999.0`) during `dotnet publish`

Post-failure `candoitall_app_status` showed the active runtime remained:

- `sessionId=app_6558695574f8460f9e2c69e543ffc91c`
- `laneKind=PublishedActive`
- `slotId=slot-b`
- `revision=bundle-validation-web:slot-b:84ce205f8c4a84db3cd7828d406705342bc62b8e69cdb5afc476b2d4d44de5db`

This satisfies the failed-candidate preservation rule.

### 8. Self-host validation isolation

Live watch session:

- `sessionId=app_51ef9bff419146f7883daed5a6b9d521`
- `logicalAppId=bundle-validation-web`

While that session remained healthy, backend-managed test execution completed:

- tool: `candoitall_tests_run`
- `operationId=op_4ba3baab6a47483c9409634ec9a9197f`
- target:
  `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj`
- `state=Completed`
- `exitCode=0`
- `testSummary.total=31`
- `testSummary.passed=31`
- `appPreemption.policy=ContinueIfSafe`
- `appPreemption.stoppedSessionIds=[]`

`candoitall_operation_status` listed isolated artifact outputs under:

- `C:\repositories\CanDoItAll\.mcp-state\artifacts\op_4ba3baab6a47483c9409634ec9a9197f\...`

Parallel `candoitall_app_status` confirmed the live watch session stayed:

- `state=Healthy`
- `laneKind=SourceWatch`
- `revision=bundle-validation-web:1`

This satisfies self-host validation through isolated artifacts without stopping the live backend-managed app.

## Residual warnings

- wrapper/bootstrap build and some local project builds emitted existing NU1510 warnings for:
  - `Microsoft.Extensions.Hosting`
  - `Microsoft.Extensions.Http`
- no blocking build or test failures remained

## Closeout judgment

Bundle implementation is complete and validated against the current bundle requirements.
The previously unfinished item after the interrupted run was validation closure, not missing code.
No 38-minute stalled build path reproduced after reboot; bounded class-by-class runs and isolated backend operations completed within expected ranges.
