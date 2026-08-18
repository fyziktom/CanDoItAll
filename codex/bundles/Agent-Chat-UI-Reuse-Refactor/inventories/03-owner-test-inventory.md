# Owner-test inventory

This file lists likely anchors. It does not override live impacted-test selection.

## Existing component owners

- `ChatWorkspacePanelTests`
- `AgentChatPanelResponsivenessTests`
- `AgentChatModalTests`
- `AgentCatalogPanelTests`
- `AgentCompactListTests`
- `AgentChatContextSurfaceProviderTests`
- `AgentDetailsDialogAvatarGenerationTests`
- `AgentDetailsDialogCapabilityTests`
- `AgentDetailsDialogDeletionTests`
- `AgentDetailsDialogProjectStructureAccessTests`
- additional `AgentDetailsDialog*` tests discovered live
- `ProviderModelSelector*` tests discovered live
- floating Agent Chat host/settings tests discovered live
- contextual Agent workspace tests discovered live
- Process workspace tests discovered live

## New neutral owners to add

- participant card/list/item/picker tests;
- opaque key tests;
- thread rail/list/history tests;
- message/markdown/transcript tests;
- composer callback/focus/disabled-state tests;
- editor identity/runtime field tests;
- floating catalog/lifecycle-field tests;
- forbidden-dependency/source guard tests or scripts.

## Proof discipline

- Do not create one enormous snapshot test as the only owner.
- Prefer focused behavior tests with semantic assertions.
- Keep compatibility tests for current Agent component public contracts.
- Browser tests prove composition and overlays, not every component permutation.
