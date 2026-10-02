# Source register


All product observations below are from connector reads at the recorded application SHA.
Search results on the default branch were used for discovery only, then relevant files were
fetched at that SHA. Partial reads are labeled; they are not a full behavioral audit of the
entire module. The original PP1 code/report distinguishes recorded evidence from reviewer execution.

## R01 — Recorded test results, blocked multi-instance lane, hot-reload limitation and scope

- File: `docs/architecture/provider-profiles-ui-pp1.md`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `d2025807b265b6d0b253a8cd34f7629c1edeab9d`
- Read coverage: Full maintained report (225 lines)
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/docs/architecture/provider-profiles-ui-pp1.md

## R02 — Production view adapter and shared-delivery composition

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `be6a9f17790b64408ef3ae79a91a0f8acdcd37bf`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs

## R03 — Imported display mapping and actual renderer state

- File: `src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderProfilesSurface.razor.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `24d1c30566063d5af75bcfb73f61b3d66e94a2e2`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderProfilesSurface.razor.cs

## R04 — Raw default model in operator tooltip

- File: `src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderProfilePresentation.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `341ce4cab692f4051219b076ab5dab9a14a512f9`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderProfilePresentation.cs

## R05 — Real tooltip wiring in catalog tree

- File: `src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderProfileTreeNodeBuilder.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `6729fe000dd7a49d4bbe46d9bdfc2d462c85b961`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderProfileTreeNodeBuilder.cs

## R06 — Raw input, field revisions and validation

- File: `src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderEditorDraft.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `4de271260ddb423de08dd716f1cb5a600e387f4b`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/UI/CanDoItAll.AgentFramework.Providers.UI/ProviderEditorDraft.cs

## R07 — Whole submission and per-field/price-row reconciliation

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Services/ProviderEditorSubmission.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `8e08ee0c658450cf0cf00b4315a1eadf1b887ef7`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Services/ProviderEditorSubmission.cs

## R08 — Existing synthetic two-instance UI test and its limits

- File: `tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderTwoInstanceUiAcceptanceTests.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `not recorded from truncated response`
- Read coverage: Requested lines 1–280; connector output truncated; read entry, settings, preparation, opt-in and relay preflight portions
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderTwoInstanceUiAcceptanceTests.cs

## R09 — Existing metadata comparisons and synthetic catalog rewriting

- File: `tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderMetadataUiChecks.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `not recorded from truncated response`
- Read coverage: Requested lines 1–250; connector output truncated near ReadAsync; read Configure/Mirrored/Agent/SimpleChat helpers
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderMetadataUiChecks.cs

## R10 — Existing scenarios, fixed default paths and orchestration identity

- File: `tools/SharedProviders/Run-SharedProviderE2E.ps1`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `6b7a003db770259cfdeb5d29d11e3cd83f11d557`
- Read coverage: Lines 1–160 only; full runner must be inspected before execution
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/tools/SharedProviders/Run-SharedProviderE2E.ps1

## R11 — Container safety, separate database roles, credentials and endpoints

- File: `compose.shared-providers.e2e.yaml`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `2b1f0f856ff8a86224577cd218c8314c92be0bd3`
- Read coverage: Lines 1–230 only; topology complemented by runner inventory
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/compose.shared-providers.e2e.yaml

## R12 — Sharing renderer and keyed imported child

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `7f58e8188ffddb5efdef2a6346f1f372c2a0d7fc`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor

## R13 — Publication/import mutations, recovery and delivery

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `d83e590465795b387e4a93759aade4f098b1aff5`
- Read coverage: Full file through Dispose and enum
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor.cs

## R14 — Source list and editor UI, references and native operations

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderSourcesDialog.razor`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `11c9dd9ff8e9e4ce95a66183388449de32ed5ba6`
- Read coverage: Lines 1–220; catalog dialog tail not read in this revision
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderSourcesDialog.razor

## R15 — Source draft, read/mutation ownership and accepted identity

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderSourcesDialog.razor.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `1ad55bb193a29b66282511aab36b4bd270b71ce1`
- Read coverage: Lines 1–245; later discovery/retry implementation to inventory at execution
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderSourcesDialog.razor.cs

## R16 — Local imported alias/enabled draft and display names

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderImportedProfileContent.razor`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `258342e51cad218ab771f488035abdbf73d812a6`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderImportedProfileContent.razor

## R17 — Existing validated runtime source/import relationship

- File: `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderValidatedRuntimeShape.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `960406d689c22ec1e8e560d5b79bca06b02e519a`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderValidatedRuntimeShape.cs

## R18 — Management requests, concurrency and safe source snapshots

- File: `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Contracts/SharedProviderManagement.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `dbf504f7de96fdbc7c543d03321c77bfcf7b45b3`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Contracts/SharedProviderManagement.cs

## R19 — Opaque routing IDs scoped to publication and exact upstream token

- File: `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderRoutingModelIdCodec.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `732a112c7624e39ee5b432b2226aab3d8fb9f885`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderRoutingModelIdCodec.cs

## R20 — Published names, suggested flags, prices, default and routing index

- File: `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `28be8ee2669cd4d2f1eb9f54c0e822cf0d1f374e`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs

## R21 — Imported native shape and availability/publication constraints

- File: `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `1740fc1ecd8f6b45c3613dc13bfc44b619bf2d4c`
- Read coverage: Lines 1–245
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs

## R22 — Stored snapshot integrity and revision validation

- File: `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderPublicationSnapshotReader.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `67359af99aac9e18c033ae69f9d1ed08860a294d`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderPublicationSnapshotReader.cs

## R23 — Native OpenAI versus source-managed suggestion mapping

- File: `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentProviderPresentationMapper.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `9759b76dee42394c50ac0a84a954e5384665ddd6`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentProviderPresentationMapper.cs

## R24 — Catalog refresh plus same-ID acquired-editor no-op

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderProfilesSession.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `d4e1dc0e7c35a7e826e4dc88bf2608eac9d85122`
- Read coverage: Lines 1–225 through shared-reconciliation entry
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderProfilesSession.cs

## R25 — Sync, pending delivery, callback and effect lifetime

- File: `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderRefreshButton.razor`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `fe5d1384a0a8ab87ed0a4386009b62c942d56116`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderRefreshButton.razor

## R26 — Existing history acceptance, credential mutation and cleanup risks

- File: `tests/Playwright/CanDoItAll.Tests.Playwright/ProviderHistoryUiAcceptanceTests.cs`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `fec4e7e0a01c0130beb7474c4281fdbee8316f9b`
- Read coverage: Lines 1–155
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/tests/Playwright/CanDoItAll.Tests.Playwright/ProviderHistoryUiAcceptanceTests.cs

## R27 — Current repository entry rules and static closure

- File: `AGENTS.md`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/AGENTS.md

## R28 — Current engineering boundaries, test triggers and desktop rules

- File: `.github/copilot-instructions.md`
- Commit: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`
- Observed blob: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`
- Read coverage: Full file
- Source: https://github.com/fyziktom/CanDoItAll/blob/00c395ba0f4ce62c611c62718d8a510f9044f7b4/.github/copilot-instructions.md
