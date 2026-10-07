# Source register

Repository: `fyziktom/CanDoItAll`. Review SHA: `20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d` (provenance, not an execution pin).

Source reads and proposed reasoning do not imply an executed build or runtime proof. Search-only references are explicitly marked.

## EV01 — git-metadata

Kind: `git-metadata`.

Live branch was read at entry and again at the end of source review on 2026-09-29; HEAD remained 20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d. GitHub reported the commit signature verified. Provenance only, not a checkout instruction.

Source: https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling

## EV02 — git-metadata

Kind: `git-metadata`.

Connector compare read: three commits after the previous Memory implementation; includes archived Resources handoff, Memory correction and Resources extraction. Changed-file statistics are inventory, not runtime proof.

Source: https://github.com/fyziktom/CanDoItAll/compare/5f7f8329e3e3e30bd1451fc72757e04847579754...20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d

## EV03 — docs/architecture/resources-ui-boundary.md

Kind: `repository-file`.

Entire 334-line implementation receipt read in two ranges, 1-220 and 218-335. Counts, graphs, measurements and completion statements are implementer-reported; raw ignored artifacts not inspected.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/docs/architecture/resources-ui-boundary.md

Observed blob SHA: `d2ab492b9a2b4c1fa1e2acbf1caa31240273816e`.

## EV04 — external-primary-document

Kind: `external-primary-document`.

Official Microsoft documentation retrieved on 2026-09-29. Supports re-entry around incomplete awaits; does not prove any project test passed.

Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0

## ME01 — src/Modules/CanDoItAll.Modules.Memory.Presentation/MemoryProvidersPageController.cs

Kind: `repository-file`.

Lines 245-360 read: accepted snapshot revision invalidates the current query result and only auto-derived feedback context. Earlier method bodies were not re-read in full in this iteration.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Memory.Presentation/MemoryProvidersPageController.cs

Observed blob SHA: `2cb564cee0a8b2cb6a77f2482985c97a531a5253`.

## ME02 — tests/Components/CanDoItAll.Memory.UI.Tests/MemoryResultOriginTests.cs

Kind: `repository-file`.

Entire file read. Controlled replacement/removal orderings, same-revision/failed reads, accepted identity and manual feedback tests. Not executed here.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/tests/Components/CanDoItAll.Memory.UI.Tests/MemoryResultOriginTests.cs

Observed blob SHA: `0d6f7e4eb305b0e3e17c659d48627e52c937d371`.

## RS01 — src/Modules/CanDoItAll.Modules.Resources.Presentation/ResourceRegistryController.cs

Kind: `repository-file`.

Entire file read in ranges 1-260 and 260-470. Route/selection, reference refresh, mutation admission, reconciliation and recovery reviewed.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Resources.Presentation/ResourceRegistryController.cs

Observed blob SHA: `bf9ea7f3c27b732544b53448dcc69aeef65aaef7`.

## RS02 — src/UI/CanDoItAll.Resources.UI/ResourceRegistryWorkspace.cs

Kind: `repository-file`.

Entire file read: editor origin, per-field revisions, EditContext, receipts and workspace contract.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/UI/CanDoItAll.Resources.UI/ResourceRegistryWorkspace.cs

Observed blob SHA: `8471fd8be06231c57b778b065f3c119613d2579e`.

## RS03 — src/UI/CanDoItAll.Resources.UI/ResourcesWorkspaceSurface.razor

Kind: `repository-file`.

Lines 1-120 and 125-210 read, including real Refresh/selection controls, readiness messages, governed branch and form. Remaining markup inventoried by compare, not fully re-read.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/UI/CanDoItAll.Resources.UI/ResourcesWorkspaceSurface.razor

Observed blob SHA: `9be1b1ca2df417632ab90b00f75aa29a2a7675eb`.

## RS04 — src/Modules/CanDoItAll.Modules.Resources.Presentation/ResourceBrowseController.cs

Kind: `repository-file`.

