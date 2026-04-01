# 03-03-inline-note-multiline-and-note-conversion

## Status

- `Completed`

## Objective

- Upgrade simple-note editing so inline notes support multiline input with `Shift+Enter`, then add a supported conversion path from simple notes into common blocks without losing meaningful note content.

## Covered Inputs

- `N002`
- `N008`
- `RQ-02`
- `RQ-08`

## Prerequisites

- `02-02-catalog-expansion-and-type-mutation-flows` is completed.
- The common block mutation path is stable enough to reuse for note-to-block conversion.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- Inline simple-note editing that inserts a newline on `Shift+Enter` and preserves multiline content after save.
- A supported UI action that converts a simple note into a common block using the note text as the title source and retained body content.
- Mutation logic that preserves note meaning instead of truncating content to a single-line title.
- Automated proof for multiline persistence and note-to-block conversion.

## Dependency Impact

- `06-06-browser-proof-and-closure` depends on this phase because note editing and conversion are explicitly user-visible behaviors that must be proven in the final pass.
- Weak proof here makes later closure unreliable because note formatting regressions are easy to miss without explicit browser evidence.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Replace or upgrade the inline note editor contract so `Shift+Enter` inserts newlines while save behavior remains explicit.
2. Update the note-edit persistence path so multiline content survives page refresh and runtime surface patching.
3. Add the note-to-block conversion action and map note text into block title and retained body content using the shared block mutation infrastructure.
4. Add or update focused component tests for inline mutation behavior.
5. Prove multiline editing and note-to-block conversion in Playwright with screenshots.

## Do Not Do

- Do not solve multiline editing only in the full edit dialog while leaving the inline note composer single-line.
- Do not discard note content beyond the first line during conversion.
- Do not add a one-off conversion path that bypasses the shared block mutation rules introduced in subbundle `02`.

## Acceptance Checklist

- A simple note can accept at least one newline via `Shift+Enter` in the inline editor.
- The saved note renders and persists multiline content correctly.
- A simple note can be converted into a supported block through an explicit UI action.
- The converted block title and retained body content are both derived from the original note text in a predictable way.

## Proof Required

- Run focused component coverage for note mutation behavior.
- Run a Playwright flow that creates or edits a simple note, inserts multiple lines with `Shift+Enter`, saves it, and verifies the persisted display.
- In the same or a follow-up flow, convert a simple note into a block and verify the resulting title and body behavior.
- Capture screenshots for the multiline note state and the post-conversion block state.

## Browser Validation Logging

- Route under test: `/projects/{projectId}/structure`
- Required viewports: `1600x1000` large-screen proof and `1280x800` follow-up
- Required Playwright evidence: open or create a simple note, edit with `Shift+Enter`, assert multiline rendering after save, invoke the note-to-block action, and assert the converted block contents
- Required screenshots: `03-multiline-note.png`, `03-note-to-block-conversion.png`
- Screenshot review questions: are multiline breaks visibly preserved and does the converted block keep the note’s meaning instead of collapsing it to a label

## Progression Gate

- Final closure work may continue only after multiline note behavior is stable in the browser and note-to-block conversion preserves meaningful content with passing tests.

## Suggested Agent Prompt

```text
Implement subbundle 03-03-inline-note-multiline-and-note-conversion only. Upgrade inline note editing for Shift+Enter multiline behavior, add note-to-block conversion through the shared mutation flow, and produce the required component and Playwright proof.
```
