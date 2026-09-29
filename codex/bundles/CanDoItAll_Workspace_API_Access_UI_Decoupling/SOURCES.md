# Source register

Review commit: `186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf`. Pinned links identify what was inspected, not a checkout instruction. Searches on the default branch were used only to locate files; claims below use the explicitly fetched commit. Tool output and code inspection are not product execution evidence.

## EV01 — git-metadata

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling)

Live branch read at entry and rechecked before sealing on 2026-09-29; both returned 186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf. GitHub reports a verified signature. Commit title identifies Workspace Settings Core; provenance only, not an execution pin.

## EV02 — git-metadata

[Source](https://github.com/fyziktom/CanDoItAll/compare/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d...186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf)

Connector comparison: ahead by two commits, including archived previous handoff and the Core implementation. Changed-file statistics were used as inventory, not execution evidence.

## EV03 — docs/architecture/workspace-settings-core-ui-boundary.md

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/docs/architecture/workspace-settings-core-ui-boundary.md)

Read the returned implementation/architecture and focused-test passages from requested lines 1-250 (response truncated), and lines 250-477 through closure. Reported raw artifacts, graph counts, timing and test results were not independently reproduced. No claim of complete inspection of the truncated middle measurement passage.

## EV04 — git-metadata

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf&per_page=10)

Unfiltered-by-event Actions query for this SHA returned total_count 0. This does not disprove local tests.

## AP01 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/WorkspaceApiAccessHost.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/WorkspaceApiAccessHost.razor)

Entire status host: lazy status, unavailable callback, enabled/auth/key conditions, token and user production composition.

## AP02 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor)

Entire issuance form, scopes, one-time result and nested dialogs; mutable request before permission await.

## AP03 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiUserAdministrationPanel.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiUserAdministrationPanel.razor)

Entire user search/page/create/edit/reset/delete host, configured-admin name, password state and generation handling.

## AP04 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokensDialog.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokensDialog.razor)

Entire paged registry dialog, Enter search, shared busy flag and nested confirmation; no independent request lifetime fencing.

## AP05 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiScopePickerDialog.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiScopePickerDialog.razor)

Entire actual scope picker: user/machine filtering, clear/select-all, explicit confirm/cancel.

## AP06 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs)

Entire service: access checked on each action; IssueAsync awaits access before reading mutable issue request.

## AP07 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiUserAdministrationService.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiUserAdministrationService.cs)

Entire safe account wire DTOs and owner methods: scopes, expected versions, authentication revisions, durable store followed by logging.

## AP08 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs)

Read lines 1-260 and 259-end: mixed sensitive deployment options, safe status/request/result, issuer validation, JWT construction and registry registration.

## AP09 — src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs)

Entire canonical scope catalog/parser/validation and managed-credential claim constants. Product names are vocabulary, not module implementation dependencies.

## AP10 — src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs)

Entire real authority adapter: validated configured-administrator session or trusted local interactive operator; no broad-scope privilege inference.

## AP11 — src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiUserStore.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiUserStore.cs)

Entire instance-local private account store: lock, uniqueness/version checks, bounded catalog and durable writer. Not PostgreSQL persistence.

## AP12 — src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs)

Entire instance-local registry: one private file per registration, CreateNew, kind-filtered paging, revoke/delete and validation. No bearer plaintext storage.

## AP13 — tests/Components/CanDoItAll.Tests.Components/ApiTokenAdministrationTests.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/tests/Components/CanDoItAll.Tests.Components/ApiTokenAdministrationTests.cs)

Entire five-Fact source: lazy list, confirm/cancel, actual scopes, denied owner actions and ordinary account. Source count only, not discovery/execution.

## AP14 — tests/Components/CanDoItAll.Tests.Components/ApiUserAdministrationPanelTests.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/tests/Components/CanDoItAll.Tests.Components/ApiUserAdministrationPanelTests.cs)

Entire one-Fact source: held write duplicate admission and committed create with failed list followed by read-only retry. Preserve this regression.

## AP15 — src/App/CanDoItAll.Web/Api/ApiAccessEndpoints.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/App/CanDoItAll.Web/Api/ApiAccessEndpoints.cs)

Entire access endpoint/filter source: feature gates, login/session/admin separation, strict request/status semantics, no-store, versioned mutations and default machine filter.

## AP16 — src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ApiTokenRegistry.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ApiTokenRegistry.cs)

Entire mixed registry contract file: safe ApiTokenSummary versus private ApiTokenRecord bindings, status precedence, numeric credential kinds and Machine default.

## RS01 — src/Modules/CanDoItAll.Modules.Resources.Presentation/ResourceRegistryController.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Resources.Presentation/ResourceRegistryController.cs)