Entire 388-line file read in two ranges: source/preview leases, promotion, action receipts, release and disposal.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Resources.Presentation/ResourceBrowseController.cs

Observed blob SHA: `bd3c6771dce8b5f6657afbb3c99c2eb10a1d121f`.

## RS05 — src/Modules/CanDoItAll.Modules.Resources/ResourceRegistryOwner.cs

Kind: `repository-file`.

Entire production adapter read: canonical profile identity, existing owner calls, reference-only secret projection and project admission refusal.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Resources/ResourceRegistryOwner.cs

Observed blob SHA: `b62d0e1957931236cd982a1e520aff74d0b20c2f`.

## RS06 — src/Modules/CanDoItAll.Modules.Resources/ResourceBrowseOwner.cs

Kind: `repository-file`.

Entire production adapter read: current source scope/revision/provider membership and promotion observation mapping.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Resources/ResourceBrowseOwner.cs

Observed blob SHA: `6034cf79ba64c3e8d1c922a3c1c6a44d01c5f9c4`.

## RS07 — tests/Components/CanDoItAll.Resources.UI.Tests/ResourceRegistryTests.cs

Kind: `repository-file`.

Entire file read: existing controlled owner tests. No current case was found covering reference refresh while exact editor acquisition is pending or after it failed. Not executed.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/tests/Components/CanDoItAll.Resources.UI.Tests/ResourceRegistryTests.cs

Observed blob SHA: `df0d06a059d942d43ef17c2f17eb7b14e92013fa`.

## RS08 — src/UI/CanDoItAll.Configuration.UI/ConnectorConfigFieldEditor.razor

Kind: `repository-file`.

Entire moved shared renderer read: stable configuration draft, immediate text, unavailable options and secret-reference-only options.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/UI/CanDoItAll.Configuration.UI/ConnectorConfigFieldEditor.razor

Observed blob SHA: `7b12ea6ddc4ca585972dbb4342b6c077aefd0111`.

## RS09 — src/Modules/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs

Kind: `repository-file`.

Entire routed host read, including mapping Registry.Access into Agent context readiness.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs

Observed blob SHA: `0018bb4cae1ac480e159750749e489b6c7d02c76`.

## WS01 — src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor

Kind: `repository-file`.

Entire current page read: defaults, secret vault, hosted panel branches and API access; source of selected scope.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor

Observed blob SHA: `b5c911b1e0d7eed9855a3633897f40d499f01f9e`.

## WS02 — src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs

Kind: `repository-file`.

Entire file read: eight navigation items, Providers redirect, initialization, mutable saves and secret selection/reset.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs

Observed blob SHA: `dda09fe3244e04b12b3f3e3cc2a848617d2b1a73`.

## WS03 — src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj

Kind: `repository-file`.

Entire project file read: heavy implementation references, existing Configuration.UI and ProviderHistory.Abstractions edges. Not an evaluated graph.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj

Observed blob SHA: `0ba9c2b4324ae12898197980555aea054f682fe6`.

## WS04 — src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs

Kind: `repository-file`.

Entire returned file read: mixed DTO/EF/service, latest-record semantics, normalization, currency state and post-save activity.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs

Observed blob SHA: `859feb4621cfc8cbf134440d8a0e32b13674eded`.

## WS05 — src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs

Kind: `repository-file`.

Entire file read across 1-340 and 338-495: metadata-only list, explicit decrypted editor, staged vault value, metadata save, old-value cleanup and protected deletion.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs

Observed blob SHA: `0f99256dc708e2a199cf79a10abd7a6854192353`.

## WS06 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/FileApplicationSettingsPanel.razor

Kind: `repository-file`.

Entire panel read: explicit save/delete, mutable selection, machine-local association editor and list refresh handling.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/FileApplicationSettingsPanel.razor

Observed blob SHA: `908e24d65d5778c8c794727fde62c8684fb93223`.

## WS07 — src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs

Kind: `repository-file`.

Lines 1-270 read: normalization/host-bound states, public port, save/delete/logging and beginning of legacy-read migration. Remaining path-validation/migration helper implementation must be read by Codex before editing.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs

