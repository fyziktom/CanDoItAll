
# UI Proof Surfaces

| UI ID | Route | Surface | Required change | Desktop screenshot | Narrow screenshot | Owning phase | Owning workstream |
| --- | --- | --- | --- | --- | --- | --- | --- |
| UI-001 | /settings?tab=storage | New storage catalog tab under SettingsPage.razor | Storage list/detail shell, add wizard, connection test, defaults, health, delete/disable. | artifacts/screenshots/storage-driver/settings-storage-desktop.png | artifacts/screenshots/storage-driver/settings-storage-narrow.png | Phase 04 | P4-WS01 |
| UI-002 | /workbench/{projectId} | Project structure create dialog | Show recommended storage, override selector, and optional create-new-storage shortcut for typed file/image/video nodes. | artifacts/screenshots/storage-driver/workbench-upload-desktop.png | artifacts/screenshots/storage-driver/workbench-upload-narrow.png | Phase 04 | P4-WS02 |
| UI-003 | /workbench/{projectId} | Project structure selection panel + modal preview | Capability-based preview/open/download actions, storage facts, and overlay validation. | artifacts/screenshots/storage-driver/workbench-preview-desktop.png | artifacts/screenshots/storage-driver/workbench-preview-narrow.png | Phase 04 | P4-WS02 |
| UI-004 | /workbench/{projectId} | Project structure storage node flows | Create storage-system node, link to storage record, set subtree default/reference, view facts. | artifacts/screenshots/storage-driver/workbench-storage-node-desktop.png | artifacts/screenshots/storage-driver/workbench-storage-node-narrow.png | Phase 04 | P4-WS02 |
| UI-005 | /factory | Prompt Factory attachment flow | Show recommendation/override for attachments and resolve previews through new access service. | artifacts/screenshots/storage-driver/factory-attachments-desktop.png | artifacts/screenshots/storage-driver/factory-attachments-narrow.png | Phase 04 | P4-WS03 |
| UI-006 | /settings?tab=data-sources or storage-snapshots | Snapshot/IPFS or publish-related management surfaces if touched | Validate any storage-backed snapshot/publish UI that was changed. | artifacts/screenshots/storage-driver/snapshot-surface-desktop.png | N/A | Phase 04 | P4-WS04 |

## Screenshot review questions

- Is every label readable without clipping or overlap?
- Do wizard steps and sticky actions remain visible without horizontal scrolling?
- When dialogs/dropdowns are open, is any content clipped by the viewport or parent container?
- Do preview panels reserve enough space for documents/images/video without collapsing controls?
- Are unsupported actions hidden or disabled instead of failing after click?
