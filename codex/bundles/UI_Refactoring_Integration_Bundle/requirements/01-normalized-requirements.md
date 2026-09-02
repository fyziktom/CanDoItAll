# Normalized Requirements

## Functional and integration requirements

- **R-001** Integrate only the original `CanDoItAll/ui-refactoring` work.
- **R-002** Merge current `development` into `ui-refactoring` before application fixes.
- **R-003** Preserve current development behavior while retaining the original branch's valid
  deltas.
- **R-004** Stabilize merged `CanDoItAll.Components/main` before downstream closure.
- **R-005** Guarantee BaseLib static CSS for clean source-reference consumers.
- **R-006** Migrate the application from old Material Icons asset/DOM selectors to Material
  Symbols with `.cda-material-icon` as the stable contract.
- **R-007** Inspect FileTools and preserve its independence from Components.
- **R-008** Use one coordinated, higher, unused package version across Components, FileTools,
  and CanDoItAll fallback properties.
- **R-009** Update CanDoItAll sibling source pins to exact final commits.
- **R-010** Reconcile Podman/macOS documentation with the current source-reference model.
- **R-011** Validate both source-reference and package-reference modes.
- **R-012** Validate representative UI behavior on the supported large-desktop profile.
- **R-013** Validate FileBrowser and FileInteraction both standalone and in the main host.
- **R-014** Validate the source-context container build.
- **R-015** Produce merge-ready history and a complete execution report.
- **R-016** Preserve the canonical merge path through development to main.

## Governance requirements

- **G-001** No v2 unique commit may enter the integration history.
- **G-002** Snapshot/approval updates require semantic review.
- **G-003** Tests and skipped proof must be reported honestly.
- **G-004** No package publish or protected-branch merge without explicit authorization.
- **G-005** All new code/script comments are English.
- **G-006** No unrelated refactor or product redesign.
- **G-007** Do not weaken source/package/static-asset validation to force green status.

## Non-goals

- Merging or preparing `ui-refactoring-v2`.
- Implementing the v2 toolbar, themes, navigation, canonical URLs, or screen contracts.
- Redesigning mobile/tablet layouts.
- Replacing FileTools UI with Components.
- Publishing NuGet packages.
- Reworking general component APIs beyond proven integration defects.
- Removing Tailwind preflight without a demonstrated regression.
- Rewriting current product modules for aesthetic consistency.
