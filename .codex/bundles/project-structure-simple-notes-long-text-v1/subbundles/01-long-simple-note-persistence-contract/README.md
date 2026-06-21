# Long Simple Note Persistence Contract

## Status

- `Completed`

## Objective

- Make simple-note create/edit persistence explicit and reliable for long note bodies. The stored `Notes` value must preserve the full body, while the display `Title` is derived from the first non-empty line and bounded independently.

## Covered Inputs

- `N001`, `N002`
- `R001`, `R002`, `R003`, `R004`

## Prerequisites

- Prepared-stage bundle validation passed or any failure has been repaired.
- Re-read the raw request and current source references before editing.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureNodeHelpers.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureCreateRequestComposer.cs`
- `repo://src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`

The `C:\repositories\CanDoItAll.Components` path is a local package source aid; the consumed artifact in this repo remains `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.0.nupkg`.

## Deliverables

- Workbench create path derives simple-note quick-create title from note body before persisting.
- Long-note component test coverage for create/edit body preservation.
- Browser proof that inline composer create stores and reloads the full note body.

## Dependency Impact

- `SB02` depends on this subbundle. UI layout proof is not meaningful if the text being rendered does not survive save/reload.

## Validation Depth

- Critical foundation with component-test and browser-proof validation.
- Semantic Adequacy Gate required: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Implementation Steps

1. Add or update failing-first/component coverage proving quick-note create currently conflates long body and title or otherwise fails the desired persistence contract.
2. Update Workbench quick-note title/body normalization with the smallest code change.
3. If CanvasLib composer semantics must change for reliable long-note submission, update CanvasLib source and package consumption consistently.
4. Add passing component coverage for long create/edit note bodies.
5. Add or update browser proof so runtime node state exposes full `Notes` after inline composer create.
6. Record proof under `proof/SB01/`.

## Scope Exceptions

- Visual node width and screenshot review are owned by `SB02`, not this subbundle.

## Do Not Do

- Do not change unrelated project object persistence.
- Do not raise title column length unless proof shows it is required.
- Do not hide save failures with fallback text.
- Do not alter unrelated dirty files in `C:/repositories/CanDoItAll.Components`.

## Acceptance Checklist

- [x] Quick-note create: `Title` equals first non-empty line/title helper output.
- [x] Quick-note create: `Notes` equals full long body.
- [x] Inline-note edit coverage remains in the existing component test for note edits.
- [x] Browser proof checks `InlineText`, not only title or page text.
- [x] `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` exist.

## Proof Required

- Failing-first transcript under `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing targeted component test transcript under `bundle://proof/SB01/transcripts/passing-component-tests.txt`.
- Browser transcript/screenshot under `bundle://proof/SB01/browser/`.
- Source assertion transcript proving `Notes` is not max-length constrained by Workbench and quick-note title normalization is present.
- Anti-stub audit transcript for production `TODO`, `NotImplemented`, fixture-specific branching, and template-only logic.
- `bundle://proof/SB01/manifest.md` with changed-file hashes and portable paths.
- `bundle://proof/SB01/semantic-invariants.md` with invariant IDs for long body preservation and title derivation.

## Browser Validation Logging

- Route: `/projects/{createdProjectId}/structure`
- Viewport: first pass `1900x1200`
- Actions/assertions: create project, open inline note editor, enter long multiline note body, save, assert runtime `Notes`/`InlineText`, reload/refresh surface, assert full body again.
- Screenshot: `bundle://proof/SB01/browser/long-note-create.png`
- Review question: Does the browser proof inspect the stored note body rather than only a visible title fragment?

## Progression Gate

- `SB02` may start only after long-note create/edit tests pass, browser proof shows full note body preservation, and `proof/SB01/manifest.md` plus `proof/SB01/semantic-invariants.md` exist.

## Suggested Agent Prompt

```text
Implement SB01 only. Preserve full simple-note bodies in Notes, derive bounded note titles from the first non-empty line, add failing-first and passing proof for long create/edit notes, update the execution report and proof artifacts, and stop if the browser proof cannot inspect the stored note body.
```
