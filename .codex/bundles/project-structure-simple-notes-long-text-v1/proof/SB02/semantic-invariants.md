# SB02 Semantic Invariants

- Invariant ID: `SB02-I001`
- Source raw note: `N003`, `N004`
- Expected behavior: a medium inline simple note uses more canvas width, Workbench placement mirrors CanvasLib sizing, and CanvasLib `0.1.1` restores with available `0.1.0` sibling packages.
- Disallowed shallow implementation: do not add page-local CSS overrides, skip package update, or rely on screenshot-only proof.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-browser-width.txt` recorded old rendered width `271px`.
- Passing test: `bundle://proof/SB02/transcripts/passing-browser-width.txt` and `bundle://proof/SB02/transcripts/passing-component-placement-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`, `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`, `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.1.nupkg`.
- Production assertions: `bundle://proof/source-assertions.txt` shows CanvasLib `maxWidth = 420`, DOM fallback `nodeElement.style.width`, CSS `white-space: pre-wrap`, and Workbench placement clamp to `420d`.
- Red-team negative case: the browser test uses a medium note body long enough to fail the old width contract, then converts the note to a task to prove mutation flow still renders normally.
- Downstream dependency check: `bundle://proof/SB02/transcripts/canvaslib-0.1.1-nuspec.txt` proves CanvasLib `0.1.1` depends on available `0.1.0` sibling packages.
