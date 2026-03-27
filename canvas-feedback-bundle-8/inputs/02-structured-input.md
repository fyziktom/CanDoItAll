# Structured Input

## Core Objective

- Fix the project-structure toolbox and selection panel so the experience matches the feedback screenshots and notes without regressing existing search and create-node flows.

## Hard Constraints

- Use the strengthened `candoitall-bundle-workflow` skill pack with real Playwright MCP validation, screenshots, and analytics logging.
- Prefer the smallest maintainable change in the existing Blazor/C# architecture.
- Preserve working toolbox search behavior and existing node-creation behavior.
- Keep contrast readable on light surfaces and do not add silent fallback behavior.

## Source Artifacts

- `C:\Users\lucys\OneDrive - TechnicInsider\Produkty\CanDoItAll\feedbacks\feedback8.docx`
- `C:\repositories\CanDoItAll\output\feedback8-extracted.md`
- `C:\repositories\CanDoItAll\output\feedback8-media\image1.png`
- `C:\repositories\CanDoItAll\output\feedback8-media\image2.png`
- `C:\repositories\CanDoItAll\output\feedback8-media\image3.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\baseline-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\excel-selection-desktop.png`

## Validation Expectations

- Build the affected project or solution slice.
- Run the relevant automated tests for the touched workbench components.
- Validate the changed UI in the real app with Playwright MCP, not by code inspection alone.
- Capture screenshots that prove the toolbox layout, accordion interaction, selection-panel trimming, and badge colors.

## UI Validation Strategy

- Large-screen pass at `1600x1000` on the project-structure page to validate default floating-window placement, toolbox clicks, node creation, selection-panel rendering, and badge colors.
- Follow-up narrower pass at `1280x900` if layout-affecting CSS changes are introduced, focused on overlap and readability.
- Screenshot review questions:
- Are toolbox group headers unobstructed and visibly interactive in the default layout?
- Does opening a group reveal its child nodes with no search regression?
- Does each selected node show only necessary information, with hints moved behind contextual help when needed?
- Do file-type badges use distinct semantic tones with readable contrast?

## Browser Validation Analytics

- Log one analytics row per subbundle in `C:\repositories\CanDoItAll\canvas-feedback-bundle-8\reviews\01-execution-report.md`.
- Each row must include the route under test, viewport, Playwright MCP actions/assertions, screenshot paths, and pass/fail result.
- Each subbundle README must define its required browser proof before implementation begins.

## Working Assumptions

- The feedback targets `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` and its related model and CSS files.
- Existing toolbox search behavior is considered correct and should remain intact.
- File-type badge colors should use existing design tokens or small local additions rather than a new styling system.

## Primary Risks

- Floating-window layout changes can regress other tool windows if positioning is changed too broadly.
- Selection-panel pruning can remove information that some node types still need if the node-type audit is incomplete.
- Badge-semantic changes can create contrast regressions if colors are not validated against the light canvas surfaces.
