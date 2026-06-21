# Simple Note Canvas Space Use

## Status

- `Completed`

## Objective

- Make rendered simple-note cards use the width CanvasLib already measures for inline text, then prove with browser screenshots and DOM metrics that medium and long notes are more readable without overlap.

## Covered Inputs

- `N003`, `N004`
- `R005`, `R006`, `R007`

## Prerequisites

- `SB01` closure gate passed.
- Baseline screenshot `bundle://inputs/01-canvas-reference.png` reviewed.
- If CanvasLib package is changed, package rebuild/update path is confirmed.

## Exact Source References

- `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.0.nupkg`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\01-foundation.js`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\scene\04-scene-and-nodes.css`
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj`

The `C:\repositories\CanDoItAll.Components` paths are local package source aids; the consumed artifact in this repo remains `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.0.nupkg`.

## Deliverables

- CanvasLib inline-note DOM/card sizing matches measured inline-node width.
- Consumed CanvasLib package artifact/reference is updated if runtime assets change.
- Playwright browser proof captures desktop and narrower screenshots with DOM metrics.

## Dependency Impact

- Final closure depends on screenshot proof from this subbundle. If visual proof is weak, `N003` and `N004` remain open.

## Validation Depth

- Critical UI closure with browser screenshot proof and package proof.
- Semantic Adequacy Gate required: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Implementation Steps

1. Capture or record pre-change metric proof that inline note DOM width is fixed around `14.25rem` despite measured larger layout needs.
2. Update CanvasLib sizing/rendering so inline note DOM width uses `getNodeSize` or equivalent measured width.
3. Rebuild/update consumed package artifact/reference if CanvasLib source changes.
4. Add Playwright assertions for note DOM width, readable text, and no overlap.
5. Capture large desktop and narrower screenshots.
6. Record package/source hashes and proof artifacts under `proof/SB02/`.

## Scope Exceptions

- Storage semantics are owned by `SB01`.

## Do Not Do

- Do not add project-structure page-local CSS overrides when the issue belongs to CanvasLib runtime sizing.
- Do not replace CanvasLib components with ad hoc DOM.
- Do not accept screenshots without DOM assertions.
- Do not alter unrelated component library files.

## Acceptance Checklist

- [x] Baseline/reference screenshot has been reviewed.
- [x] After-change desktop screenshot captured and reviewed.
- [x] Browser screenshot captured after conversion to verify surrounding node stability.
- [x] Canvas renderer metric proof shows content-dependent inline-note width.
- [x] No obvious node overlaps or text/badge overlap in screenshots.
- [x] Package/version/hash proof recorded for CanvasLib `0.1.1`.
- [x] `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md` exist.

## Proof Required

- Failing-first/baseline transcript under `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing Playwright transcript under `bundle://proof/SB02/transcripts/passing-playwright.txt`.
- Package hash/version transcript under `bundle://proof/SB02/transcripts/package-proof.txt` if package changes.
- Screenshots under `bundle://proof/SB02/browser/simple-note-desktop.png` and `bundle://proof/SB02/browser/simple-note-narrow.png`.
- Source assertion transcript proving inline DOM width is tied to measured node size.
- Anti-stub audit transcript.
- `bundle://proof/SB02/manifest.md` with changed-file hashes and portable paths.
- `bundle://proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/projects/{createdProjectId}/structure`
- Viewports: `1900x1200` first; `1280x900` follow-up.
- Actions/assertions: create short, medium, and long simple notes; inspect `.cw-node.is-inline-text` bounding boxes; compare short versus medium/long widths; assert text box is contained by card; assert no overlaps among created notes.
- Screenshots: `bundle://proof/SB02/browser/simple-note-desktop.png`, `bundle://proof/SB02/browser/simple-note-narrow.png`
- Visual review questions: Is the medium note allowed to use available horizontal space? Is text readable? Are badges/annotations/collapse controls clear? Are neighboring nodes still separated?

## Progression Gate

- Final closure may start only after screenshots, DOM metric proof, package proof when applicable, and semantic proof artifacts are present and cited in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

```text
Implement SB02 only after SB01 is complete. Make CanvasLib inline note DOM rendering use measured width, rebuild/update the consumed package if needed, capture desktop and narrower browser screenshots, prove no overlap, update proof artifacts, and stop if screenshots or DOM metrics contradict the visual requirement.
```
