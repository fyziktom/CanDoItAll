# Structured Input

## Core Objective

- Fix project-structure simple notes so long inline notes persist their full text reliably, titles are derived predictably from note bodies, and rendered note cards use the available canvas width before wrapping or truncating too early.

## Success Criteria

- Long quick-note create stores the complete normalized note body in `Notes`.
- Long inline-note edit stores the complete normalized note body in `Notes`.
- Quick-note create stores a bounded first-line title rather than the whole long body as `Title`.
- Browser proof checks runtime and persisted note body state, not only visible page text.
- Screenshots after the change show simple-note cards using horizontal room more effectively with no text/badge/node overlap.

## Hard Constraints

- Preserve typed Workbench request/service boundaries.
- Do not introduce silent fallback mechanisms that hide failed saves.
- Do not use page-local wrapper markup to compensate for a CanvasLib runtime sizing defect.
- Do not touch unrelated dirty component-library work.

## Allowed Side Effects

- Inline note cards may become wider for medium/long text up to the existing CanvasLib measurement cap.
- The consumed local CanvasLib package may be rebuilt and version-bumped if runtime assets change.

## Source Artifacts

| Artifact | Type | Relevant observations |
| --- | --- | --- |
| `bundle://inputs/01-canvas-reference.png` | Screenshot | Shows desktop project-structure canvas and node cards with room around node content; supports the layout complaint but not the storage bug. |
| `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` | Workbench page | Quick note create/edit flows normalize titles and notes before calling the Workbench service. |
| `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureCreateRequestComposer.cs` | Workbench composer | Maps canvas create requests into typed `ProjectObjectCreateRequest` records. |
| `repo://src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs` | Persistence/service model | `ProjectObjectRecord.Notes` is configured as `TEXT`; storage should not be title-length limited. |
| `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.0.nupkg` | Local package artifact | Contains CanvasLib runtime JS/CSS consumed by the app. Execution may need to rebuild/update this package from the local `C:/repositories/CanDoItAll.Components` source workspace. |

## Input Coverage Signals

- `N001` and `N002` must not be collapsed into a short note smoke test; they require long-body create/edit proof.
- `N003` and `N004` require visual proof from real rendered canvas screenshots.

## Dependency And Sequencing Signals

- Persistence proof must land before visual proof. A rendered note is not trustworthy if the body will not survive save/reload.
- Package proof is required before browser screenshots can close if CanvasLib assets are changed.

## Validation Expectations

- Component tests for Workbench create/edit persistence.
- Playwright/browser proof for inline composer create and rendered card width.
- Screenshot review for desktop and narrower viewport.
- Bundle proof manifests and semantic invariants for both critical subbundles.

## Evidence Contract

- `bundle://proof/SB01/transcripts/failing-first.txt`
- `bundle://proof/SB01/transcripts/passing-component-tests.txt`
- `bundle://proof/SB01/browser/long-note-create.png`
- `bundle://proof/SB02/transcripts/failing-first.txt`
- `bundle://proof/SB02/transcripts/passing-playwright.txt`
- `bundle://proof/SB02/browser/simple-note-desktop.png`
- `bundle://proof/SB02/browser/simple-note-narrow.png`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`

## UI Validation Strategy

- Use a large desktop browser viewport first, then a narrower viewport.
- Review screenshots for text readability, space use, badge/control overlap, neighboring-node overlap, and card/hitbox consistency.
- DOM metrics must accompany screenshots so proof is not subjective only.

## Browser Validation Analytics

- Log route, viewport, actions, assertions, screenshots, and pass/fail result in `bundle://reviews/01-execution-report.md`.

## Working Assumptions

- Simple notes are Workbench `ProjectObjectType.Note` nodes with empty subtitle, mapped to CanvasLib inline text nodes.
- The correct persisted contract is full note body in `Notes`, derived bounded title in `Title`.

## Primary Risks

- Browser fixture may use stale NuGet package assets unless the package version/cache story is explicit.
- The exact user failure is intermittent, so proof must cover adversarial long text rather than a single short happy path.
