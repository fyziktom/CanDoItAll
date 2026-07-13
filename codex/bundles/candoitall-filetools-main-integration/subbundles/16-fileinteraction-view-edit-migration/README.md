# SB16 FileInteraction View Edit Migration

## Status

- `Ready`

## Objective

- Incrementally replace known main-app preview/edit paths with explicitly registered FileInteraction viewers/editors and authorized awaited save, deleting duplicate legacy behavior only after per-type proof.

## Covered Inputs

- N007, N010-N017; R012, R022-R040.

## Prerequisites

- SB15 Completed; SB07 authority/save and SB08 revision remain trusted; read-only pilot accepted.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureSupportDialogs.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Workflows.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructureNodeHelpers.cs`
- `C:\repositories\CanDoItAll.FileTools\docs\file-interaction.md`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Core`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Components`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Markdown`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `bundle://architecture/10-performance-and-scale.md`

## Deliverables

- Host interaction coordinator/dialog with explicit selected packages/profiles/renderers: built-in text/raster/PDF/inert fallback, optional Markdown, and host Mermaid adapter where required.
- Direct known-file contract from authorized `FileReference`/content source; no FileBrowser dependency in the interaction coordinator or Project Structure image/PDF dialog path.
- View/Edit controlled mode, close guard, bounded content, dirty/saving/conflict/error states, history/preview only where registered.
- Awaited authorized save adapter enforcing expected revision/overwrite permission and revision bump after success.
- Per-type staged migration: characterize -> replace -> prove positive/hostile/replacement/save -> remove old duplicate path.
- Unsupported/oversize/SVG/raw Markdown/PDF embedded-action limitations explicit and safe.
- Legacy unsigned route/compatibility bridge final removal or documented secured deprecation decision.

## Dependency Impact

- SB17/SB18 depend on safe mutation and absence of duplicate bypass paths.

## Validation Depth

- Proof tier: `Governed`.
- Mutating migration/security boundary; require `bundle://proof/SB16/manifest.md` and semantic invariants.

## Implementation Steps

1. Inventory each remaining viewer/editor/type/entry point and add characterization/failing-first hostile/revision tests; rerun SB13's accepted Project Structure image/PDF double-click/dialog and zero-browser-call proof as a prerequisite regression.
2. Configure only required FileInteraction packages through existing builder and Components/Mermaid guidance.
3. Implement focused host coordinator/dialog and controlled state/lifetimes.
4. Migrate one type at a time; pass one known file directly and do not batch-delete legacy paths.
5. Implement/verify save/history/preview only for supported types.
6. Delete duplicate old behavior after per-type browser/host proof.
7. Run governed tests/red-team/browser/dependency/no-bypass/C# gate.

## C# Architecture Impact

- Provider/renderer composition and modular refactoring out of Workbench page/dialog branches.

## Boundary Ownership

- FileTools owns generic interaction; host owns authorization/content/save/type adapters; Components Mermaid wrapper remains in Components.

## Dependency Direction

- Optional renderer dependencies stay at outer UI/composition; no Markdig/Mermaid/UI dependency in Infrastructure/Integration.Abstractions.

## Pattern Decision

- PSR-06 existing builder plus Adapter for Mermaid/host save; no extension switch/service locator.

## Testability Contract

- Coordinator/save/adapter direct tests without page; component tests for state/lifecycle; browser for renderer/effect truth.

## Partial Class Policy

- No new Workbench page partial; old partial responsibilities must shrink when migrated.

## Architecture Proof Required

- Per-type migration matrix, old-owner shrink/deletion, package graph, direct tests, no bypass/partial/service-locator, dependency/cycle, C# gate.

## Scope Exceptions

- No full Diff engine, Office editor suite, hostile PDF action mediation beyond documented renderer policy, or all-format claim.

## Do Not Do

- Do not register every renderer package, initialize FileBrowser to show one known file, raise content limit without budget, sanitize only superficially, treat retry as merge, clear dirty on failed/stale save, or leave duplicate unsafe route.

## Acceptance Checklist

- [ ] Selected known types resolve correct view/edit profiles; unsupported is explicit.
- [ ] Project Structure image/PDF double-click/dialog semantics are preserved and the direct path invokes no FileBrowser service/component/session.
- [ ] Hostile Markdown/SVG/unknown/oversize content remains inert/safe.
- [ ] Save success/conflict/failure/cancel/edit-during-save/overwrite policy passes.
- [ ] Old duplicate path removed only after proof; no unsigned bypass.
- [ ] Desktop renderer/dialog/preview/overlay/console and C# gates pass.

## Proof Required

- Governed manifest/hashes/transcripts/invariants, per-type producer/consumer/lifecycle matrix, failing/passing hostile/save/browser artifacts, package/source/no-bypass/anti-stub assertions.
- Shallow-pass traps: renderer selection works only through a browser-owned item/session, or old image/PDF branches remain behind the new dialog. Zero-browser-call spies, browser-disposal/replacement, and duplicate-path source assertions must reject these; realistic positives pass one authorized file directly for each claimed type.

## Browser Validation Logging

- Target Workbench and any migrated host routes; `1900x1200`, `1440x900`.
- Exercise text/Markdown view/edit/preview/history/save/conflict/retry/close guard, raster/PDF readiness, Mermaid, SVG/unknown/oversize inert states, replacement/disposal, overlay/dialog.
- Assert controlled mode/state/revision, dirty lifecycle, object/preview readiness, no clipping/scroll conflict, zero unexpected console/page/network errors.

## Progression Gate

- SB17 enters after every claimed migrated type has governed positive/negative/save/browser proof and no duplicate authority/effect path remains.

## Reopen Triggers

- Unsafe content, wrong renderer, stale replacement, direct-path browser invocation, asset dialog regression, dirty/save/revision error, package leak, duplicate viewer/effect, or new partial reopens SB16 and final proof.

## Suggested Agent Prompt

```text
Migrate FileInteraction one known type at a time. Use the existing builder and focused host adapters, govern save/content security and hostile cases, delete each legacy path only after proof, and stop without claiming unsupported formats.
```
