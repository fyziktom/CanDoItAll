
# Structured Input

## Core Objective

- Add a real storage-driver platform to CanDoItAll so uploads, previews, exports, downloads, storage defaults, and project-structure references can work through FileSystem, IPFS, FTP, and future providers without local-filesystem assumptions leaking everywhere.

## Hard Constraints

- Support FileSystem, IPFS, FTP, and future providers.
- Persist storage catalog settings and default routing rules.
- Recommend default storage during uploads based on file type and expected editability.
- Provide reusable storage UI components and a settings wizard with connection testing.
- Split execution into four phases with phase folders and nested workstreams.
- Inventory all touched upload/view/download/file-use surfaces and deliver that map as XLSX.
- Force real Playwright MCP validation with screenshots and explicit visual review questions.
- Keep execution checklists strong enough that Codex cannot honestly skip steps.
- Keep code comments in English.

## Source Artifacts

- Primary source zip: `/mnt/data/CanDoItAll-canvas-drawing-refactor.zip`
- Extracted repo root: `C:\repositories\CanDoItAll`
- Detailed source list: `inputs/01-source-artifacts.md`
- Touchpoint workbook: `inventories/04-storage-driver-touchpoints.xlsx`

## Input Coverage Signals

- `WorkspaceStorage.cs` is explicitly called out as insufficient; this cannot be treated as a small additive change.
- The request explicitly names provider extensibility, persisted defaults, upload recommendations, settings UI, project-structure storage nodes, batch transfer, and full touchpoint inventory.
- The request uses absolute language such as “all uploads and views/downloads/use of files”, “must”, and “Final zip must...”; those statements are preserved and enumerated instead of silently narrowed.

## Dependency And Sequencing Signals

- Phase 01 is a critical foundation because schema, contracts, and compatibility seams define what later phases are allowed to call.
- Phase 02 depends on Phase 01 contracts and unblocks Phase 03 proof and Phase 04 adoption.
- Phase 03 must land before Phase 04 can claim closure, because UI adoption without new proof harnesses would be untrustworthy.
- Phase 04 depends on all earlier phases and closes the inventory plus QA audit.

## Validation Expectations

- Prepared-stage validator must pass before the bundle is handed off.
- Each phase must have entry/closure gates in the execution report.
- Unit + integration + Playwright automated coverage are required.
- Manual Playwright MCP proof is also required for changed UI surfaces.
- Blocked provider proof (especially FTP real-protocol proof) must remain blocked; it cannot be faked or described as complete.

## UI Validation Strategy

- Use a large-screen headed Playwright MCP pass first (`1900x1200` target) for every changed UI surface.
- Use a narrower-width pass (`1366x900` or similar) for layout/overflow regression checking after the large-screen pass.
- For overlays/dialogs/dropdowns/wizard steps, capture open-state screenshots and review clipping, viewport overflow, button reachability, sticky footer behavior, and z-layering.
- Reuse existing Playwright artifact patterns from `tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs`.

## Browser Validation Analytics

- Every UI-relevant phase must log route, viewport, Playwright MCP actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Screenshot findings must be summarized, not merely attached.
- The QA audit must compare screenshot evidence against the XLSX UI surfaces sheet.

## Working Assumptions

- Storage catalog persistence belongs in app data (with secrets references) rather than control-plane JSON, while bootstrap workspace-root defaults remain available through app options.
- A compatibility seam is required so existing `IFileStore` / `IManagedArtifactStore` consumers can be migrated incrementally.
- A dedicated unified access route/service is safer than letting all UI flows talk directly to raw provider URLs or filesystem paths.

## Primary Risks

- Media and preview flows currently rely on `MediaRelativePath` and `/managed-files`; replacing that assumption is cross-cutting.
- FTP real-protocol integration proof may be environment-sensitive and must be handled honestly.
- Storage-node design in project structure can sprawl if it is not anchored to a concrete metadata/linking strategy.
- UI validation can be faked if automated tests are treated as a substitute for Playwright MCP screenshot review.