Observed blob SHA: `2ec42bf14baf670906236fc810d7b9bee26bfa75`.

## WS08 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderHistoryPolicyPanel.razor

Kind: `repository-file`.

Entire panel read: explicit load, typed fields, validation, shorter-retention preview and confirmation.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderHistoryPolicyPanel.razor

Observed blob SHA: `bfbb1227066cf2b3ac5b2b36362eec3e06d3d501`.

## WS09 — src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderHistoryPolicyPanel.razor.cs

Kind: `repository-file`.

Entire host code read: authentication/profile retirement, per-operation CTS, expected-version update and preview binding.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderHistoryPolicyPanel.razor.cs

Observed blob SHA: `2c0d2d92ffce9df0c940b369599249cdb6083daa`.

## WS10 — src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions/HistoryPolicy.cs

Kind: `repository-file`.

Entire lightweight policy contract read. Reuse this existing seam instead of duplicating the policy protocol.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions/HistoryPolicy.cs

Observed blob SHA: `d2c6b738dd84c2bc2e7d334ecc1b2522c0e69b4e`.

## WS11 — src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryPolicyStore.cs

Kind: `repository-file`.

Entire store read: Manage authorization, expected generation, write fence, version conflict, transaction and audit. No runtime tests executed.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryPolicyStore.cs

Observed blob SHA: `1f8026321875e0020a9b63532b2cce7beb129943`.

## WS12 — tests/Components/CanDoItAll.Tests.Components/ProviderHistoryPolicyPanelTests.cs

Kind: `repository-file`.

Entire existing test file read. Explicit load, bounded preview, invalid values, authentication/profile changes and route regression. Not executed.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/tests/Components/CanDoItAll.Tests.Components/ProviderHistoryPolicyPanelTests.cs

Observed blob SHA: `ceb89f0b2e0611e169f8563ee308e7c1fa85b302`.

## WS13 — src/App/CanDoItAll.Web/Api/WorkspaceSettingsApi.cs

Kind: `repository-file`.

Entire endpoint file read: distinct HTTP validation, six-field replacement, scopes, saved snapshot and pending read-back header. Preserve existing behavior.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/App/CanDoItAll.Web/Api/WorkspaceSettingsApi.cs

Observed blob SHA: `9dd0443afdf08ebdcaa9c5d2d892b60a11e9ab9c`.

## WS14 — src/Modules/CanDoItAll.Modules.Workspace/Providers/WorkspaceProviderCatalog.cs

Kind: `repository-file`.

Entire small record/read-port file read: provider ID/name/enabled, no implementation bodies.

Source: https://github.com/fyziktom/CanDoItAll/blob/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Providers/WorkspaceProviderCatalog.cs

Observed blob SHA: `412e9c91a208945f6ad731e76914b04955eb7865`.

## WS15 — directory-inventory

Kind: `directory-inventory`.

Pinned directory tree read. Confirms large database/storage panels and separate API token/user dialogs. Their implementation bodies are intentionally not claimed audited or included in the extraction scope.

Source: https://github.com/fyziktom/CanDoItAll/tree/20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components

## WS16 — discovery-only

Kind: `discovery-only`.

Default-branch connector search located SettingsPageDataSourcesTests, SecretProviderSelectionTests and ProviderFeatureMatrixTests. Exact current checkout contents/discovery must be resolved by Codex; these are candidate consumers, not current executed proof.

Source: https://github.com/fyziktom/CanDoItAll

## EV05 — handoff-input

Kind: `handoff-input`.

Locally available previous handoff read and shared foundation copied byte-for-byte. This is archived task input, not implementation evidence.

Source: CanDoItAll_Resources_UI_Decoupling_Codex_Max.zip

## EV06 — git-metadata

Kind: `git-metadata`.

Current GitHub Actions query for this exact SHA returned total_count 0. This neither confirms nor disproves the implementer-reported local runs.

Source: https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d&per_page=10
