# CanDoItAll FileTools Main Integration

This initiative bundle is the implementation and validation contract for integrating the standalone `CanDoItAll.FileTools` FileBrowser and FileInteraction packages into the main CanDoItAll application. It is preparation only: no product source, project reference, package feed, database, or UI behavior is changed by bundle creation.

## Outcome Contract

- Improve the main storage subsystem first with provider-native, bounded browsing contracts and implementations that remain independent of FileTools.
- Make large-source bounds structural: page one must not enumerate, metadata-probe, hash, sort, or retain an unbounded directory merely to return a bounded page.
- Prove Storage behavior, provider capability honesty, security boundaries, package intake, dependency direction, and composition before any product UI starts.
- Use one deliberately small end-to-end pilot: search and browse one project's authorized files, then open one known Markdown/text file read-only in FileInteraction.
- Preserve Project Structure image/PDF asset double-click: the existing dialog opens FileInteraction directly for its one authorized file, with zero FileBrowser initialization. FileBrowser is reserved for semantic multi-file/container browsing.
- Unlock broader project, Workbench, process-run, Resources, and editing stories only after the pilot and its architecture/UX cleanup gate pass.
- Keep the application large-screen desktop only. The named UI proof viewport is `1900x1200`; `1440x900` is the minimum regression viewport. No small, medium, tablet, or mobile tuning is in scope.
- Preserve FileTools independence: `C:\repositories\CanDoItAll.FileTools` must never reference the main app; `CanDoItAll.Infrastructure` must never reference FileTools.
- Treat browser keys, display paths, storage reference JSON, preview/download URLs, and `StorageJson.EncodeReferenceToken` output as descriptive data, never authority.

## Profile

- `initiative`

## Repository And Evidence Aliases

- `repo://` means `C:\repositories\CanDoItAll` and is the implementation repository.
- `filetools://` means `C:\repositories\CanDoItAll.FileTools`; use the pinned commit in `inputs/01-source-artifacts.md`.
- `legacy://` means `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration`; it is source evidence, not the execution bundle.
- `bundle://` means this directory.

## Execution Order

1. `SB01` re-entry, SDK/package, clean-worktree, component-catalog, and baseline gate.
2. `SB02-SB04` Storage browse contracts plus filesystem/IPFS/FTP providers.
3. `SB05` mandatory Storage architecture cleanup and progression decision.
4. `SB06-SB08` package adoption, cross-project integration boundary, authorization/handles/endpoints, cache/revision.
5. `SB09` mandatory backbone architecture cleanup and composition smoke.
6. `SB10` one project-files search/browse/read-only interaction pilot.
7. `SB11` mandatory pilot architecture and desktop UX cleanup.
8. `SB12-SB16` broader user stories, one independently closable surface at a time.
9. `SB17` expansion cleanup gate.
10. `SB18` final security, dependency, package, test, browser, and raw-input closure.

The dependency graph and stop/reopen rules are in `bundle://plan/01-phase-plan.md`.

## Durable Anchors

- Raw request: `bundle://inputs/00-original-request.md`
- Source pins and known tool gaps: `bundle://inputs/01-source-artifacts.md`
- Requirements: `bundle://requirements/01-normalized-requirements.md`
- Current repository evidence: `bundle://analysis/01-current-state.md`
- Target boundaries: `bundle://architecture/01-csharp-boundary-map.md`
- Storage/FileTools mapping: `bundle://architecture/05-storage-filetools-contract-map.md`
- Security and effects: `bundle://architecture/06-authorization-handles-and-effects.md`
- UI progression: `bundle://architecture/08-ui-story-progression.md`
- Performance audit and scale contract: `bundle://analysis/03-dotnet-performance-audit.md`, `bundle://architecture/10-performance-and-scale.md`
- Architecture checkpoints: `bundle://plan/architecture-checkpoints.md`
- Execution status and closure: `bundle://reviews/01-execution-report.md`

## Validation Summary

- Bundle preparation status: `Ready — prepared validator and manual readiness review passed 2026-07-12`
- Execution status: `Not started — bundle preparation only`
- Subbundle gate review: `SB01-SB18 entry, closure, progression, and reopen rules are defined; none executed`
- Final closure gate: `Not started — requires implementation proof from SB01-SB18`
- Main CodeAnalytics baseline: `snap-20260713002602-7de53bec`, seven product projects, 1,029 types, 8,284 members, no project cycle; one pre-existing Infrastructure module cycle between Persistence and ControlPlane.
- FileTools CodeAnalytics baseline: unavailable because the repo pins SDK `10.0.301` and this machine has `10.0.300`; direct source/package-boundary inspection is recorded and SB01 must close this environment gate before packaging.
- Components MCP baseline: unavailable after two calls returned `Transport closed`; SB01 must repeat library/recommendation/component/example discovery before UI markup or CSS work.

## Non-Goals

- No FileTools product redesign or transfer work; that is complete in the legacy bundle.
- No distributed cache secondary, durable cross-node revision system, or mobile UI.
- No trust in arbitrary absolute paths, unsigned reference tokens, browser state, or client capability flags.
- No new partial file on `ProjectStructurePage`, no added responsibility in `LiveProcessesDashboard`, `ProjectsPage`, `ProjectsBoard`, `ProjectModalHost`, `ResourcesPage`, or `RuntimeHostServiceCollectionExtensions` as the final owner.
- No silent provider fallback. Unsupported browse/search/stat/write operations must fail explicitly and predictably.
- No browser/session construction for a known single-file dialog, and no claim that a small returned page proves bounded provider work.

## Preparation Decision

The bundle is implementation-ready. Execution may start only at `SB01`; the first product-code phase is `SB02` Storage browsing. UI work is blocked until `SB09` passes.
