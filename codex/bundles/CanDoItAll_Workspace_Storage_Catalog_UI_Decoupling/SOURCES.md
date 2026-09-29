# Source register

Review commit: `56f615a19f5d7eb22042584230ceebcc611bf549`. File URLs are immutable evidence, never execution checkout instructions. Search was used only to locate paths; code claims use explicit-commit reads.

## EV01 — github-branch-metadata

Connected GitHub branch read; observed HEAD 56f615a19f5d7eb22042584230ceebcc611bf549. Rechecked at delivery; not an execution pin.

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling)

## EV02 — github-compare

Connected compare metadata and complete returned changed-file statistics: two commits ahead; archived assignment separated from product implementation. No local diff execution.

[Source](https://github.com/fyziktom/CanDoItAll/compare/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf...56f615a19f5d7eb22042584230ceebcc611bf549)

## AP01 — docs/architecture/workspace-api-access-ui-boundary.md

Lines 1–350 (whole file): implementer architecture, test and measurement record; raw local artifacts were not available.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/docs/architecture/workspace-api-access-ui-boundary.md)

Observed blob: `5855c8fbefc6519ac3a3353a3ebc8b43f027e3fc`.

## AP02 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiAccessSession.cs

Whole file: root lifetime, child authority, activation, observation and retirement.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiAccessSession.cs)

Observed blob: `723d7a0fcf36e36ae52bdf66447b8da29fe6c8ab`.

## AP03 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiAccountController.cs

Whole file: child lifetime passed to page, direct denial path, exact editor, mutations and observations.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiAccountController.cs)

Observed blob: `6e6ac0ac887654157a8c2485b59917c9edd7cd5a`.

## AP04 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiTokenIssueController.cs

Whole file: one-time disclosure, captured issuance, observation, lifetime hierarchy.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiTokenIssueController.cs)

Observed blob: `c71776e83b75ab44dc3c5df416ff2b2c10fb49de`.

## AP05 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiPageController.cs

Whole file: current denial, using-owned CTS, gated finally, Invalidate and Dispose.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiPageController.cs)

Observed blob: `b11dd522e8e3185c2c6d4191f9c1497cd45710fb`.

## AP06 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiTokenListController.cs

Whole file: nested page lifetime, captured confirmation, writes and observations.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiTokenListController.cs)

Observed blob: `fc9f5690d55256c36372d03aca66d556da5c3ae7`.

## AP07 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiViewLifetime.cs

Whole file: downward-only parent cancellation and retirement.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiViewLifetime.cs)

Observed blob: `a7d9756aeb396c83b4e54776562057c18ac89f22`.

## AP08 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/Presentation/ApiTokenOwner.cs

Whole file: real owner projection, machine-kind validation and search exception propagation.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/Presentation/ApiTokenOwner.cs)

Observed blob: `12d9f4be1b549fe6441cb9184eb5981e96551ffe`.

## AP09 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs

Whole file: capture before access await, access checks, acknowledged/unknown writer boundary.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs)

Observed blob: `1de9ba8e2647d9eb1ab2e3cdf51319338288cc7f`.

## AP10 — tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests/ApiSessionTests.cs

Whole file: current test coverage; activation/old observation denial, not current child-list denial. Not executed here.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests/ApiSessionTests.cs)

Observed blob: `43d097ec6c2de4560270897040202db104a1a7d8`.

## AP11 — tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests/ApiPageTests.cs

Whole file: stale read, paging and pre-disposed page coverage. Not executed here.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests/ApiPageTests.cs)

Observed blob: `b483a6cdf0e0d12ea34e4eaa4231b25575b51939`.

## AP12 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/WorkspaceApiAccessHost.razor

Whole file: actual route child lifecycle, caller changes and status callback.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/WorkspaceApiAccessHost.razor)

Observed blob: `7efc900aa66cfa30476521c27ff2c999cb84d966`.

## AP13 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiAccessSurface.razor

Whole file: granted rendering condition, retry control, nested panels and receipt rendering.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/ApiAccessSurface.razor)

Observed blob: `6966d00c5789e6ca8ba1f71327083c466fbe5f84`.

## AP14 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/Presentation/ApiAccountOwner.cs

Whole file: search propagates actual access denial; mutation/metadata projection.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/Presentation/ApiAccountOwner.cs)

Observed blob: `a79cf598b9780962387b63a87c7e229de2181cf3`.

## AP15 — tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests/ApiBoundaryTests.cs

Whole file: evaluated/runtime/public contract checks and Core/Foundation reverse-edge guards. Not run here.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests/ApiBoundaryTests.cs)

Observed blob: `164bae0f9f1b6689108511d3c782ad21291ae28b`.

## AP16 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiUserAdministrationService.cs

Lines 64–240: captured scopes, versioned writes, diagnostic isolation, permission checks. Public declarations above this range not independently reread in this review.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiUserAdministrationService.cs)

Observed blob: `d29aa8450100bedc1bf0f78745f4b47e822813b5`.

