# Proof Manifest — SB07

- Status: `Completed`.
- Proof tier: `Behavioral`.
- Owned requirements: `SCUI-031`, `SCUI-032`, `SCUI-033`, `SCUI-034`, `SCUI-035`, `SCUI-036`, `SCUI-037`, `SCUI-038`, `SCUI-043`, `SCUI-058`, `SCUI-061`, `SCUI-062`.
- Start commit: `1a49848f34bcd72adb0aa11d4c1453724fed5a02`.
- Candidate commit: `154a23e0daaa6af21081b25303c51a86477d8ab3` (`chat definition dialog`).
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Semantic contract: `bundle://proof/SB07/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB07/architecture-gate.md`.
- Execution report: `bundle://proof/SB07/execution-report.md`.

## Scope

SB07 adds the internal definition catalog and wide editor over the SB06 gateways. It supports status filtering, bounded paging, reusable definition fields, provider capability selection, advanced model/response settings, revisions, optimistic concurrency reload, and status changes. No `/chats` route, navigation entry, conversation workspace, streaming follower, or floating integration is activated.

## Source and proof identity

- Production/test candidate tree: `154a23e0daaa6af21081b25303c51a86477d8ab3`.
- Changed source: 9 files, 1,419 insertions, 7 deletions.
- Final-diff impact correlation: `code-analytics_110e6c986ee44d3096fed08e745a6f64`.
- Architecture snapshot: `snap-20260817001529-f0f61dd3`.
- Snapshot correlation: `code-analytics_8fe55bb6a11846919843f61f8487671d`.
- Dependency query: `code-analytics_2bf2bfc1dab244dd992cebd469cb69ba`.

## Changed source paths and ranges

- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/_Imports.razor`: lines 1, 3, and 5-8.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionCatalogPanel.razor`: lines 1-234.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionCatalogPanel.razor.css`: lines 1-12.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionEditorDialog.razor`: lines 1-554.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionEditorDialog.razor.css`: lines 1-7.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionEditorForm.cs`: lines 1-152.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionPresentationMapper.cs`: lines 1-129.
- `tests/Components/CanDoItAll.Tests.Components/LlmChatDefinitionUiTests.cs`: lines 1-318.
- `tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs`: lines 1402-1404, 1517, 1562, 1629, and 1753.

## Validation and artifact matrix

- Focused Components: 5 passed, including realistic positive and negative cases.
- Required Components workspace: 1,015 passed, 0 failed, 0 skipped.
- Required Integration workspace: 851 initial passes plus 3 exact corrected passes; 854 non-skipped tests covered and 1 environment-dependent live-provider test skipped.
- Web build: pass, 0 warnings, 0 errors.
- Static dependency, route, anti-stub, sensitive-material, and whitespace scans: pass.
- Architecture: fresh scoped snapshot, no blocking errors and no cycles.
- Browser: intentionally deferred; route activation is forbidden until SB10.

## Artifact hashes

- `7337baa08985e3bcb7b835444c060d50667fe507d6ed417f983cab03f9d8e81c` — `LlmChatDefinitionCatalogPanel.razor`.
- `c21ca8658a3c64ccd1514b55d270ad1d35e6b30c477de64656251519beee4264` — `LlmChatDefinitionCatalogPanel.razor.css`.
- `d54cb0e31c06b17d36f3a371aab957e1a2d84ed21806b61067778168f0493dbb` — `LlmChatDefinitionEditorDialog.razor`.
- `f96b2c456517e92ab4b64eff20861f31842fcf3ee9b8b263a2506cb278ab4094` — `LlmChatDefinitionEditorDialog.razor.css`.
- `e7c6cfa1fd1b5e19cf9ceed6d4b288c2a04e145b28a54431d441d7d65ca54aa4` — `LlmChatDefinitionEditorForm.cs`.
- `8d7a59f866ce434edeac76d4a5f023bdfce982355cc3cbf756f27ce40dda948a` — `LlmChatDefinitionPresentationMapper.cs`.
- `e88424a5d7b0838b746f60b63c15b910b4ea8cf173a9d08e829e86dee3d1013d` — `LlmChatDefinitionUiTests.cs`.
- `fedf55512042226e36f899411b14c312b426bc76b8fc9aeea2d0f800b64e3c48` — `LlmChatPersistenceIntegrationTests.cs`.

## Progression

All acceptance criteria pass. SB08 is unlocked. The route remains unadvertised and floating Simple Chat integration remains locked until CP2.

The manifest omits its own self-referential digest. Its integrity is checked by the bundle validator and proof commit.
