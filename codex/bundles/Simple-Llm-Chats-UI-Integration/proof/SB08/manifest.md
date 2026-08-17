# Proof Manifest — SB08

- Status: `Completed`.
- Proof tier: `Behavioral`.
- Owned requirements: `SCUI-031`, `SCUI-039`, `SCUI-040`, `SCUI-041`, `SCUI-042`, `SCUI-043`, `SCUI-044`, `SCUI-045`, `SCUI-058`, `SCUI-059`, `SCUI-060`, `SCUI-062`.
- Start commit: `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`.
- Candidate commit: skipped after the user-authorized commit attempt blocked on unavailable interactive signing; the exact staged source is identified by the hashes below.
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Semantic contract: `bundle://proof/SB08/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB08/architecture-gate.md`.
- Execution report: `bundle://proof/SB08/execution-report.md`.

## Scope

SB08 adds an internal, route-inactive Simple Chat conversation workspace over the SB06 gateways. It supports active-definition creation, bounded conversation and transcript paging, selection, rename, archive, send admission, stable retry identity, pending User projection, and pinned definition revision display. Streaming follow, cancellation, reconnect, route activation, navigation, and floating integration remain deferred.

## Source and proof identity

- Production/test candidate: staged working tree based on `2d6dac63a6350a3bdd538c34d11e68ce364a74d4`; commit skipped as explicitly authorized after signing blocked.
- Changed source: 6 files, 1,673 insertions, 0 deletions.
- Final-diff impact correlation: `code-analytics_de47ff5257044f2f9b0ad407c109b388`.
- Architecture snapshot: `snap-20260817103441-e2dc18f1`.
- Snapshot correlation: `code-analytics_b2d2d24e05c84b24b486ddc53ed34b91`.
- Dependency query: `code-analytics_381b9450e2b24ff28e8f1fecdbdd72da`.

## Changed source paths and ranges

- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/_Imports.razor`: line 9.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationPresentationMapper.cs`: lines 1-152.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspace.razor`: lines 1-518.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspace.razor.css`: lines 1-24.
- `src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspaceController.cs`: lines 1-548.
- `tests/Components/CanDoItAll.Tests.Components/LlmChatConversationWorkspaceTests.cs`: lines 1-430.

## Validation and artifact matrix

- Failing-first focused build: failed with the expected missing workspace type.
- Focused Components: 5 passed, including positive, negative, retry, concurrency, and hard-cap cases.
- Required Components workspace: 1,020 passed, 0 failed, 0 skipped.
- Required Integration workspace: 854 passed, 0 failed, 1 expected environment-dependent live-provider skip.
- Static route, anti-stub, forbidden-feature, dependency, and whitespace scans: pass.
- Architecture: fresh scoped snapshot, no blocking errors and no cycles.
- Browser: intentionally deferred; route activation is forbidden until SB10.

## Artifact hashes

- `ab9af34bf60beb6d5e19c9e29de240ad8da024fb717a6b3f360267fc02082c3d` — `_Imports.razor`.
- `0368ee2faf9d3b25162903a5885ee0902078592fa5fe35470f39b8c4e42b7a65` — `LlmChatConversationPresentationMapper.cs`.
- `466c79e7049a1c978169366d7d7e58495cc2f7a5e408f71ddb1eaa5e167864c5` — `LlmChatConversationWorkspace.razor`.
- `7e4e0b17083c3a4f8a0ec4e2f3706ba50f88339b8048980e4d91567ed7469b68` — `LlmChatConversationWorkspace.razor.css`.
- `01f804e470458ea0c037c0a91774a69636fbf3fa1aff5920853e072dc4b6b825` — `LlmChatConversationWorkspaceController.cs`.
- `5729b0744a8659d9cbbb2773e49bf49265a00d1a056831f0afd0d49f4eb91c37` — `LlmChatConversationWorkspaceTests.cs`.

## Progression

All acceptance criteria pass. SB09 is unlocked. The route remains unadvertised and floating Simple Chat integration remains locked until CP2.

The manifest omits its own self-referential digest. Its integrity is checked by the bundle validator and final repository commit.