## AP17 — src/UI/CanDoItAll.Workspace.ApiAccess.UI/CanDoItAll.Workspace.ApiAccess.UI.csproj

Whole file: direct declared references only; not evaluated by MSBuild here.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/UI/CanDoItAll.Workspace.ApiAccess.UI/CanDoItAll.Workspace.ApiAccess.UI.csproj)

Observed blob: `66e80a309c85922247b8f46982b81c534a6fc954`.

## WS01 — src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceFilesController.cs

Lines 65–140: WSC-R1 target revision acceptance and mutation/review completion.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceFilesController.cs)

Observed blob: `cefa194c18cd7bb4c2fae37cdb63e39a9eda2ae1`.

## WS02 — src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor

Whole file: live Core and deferred Data Sources/Storage/API host composition.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor)

Observed blob: `feef9b41f0dee2ec876c856827e3a471d348ce92`.

## ST01 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StorageSettingsPanel.razor

Lines 1–740, covering whole file in three reads: complete wizard, catalog, recovery invocation, handlers and presentation helpers.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StorageSettingsPanel.razor)

Observed blob: `45b2bf4086b09895955708d6a4f5d9dc36554a5f`.

## ST02 — src/Modules/CanDoItAll.Modules.Workspace/Storage/WorkspaceService.Storage.cs

Lines 1–460, covering whole file in two reads: Save/Delete/Test, credential purpose, routing and templates.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Storage/WorkspaceService.Storage.cs)

Observed blob: `b497125376488e0481c080bc81dbdb2659eb525e`.

## ST03 — src/Modules/CanDoItAll.Modules.Workspace/Storage/WorkspaceStorageModels.cs

Whole file: mixed UI models and Infrastructure enum dependencies; tracked-purpose delegation.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Storage/WorkspaceStorageModels.cs)

Observed blob: `dd90873b6766c3b4f1a75195d8913a5f5e906a36`.

## ST04 — src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs

Lines 1–260 and 330–545 only: owner factories, bootstrap-on-read, SaveCore/Delete/rules. Migration middle/tail not reviewed in full.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs)

Observed blob: `2ba44ff18b24f314d902eaa0791b44aff04a8ed2`.

## ST05 — src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.WorkspaceDefaults.cs

Whole file: independent per-purpose saves and exact disable/selection behavior.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.WorkspaceDefaults.cs)

Observed blob: `7caf49fa9fe5ae602052657ac44bf423fbafbbd6`.

## ST06 — tests/Integration/CanDoItAll.Tests.Integration/StorageCatalogContractPersistenceTests.cs

Whole file: current real owner byte-preservation, routing, health and Workspace editor tests. Four Fact methods read, not discovered or executed here.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/tests/Integration/CanDoItAll.Tests.Integration/StorageCatalogContractPersistenceTests.cs)

Observed blob: `4fa1de1fc2f1fa2e842cd2b047005446a32ba6e9`.

## ST07 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StoragePlacementRecoveryDialog.razor

Lines 1–90 only: recovery and owner-continuation dependencies, original identities/actions. Deferred workflow not audited end-to-end.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StoragePlacementRecoveryDialog.razor)

Observed blob: `7c1e4f55a8128c55c0e6cd510c575812300f189a`.

## ST08 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StorageCatalogSelectionField.razor.cs

Lines 1–220 only: shared catalog picker integration, selected IDs and lazy metadata. Deferred tail/renderer not audited end-to-end.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StorageCatalogSelectionField.razor.cs)

Observed blob: `58582dcfbf8fcdc45e654986388a5b86bba2a659`.

## ST09 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor

Lines 1–80 only: concrete runtime-profile owner and locked/startup selection state. Not a review of full database switching/transfer.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor)

Observed blob: `994e6777a5502be7ef1f08e14a340031cc77bb4f`.

## EV03 — AGENTS.md

Whole file: current canonical guidance and mandatory static gate.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/AGENTS.md)

Observed blob: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.

## EV04 — docs/architecture/ui-component-seams.md

Whole file: canonical placement/state/effect/proof policy; historical example table not a current completion ledger.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/docs/architecture/ui-component-seams.md)

Observed blob: `fe86176f5e24d7d6d3300fefaa687e348cf63670`.

## EV05 — docs/testing.md

Lines 1–105: isolated PostgreSQL 18, owning solutions and focused discovery requirements. Codex must read full current document/CI before changes.

[Source](https://github.com/fyziktom/CanDoItAll/blob/56f615a19f5d7eb22042584230ceebcc611bf549/docs/testing.md)

Observed blob: `d77c7d1b115ecde28d39b430cb93d9d072512852`.

## EV06 — github-workflow-metadata

Connected exact-SHA request returned total_count=0. Does not disprove implementer-local tests.

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=56f615a19f5d7eb22042584230ceebcc611bf549&per_page=10)

## EV07 — CanDoItAll_Workspace_API_Access_UI_Decoupling_Codex_Max.zip

Previous complete archive extracted; executable prompt and validator read; shared foundation copied unchanged and checked byte-for-byte.

Input is the user-provided prior archive.