Lines 1-250 and 260-300 read: separate EditorAccess/catalogAccess, exact-selection retry and mutation admission. Other mutation tail not re-read in this iteration.

## WS01 — src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceDefaultsController.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceDefaultsController.cs)

Entire controller: first acquisition, independent provider reads, single-flight refresh, save reconciliation and metadata observation.

## WS02 — src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceSecretsController.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceSecretsController.cs)

Entire controller: explicit editor acquisition, command copies, sensitive-reference retirement, receipts and metadata-only review.

## WS03 — src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceFilesController.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceFilesController.cs)

Entire controller: captured file command, revision-wide result adoption, selected deletion target, refresh and receipts; WSC-R1 source.

## WS04 — src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceHistoryController.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceHistoryController.cs)

Entire controller: explicit policy load, expected version, preview validation, authority retirement and unknown-outcome handling.

## WS05 — src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs)

Entire route code-behind: eight tokens, Providers redirect, database/caller retirement and lazy file activation.

## WS06 — src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor)

Entire route markup: active deferred Data Sources/Storage/API hosts supplied by production, separate history host.

## WS07 — src/UI/CanDoItAll.Workspace.UI/SettingsOperations.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/UI/CanDoItAll.Workspace.UI/SettingsOperations.cs)

Entire safe receipt and bounded ledger contract. No secret command stored in the ledger.

## WS08 — src/Modules/CanDoItAll.Modules.Workspace/Presentation/WorkspaceSecretsOwner.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Presentation/WorkspaceSecretsOwner.cs)

Entire production adapter: original profile generation, editor-only save semantics and known owner stages.

## WS09 — src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs)

Lines 230-450: captured Save/SaveEditor, staged vault write, unknown metadata acknowledgement, committed cleanup/Activity and deletion. Not a full audit of Security.

## WS10 — src/UI/CanDoItAll.Workspace.UI/WorkspaceSecretsSurface.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/UI/CanDoItAll.Workspace.UI/WorkspaceSecretsSurface.razor)

Entire renderer: actual SecretField, immediate inputs, draft-keyed editor, safe receipt list and exact review.

## WS11 — src/Modules/CanDoItAll.Modules.Workspace.Contracts/WorkspaceOwnerContracts.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace.Contracts/WorkspaceOwnerContracts.cs)

Entire light defaults/secrets/files ports and safe write diagnostic contracts.

## WS12 — src/Modules/CanDoItAll.Modules.Workspace.Contracts/CanDoItAll.Modules.Workspace.Contracts.csproj

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace.Contracts/CanDoItAll.Modules.Workspace.Contracts.csproj)

Entire declared project graph root; only existing Security.Abstractions reference. Not an evaluated restore graph.

## WS13 — src/UI/CanDoItAll.Workspace.UI/CanDoItAll.Workspace.UI.csproj

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/UI/CanDoItAll.Workspace.UI/CanDoItAll.Workspace.UI.csproj)

Entire declared UI references: Core Contracts, History.Abstractions and BaseLib/framework.

## WS14 — tests/Components/CanDoItAll.Workspace.UI.Tests/WorkspaceBoundaryTests.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/tests/Components/CanDoItAll.Workspace.UI.Tests/WorkspaceBoundaryTests.cs)

Entire test source: transitive closure, forbidden/unresolved negatives and public contract exposure; not executed here.

## WS15 — src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs)

Lines 95-180: persistence before currency publication, normalized returned snapshot, protected Activity/logging commit facts.

## WS16 — src/Foundation/CanDoItAll.Infrastructure/Configuration/CurrencyFormatting.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/Foundation/CanDoItAll.Infrastructure/Configuration/CurrencyFormatting.cs)

Entire source checked to confirm CurrencyDisplayState.Update is a locked assignment, not a subscriber notification. No defect inferred from a nonexistent event.

## WS17 — src/UI/CanDoItAll.Workspace.UI/WorkspaceFilesSurface.razor

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/src/UI/CanDoItAll.Workspace.UI/WorkspaceFilesSurface.razor)

Entire renderer: editable immediate Extension/ExecutablePath and Use system default action bound to controller.Selected; WSC-R1 interaction path.

## WS18 — tests/Components/CanDoItAll.Workspace.UI.Tests/WorkspaceDraftTests.cs

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/tests/Components/CanDoItAll.Workspace.UI.Tests/WorkspaceDraftTests.cs)

Lines 145-313: secret retirement, unknown review, file new-successor and warning/list tests. Existing file test replaces the whole draft; it does not exercise a path-only edit of the same draft during destination-changing Save.

## WS19 — AGENTS.md

[Source](https://github.com/fyziktom/CanDoItAll/blob/186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf/AGENTS.md)

Entire current entry instructions: canonical UI seams, current Testing/CI and mandatory reviewed portability enforcement.
