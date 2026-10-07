# Source register

Product implementation reviewed: `14f07bffa30ddb124011869e38bc5b264b8bce42`.
Observed branch HEAD: `b55baa3a94ff216353768f0a4ac9a50e89463d08`. Its additional commit archives the preceding bundle only.

All repository-file URLs are pinned to the implementation, not to a moving branch. Coverage is explicit; a blob identity is not a claim of full-file reading. No line-level quotation or linked source substitutes for product execution.

The local package validator verifies this register's structure, not the authenticity or contents of GitHub or Microsoft endpoints.

## EV01

Source: connector-metadata

https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling

Branch read: HEAD b55baa3a94ff216353768f0a4ac9a50e89463d08; message commit previous bundle; parent 14f07bffa30ddb124011869e38bc5b264b8bce42; valid GitHub signature. A separate compare confirmed the only 36 additions are the historical Scheduler bundle. This is branch metadata, not execution evidence. Final branch recheck still returned the same HEAD.

## EV02

Source: connector-metadata

https://api.github.com/repos/fyziktom/CanDoItAll/compare/14f07bffa30ddb124011869e38bc5b264b8bce42...b55baa3a94ff216353768f0a4ac9a50e89463d08

Connector compare completed: one archival commit; only codex/bundles/CanDoItAll_SchedulerPlanner_UI_Decoupling/** added, no product/test/build changes.

## EV03

Source: connector-metadata

https://api.github.com/repos/fyziktom/CanDoItAll/compare/0e176a3b99270cdc9a86a57d6276d05979d7352e...14f07bffa30ddb124011869e38bc5b264b8bce42

Connector compare completed: one implementation commit; full changed-path/stat inventory inspected. Not a claim that every changed file was read in full.

## EV04

Source: connector-metadata

https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=14f07bffa30ddb124011869e38bc5b264b8bce42&per_page=10

GitHub Actions run lookup returned total_count 0. Does not disprove local runs and does not establish product correctness.

## EV05

Source: docs/architecture/scheduler-ui-boundary.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/docs/architecture/scheduler-ui-boundary.md

Full maintained implementation receipt read. Reports 245 selected passing cases, graphs and measurements. Raw TRX, screenshots and measurement artifacts were not inspected or rerun by this reviewer.

Observed Git blob: `73fc36db02cf5c7751b46c55891443bcde9e19a5`.

## EV06

Source: AGENTS.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/AGENTS.md

Full repository entry instructions read.

Observed Git blob: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.

## EV07

Source: .github/copilot-instructions.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/.github/copilot-instructions.md

Full engineering and validation instructions read.

Observed Git blob: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`.

## EV08

Source: docs/testing.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/docs/testing.md

Lines 1-165 read: isolated PostgreSQL, solution entry points, discovery, bUnit lifecycle and Scheduler lanes. The executor must read the complete current guide, including widening/static-gate sections.

Observed Git blob: `b9d69fb2b52fd766a01f387f6989683812515995`.

## EV09

Source: docs/architecture/ui-component-seams.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/docs/architecture/ui-component-seams.md

Lines 1-230 returned and read: canonical placement, view/record alternatives, state/effect lifetime, owner outcomes, assets, proof and anti-patterns.

Observed Git blob: `da998e19c2206ef4396e04b20aeed6ac4634bbaa`.

## EV10

Source: src/Modules/README.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/README.md

Full module overview read; used as inventory, not proof of complete decoupling or a global effort estimate.

Observed Git blob: `877b82a3a491cf502a65898d19ff5db90249d2cc`.

## PL01

Source: src/UI/CanDoItAll.Plugins.UI/PluginsWorkspaceSurface.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/UI/CanDoItAll.Plugins.UI/PluginsWorkspaceSurface.razor

Lines 90-144 read: list shell independent from missing selected plugin, explicit detail fallback.

Observed Git blob: `75111b1e2125315003a120156f423654f8c917a7`.

## PL02

Source: src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceReads.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Plugins.Presentation/PluginWorkspaceReads.cs

Full file read: catalog disappearance signals selection retirement while preserving target identity.

Observed Git blob: `f64a336f4167461461a71f0596c66804a6d1a8cb`.

## PL03

Source: src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs

Lines 440-565 read: extraction replacement and independently caught cleanup failure preserve primary exception and stage.

Observed Git blob: `45b8262527bf028346e71fdd5706a2b4c0a82ffb`.

## SC01

Source: src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/SchedulerWorkspace.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/SchedulerWorkspace.cs

Full file read: read lanes, draft retention, editor/overlay origins, recovery delegation and disposal.

Observed Git blob: `1815d605b3b34f2ff6c19bea9f14623c4a0d88f5`.

## SC02

Source: src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/SchedulerMutations.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/SchedulerMutations.cs

Full file read: immutable save capture, per-plan admission, receipt merging, both unknown review methods. Source basis of SC-R1 and Save precondition in SC-R2.

Observed Git blob: `2c28227d07e4bafa24e46835b3567890dac13a30`.

## SC03

Source: src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/SchedulerInputSession.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/SchedulerInputSession.cs

Full file read: raw JSON, typed inputs, dependent-value invalidation and option read budgets. Source basis of SC-R2.

Observed Git blob: `e674e429e471463f91b2350fefcc93bbd667dca9`.

## SC04

Source: src/UI/CanDoItAll.SchedulerPlanner.UI/SchedulerWorkspaceContract.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/UI/CanDoItAll.SchedulerPlanner.UI/SchedulerWorkspaceContract.cs

Full file read: immutable editable values, per-field revisions, receipt and lock definitions, public workspace methods.

Observed Git blob: `702977e83245d4ea5d8c78f0340cbc6f6be5e1a2`.

## SC05

Source: src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerWorkspaceSession.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerWorkspaceSession.cs

Full file read: production port mapping, profile guard, authority capture and committed/unknown outcomes.

Observed Git blob: `a370b48c7ced8f8fa3e11f230c87a152704153e8`.

## SC06

Source: src/UI/CanDoItAll.SchedulerPlanner.UI/SchedulerCalendar.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/UI/CanDoItAll.SchedulerPlanner.UI/SchedulerCalendar.razor

Full file read: actual Canvas, unique registration, exact event selection, pointer/view fencing and async disposal. Browser behavior not rerun.

Observed Git blob: `7aba4683a294f3d347ff3979078aeaa2b7aeeb9e`.

## SC07

Source: src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor

Full routed host read: per-page workspace, managed Agent slot/context, subscriptions and retirement.

Observed Git blob: `056713ef6fcf91005e6fc53ed2079b1ea3546450`.

## SC08

Source: src/Sandboxes/CanDoItAll.SchedulerPlanner.UiSandbox/SchedulerScenarioStore.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Sandboxes/CanDoItAll.SchedulerPlanner.UiSandbox/SchedulerScenarioStore.cs

Full scenario owner read: controlled waits, original-store writes, optional project/node dependency and validation override useful for regressions.

Observed Git blob: `97d9b4df8202b26a30612804bf6de43d02921272`.

## SC09

Source: src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs

Lines 220-390 read: durable save/transaction facts, projection/reload/log follow-ups, enable/delete paths and workflow target lookup.

Observed Git blob: `71585c76b6c9876ed5a83994834b6965bcdcd7b0`.

## SC10

Source: src/UI/CanDoItAll.SchedulerPlanner.UI/SchedulerDraftReceipt.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/UI/CanDoItAll.SchedulerPlanner.UI/SchedulerDraftReceipt.razor

Full component read: exact-plan review input and click handler. No separate review-in-flight admission is exposed here.

Observed Git blob: `0750fe037355976e3ba806abcfe81dc78879219d`.

## SC11

Source: tests/Components/CanDoItAll.SchedulerPlanner.UI.Tests/SchedulerWorkspaceTests.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/tests/Components/CanDoItAll.SchedulerPlanner.UI.Tests/SchedulerWorkspaceTests.cs

Returned initial test block read through the missing-version/deletion test; large response was truncated. Includes sequential unknown review and held-save/input tests. No assertion that the entire test inventory was exhaustively inspected.

## ME01

Source: src/Modules/CanDoItAll.Modules.Memory/Pages/MemoryProvidersPage.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Pages/MemoryProvidersPage.razor

Full route read: /memory, seven tabs, controllers, summary and keyed tab tree.

Observed Git blob: `5a2b941f6269fdeb70c6b50e75f0c1356b651257`.

## ME02

Source: src/Modules/CanDoItAll.Modules.Memory/Pages/MemoryProvidersPageController.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Pages/MemoryProvidersPageController.cs

Full controller read: mutable requests, unconditional editor replacement, post-action selection/results/tab updates, unowned busy flags and no disposal fence.

Observed Git blob: `8c32af79a1afe7590f7ba6fc756389f550e9b5a4`.

## ME03

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderManagementUiService.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderManagementUiService.cs

Full in-process facade read. Delegates to separate profile, query, ledger, ingestion and snapshot owners.

Observed Git blob: `acbcd6294935e250b3e3efc740377adec2371ade`.

## ME04

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderSnapshotReader.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderSnapshotReader.cs

Full reader read: exact ordinal match or first-profile fallback, provider-specific ledgers and UI projection. Underlying store limit implementations not inspected.

Observed Git blob: `eccb522de8565a854c73bbfaa2ae36d4e2293bb1`.

## ME05

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderProfileUiService.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderProfileUiService.cs

Full file read: profile mapping/upsert and two sequential explicit demo upserts; no implied atomicity.

Observed Git blob: `550170ea9a65225e51cc6990589aaa7f929bc5ab`.

## ME06

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderLedgerActionUiService.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderLedgerActionUiService.cs

Full file read: explicit status operation, cancellation rejection, feedback and event routes. Capability decisions must be combined with ME11; dormant paths are not currently enabled features.

Observed Git blob: `18b18fa670f07e9b6ecf1aa32b57b04ca5faa9f8`.

## ME07

Source: src/Modules/CanDoItAll.Modules.Memory/Components/MemoryProviderUiSurfaceHost.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Components/MemoryProviderUiSurfaceHost.razor

Full dynamic host read: RCL component/parameter binding, iframe attributes, external-link policy and denied state.

Observed Git blob: `e70e908cac5392fea9fd97aa253be1e7f7de4594`.

## ME08

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderUiSurfaceProjector.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderUiSurfaceProjector.cs

Full projector read: enabled/healthy/capability gates, registered component key, URL scheme and userinfo/query/fragment restrictions.

Observed Git blob: `4954b8b316066f318cadf18b69984f63249c475f`.

## ME09

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderExecutableActionGuard.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderExecutableActionGuard.cs

Full guard read: current exact provider and operation checks; cancellation explicitly unsupported.

Observed Git blob: `7d37c03f7541bd0049740127704ce0e27436eec7`.

## ME10

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderProfileEditorModel.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderProfileEditorModel.cs

Full model read: HTTP/MCP default constants, nested mutable transport models, preserved manifest/extension/credential metadata.

Observed Git blob: `86db676fb36854f53fc8053497ac9fa0ab3bfeea`.

## ME11

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderCapabilityPolicy.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderCapabilityPolicy.cs

Full shipped-driver capability policy read. Only MCP advertises async query/status; no current driver supports ingestion, feedback, push acknowledgement or cancellation.

Observed Git blob: `0adb13a531bd3f73da038801308e6a62a722e4c4`.

## ME12

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderQueryUiService.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderQueryUiService.cs

Full query service read: guard await precedes reading mutable query/provenance; result retains accepted operation, feedback handle and driver-dispatch evidence.

Observed Git blob: `8dab3056cbce60824103eaf4e886b550ac484084`.

## ME13

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderIngestionUiService.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderIngestionUiService.cs

Full dormant ingestion path read: enqueue identity precedes ledger follow-up. The current shipped capability guard refuses this action; do not present it as a demonstrated accessible production loss.

Observed Git blob: `f14953a02b19d89f9da25ea041471ee446895ce3`.

## ME14

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderOperationUiModels.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderOperationUiModels.cs

Full UI records read: Application-owned statuses and Abstractions values; outcome/provenance must survive any light projection.

Observed Git blob: `2c987702ac430938a7d7a070b222fa6a316b6ba7`.

## ME15

Source: src/Modules/CanDoItAll.Modules.Memory/CanDoItAll.Modules.Memory.csproj

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/CanDoItAll.Modules.Memory.csproj

Full declared graph read: BaseLib/Common, SharedKernel, Memory.Abstractions/Application/Http/Mcp. No direct EF claim; evaluate transitive closure locally.

Observed Git blob: `4643b4aea6b3d6f70b36f1ce61ee3c0bc99c2cc1`.

## ME16

Source: src/Modules/CanDoItAll.Modules.Memory/Components/MemoryProviderProfileEditor.razor

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Components/MemoryProviderProfileEditor.razor

Full editor read: identifier, nested transports, capability selectors, credential warning, tags and save.

Observed Git blob: `c8750a1267ed21f0879b5ee0ed66ed30998a80c9`.

## ME17

Source: src/Modules/CanDoItAll.Modules.Memory/MemoryModuleServiceCollectionExtensions.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/MemoryModuleServiceCollectionExtensions.cs

Full DI read: current controller is transient; owner services scoped; built-in mock RCL and registered key.

Observed Git blob: `edcefc7968e3ee3364f7234d35594a70600bf0c7`.

## ME18

Source: src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderUiSurfaceModels.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderUiSurfaceModels.cs

Full surface models and registry read: explicit component type registrations, ordinal keys, duplicate last-registration policy. No arbitrary manifest assembly loading.

Observed Git blob: `1986051b4f7e8c290b5704009259209da736c108`.

## ME19

Source: src/Memory/CanDoItAll.Memory.Application/CanDoItAll.Memory.Application.csproj

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Memory/CanDoItAll.Memory.Application/CanDoItAll.Memory.Application.csproj

Full project read: abstract references and DI/logging packages. Absence of EF does not make the application implementation a rendering contract; avoid judging cost by name alone.

Observed Git blob: `d4fab3b3a409977b8bd793e0eb291c79315d00e2`.

## ME20

Source: tests/Components/CanDoItAll.Tests.Components/MemoryProviderProfileEditorRoundTripTests.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/tests/Components/CanDoItAll.Tests.Components/MemoryProviderProfileEditorRoundTripTests.cs

Full three source tests read: HTTP/NativeRemote/MCP lossless round trips, vendor JSON and environment credentials. Not run here.

Observed Git blob: `4fdefbeb65859324eec5e1a109a4a0109286fdb1`.

## ME21

Source: tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs

Lines 1-210 read: loading/zero/selection/demo/guard/navigation, absence of native dependencies and beginning of responsibility checkpoint. Remaining tests/consumers require local discovery.

Observed Git blob: `dfd630ef917f8516b3bf403e85cb9037b6a921e0`.

## ME22

Source: src/Modules/CanDoItAll.Modules.Workspace/README.md

https://github.com/fyziktom/CanDoItAll/blob/14f07bffa30ddb124011869e38bc5b264b8bce42/src/Modules/CanDoItAll.Modules.Workspace/README.md

Full comparison-candidate overview only: profiles/outbox and cross-owner obligations. Memory selection is a bounded architectural recommendation, not measured ranking of all remaining modules.

Observed Git blob: `299707463ecf3577a3d0d92585d4d7e4ea226673`.

## EV11

Source: official-documentation

https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0

Official Microsoft guidance opened: Blazor component methods are reentrant at incomplete awaits, including disposal. Background rationale, not proof of a repository execution.

## EV12

Source: official-documentation

https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch

Official .NET watch reference retrieved: project/watch behavior must be evaluated and measured, not inferred from moved Razor files.

## EV13

Source: official-documentation

https://learn.microsoft.com/en-us/aspnet/core/blazor/components/css-isolation?view=aspnetcore-10.0

Official CSS isolation guidance retrieved: scopes belong to their rendered DOM; descendant styling needs correct scope ownership.
